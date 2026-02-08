using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Java.Interop;
using Microsoft.Android.Runtime;

namespace Android.Runtime
{
	/// <summary>
	/// <see cref="LegacyTypeMap"/> implementation that wraps the existing native P/Invoke type mapping.
	///
	/// Java→.NET lookups use <c>monovm_typemap_java_to_managed()</c> (Mono) or
	/// <c>clr_typemap_java_to_managed()</c> (CoreCLR), with a managed cache
	/// via <see cref="TypeManagerMapDictionaries"/>.
	///
	/// .NET→Java lookups use <see cref="JNIEnv.TypemapManagedToJava"/>.
	///
	/// Invoker type resolution uses <see cref="JavaObjectExtensions.GetInvokerType"/>.
	/// Peer activation uses reflection (<see cref="RuntimeHelpers.GetUninitializedObject"/>
	/// + <see cref="ConstructorInfo.Invoke"/>).
	/// </summary>
	[RequiresUnreferencedCode ("Native type map relies on native code to resolve Java->.NET type mappings, which may not be preserved when trimming.")]
	internal class LegacyNativeTypeMap : LegacyTypeMap
	{
		[MethodImpl(MethodImplOptions.InternalCall)]
		static extern Type monodroid_typemap_java_to_managed (string java_type_name);

		static Type monovm_typemap_java_to_managed (string java_type_name)
		{
			return monodroid_typemap_java_to_managed (java_type_name);
		}

		static Type? clr_typemap_java_to_managed (string java_type_name)
		{
			bool result = RuntimeNativeMethods.clr_typemap_java_to_managed (java_type_name, out IntPtr managedAssemblyNamePointer, out uint managedTypeTokenId);
			if (!result || managedAssemblyNamePointer == IntPtr.Zero) {
				return null;
			}

			string managedAssemblyName = Marshal.PtrToStringAnsi (managedAssemblyNamePointer)!;
			Assembly assembly = Assembly.Load (managedAssemblyName);
			Type? ret = null;
			foreach (Module module in assembly.Modules) {
				ret = module.ResolveType ((int) managedTypeTokenId);
				if (ret != null) {
					break;
				}
			}

			if (Logger.LogAssembly) {
				Logger.Log (LogLevel.Info, "monodroid", $"Loaded type: {ret}");
			}

			return ret;
		}

		public override IEnumerable<Type> GetManagedTypes (string jniTypeName)
		{
			Type? type;
			lock (TypeManagerMapDictionaries.AccessLock) {
				type = GetJavaToManagedTypeCore (jniTypeName);
			}
			if (type != null) {
				yield return type;
			}
		}

		static Type? GetJavaToManagedTypeCore (string class_name)
		{
			if (TypeManagerMapDictionaries.JniToManaged.TryGetValue (class_name, out Type? type)) {
				return type;
			}

			if (RuntimeFeature.IsMonoRuntime) {
				type = monovm_typemap_java_to_managed (class_name);
			} else if (RuntimeFeature.IsCoreClrRuntime) {
				type = clr_typemap_java_to_managed (class_name);
			} else {
				throw new NotSupportedException ("Internal error: unknown runtime not supported");
			}

			if (type != null) {
				TypeManagerMapDictionaries.JniToManaged.Add (class_name, type);
				return type;
			}

			// Miss message is logged in the native runtime
			if (Logger.LogAssembly)
				JNIEnv.LogTypemapTrace (new System.Diagnostics.StackTrace (true));
			return null;
		}

		public override bool TryGetJniTypeName (Type managedType, [NotNullWhen (true)] out string? jniTypeName)
		{
			jniTypeName = JNIEnv.TypemapManagedToJava (managedType);
			return jniTypeName != null;
		}

		public override IJavaPeerable? CreatePeer (IntPtr handle, JniHandleOwnership transfer, Type? targetType)
		{
			Type? type = FindClosestManagedType (handle, targetType);

			if (type == null) {
				string class_name = JNIEnv.GetClassNameFromInstance (handle);
				JNIEnv.DeleteRef (handle, transfer);
				throw new NotSupportedException (
						FormattableString.Invariant ($"Internal error finding wrapper class for '{class_name}'. (Where is the Java.Lang.Object wrapper?!)"),
						JNIEnv.CreateJavaLocationException ());
			}

			// Resolve invoker if needed
			if (type.IsInterface || type.IsAbstract) {
				var invokerType = JavaObjectExtensions.GetInvokerType (type);
				if (invokerType == null)
					throw new NotSupportedException ("Unable to find Invoker for type '" + type.FullName + "'. Was it linked away?",
							JNIEnv.CreateJavaLocationException ());
				type = invokerType;
			}

			return ActivatePeer (type, handle, transfer);
		}

		static IJavaPeerable? ActivatePeer ([DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)] Type type, IntPtr handle, JniHandleOwnership transfer)
		{
			// Validate assignability
			var typeSig = JNIEnvInit.androidRuntime?.TypeManager.GetTypeSignature (type) ?? default;
			if (!typeSig.IsValid || typeSig.SimpleReference == null) {
				throw new ArgumentException ($"Could not determine Java type corresponding to `{type.AssemblyQualifiedName}`.", nameof (type));
			}

			JniObjectReference typeClass = default;
			JniObjectReference handleClass = default;
			try {
				try {
					typeClass = JniEnvironment.Types.FindClass (typeSig.SimpleReference);
				} catch (Exception e) {
					throw new ArgumentException ($"Could not find Java class `{typeSig.SimpleReference}`.",
							nameof (type),
							e);
				}

				handleClass = JniEnvironment.Types.GetObjectClass (new JniObjectReference (handle));
				if (!JniEnvironment.Types.IsAssignableFrom (handleClass, typeClass)) {
					if (Logger.LogAssembly) {
						var message = $"Handle 0x{handle:x} is of type '{JNIEnv.GetClassNameFromInstance (handle)}' which is not assignable to '{typeSig.SimpleReference}'";
						Logger.Log (LogLevel.Debug, "monodroid-assembly", message);
					}
					if (RuntimeFeature.IsAssignableFromCheck) {
						return null;
					}
				}
			} finally {
				JniObjectReference.Dispose (ref handleClass);
				JniObjectReference.Dispose (ref typeClass);
			}

			IJavaPeerable? result = null;

			try {
				result = (IJavaPeerable) CreateProxy (type, handle, transfer);
				if (Java.Interop.Runtime.IsGCUserPeer (result.PeerReference.Handle)) {
					result.SetJniManagedPeerState (JniManagedPeerStates.Replaceable | JniManagedPeerStates.Activatable);
				}
			} catch (MissingMethodException e) {
				var key_handle = JNIEnv.IdentityHash (handle);
				JNIEnv.DeleteRef (handle, transfer);
				throw new NotSupportedException (FormattableString.Invariant (
					$"Unable to activate instance of type {type} from native handle 0x{handle:x} (key_handle 0x{key_handle:x})."), e);
			}
			return result;
		}

		static readonly Type[] XAConstructorSignature = new Type [] { typeof (IntPtr), typeof (JniHandleOwnership) };
		static readonly Type[] JIConstructorSignature = new Type [] { typeof (JniObjectReference).MakeByRefType (), typeof (JniObjectReferenceOptions) };

		static object CreateProxy (Type type, IntPtr handle, JniHandleOwnership transfer)
		{
			var peer = GetUninitializedObject (type);
			BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
			var c = type.GetConstructor (flags, null, XAConstructorSignature, null);
			if (c != null) {
				c.Invoke (peer, new object[] { handle, transfer });
				return peer;
			}
			c = type.GetConstructor (flags, null, JIConstructorSignature, null);
			if (c != null) {
				JniObjectReference          r = new JniObjectReference (handle);
				JniObjectReferenceOptions   o = JniObjectReferenceOptions.Copy;
				c.Invoke (peer, new object [] { r, o });
				JNIEnv.DeleteRef (handle, transfer);
				return peer;
			}
			GC.SuppressFinalize (peer);
			throw new MissingMethodException (
					"No constructor found for " + type.FullName + "::.ctor(System.IntPtr, Android.Runtime.JniHandleOwnership)",
					JNIEnv.CreateJavaLocationException ());

			static IJavaPeerable GetUninitializedObject (Type type)
			{
				var v = (IJavaPeerable) RuntimeHelpers.GetUninitializedObject (type);
				v.SetJniManagedPeerState (JniManagedPeerStates.Replaceable | JniManagedPeerStates.Activatable);
				return v;
			}
		}
	}
}
