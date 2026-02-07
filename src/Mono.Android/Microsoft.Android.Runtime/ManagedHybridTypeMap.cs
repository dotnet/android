using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

using Android.Runtime;
using Java.Interop;

namespace Microsoft.Android.Runtime
{
	/// <summary>
	/// <see cref="ITypeMap"/> implementation that wraps <see cref="ManagedTypeMapping"/>'s
	/// hash-based lookups. Used when <see cref="RuntimeFeature.ManagedTypeMap"/> is enabled
	/// (NativeAOT / CoreCLR with managed type maps).
	///
	/// Invoker type resolution uses the <c>TypeNameInvoker</c> convention.
	/// </summary>
	class ManagedHybridTypeMap : ITypeMap
	{
		const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

		public bool TryGetManagedType (string jniTypeName, [NotNullWhen (true)] out Type? managedType)
		{
			return ManagedTypeMapping.TryGetType (jniTypeName, out managedType);
		}

		public bool TryGetJniTypeName (Type managedType, [NotNullWhen (true)] out string? jniTypeName)
		{
			return ManagedTypeMapping.TryGetJniName (managedType, out jniTypeName);
		}

		public IJavaPeerable? CreatePeer (IntPtr handle, JniHandleOwnership transfer, Type? targetType)
		{
			// Walk Java hierarchy to find the managed type
			Type? type = PeerCreationHelper.WalkHierarchyForType (this, handle, targetType);

			if (type == null) {
				string class_name = JNIEnv.GetClassNameFromInstance (handle);
				JNIEnv.DeleteRef (handle, transfer);
				throw new NotSupportedException (
						FormattableString.Invariant ($"Internal error finding wrapper class for '{class_name}'. (Where is the Java.Lang.Object wrapper?!)"),
						PeerCreationHelper.CreateJavaLocationException ());
			}

			// Managed hybrid typemap: reflection-based instantiation with custom invoker resolution
			return InstantiatePeer (type, handle, transfer, targetType);
		}

		[UnconditionalSuppressMessage ("Trimming", "IL2067", Justification = "Reflection-based instantiation is only used by legacy type maps.")]
		[UnconditionalSuppressMessage ("Trimming", "IL2072", Justification = "Reflection-based instantiation is only used by legacy type maps.")]
		static IJavaPeerable? InstantiatePeer (
			Type type,
			IntPtr handle,
			JniHandleOwnership transfer,
			Type? targetType)
		{
			// Resolve invoker if needed
			if (type.IsInterface || type.IsAbstract) {
				var invokerType = ResolveInvokerType (type);
				if (invokerType == null)
					throw new NotSupportedException ("Unable to find Invoker for type '" + type.FullName + "'. Was it linked away?",
							PeerCreationHelper.CreateJavaLocationException ());
				type = invokerType;
			}

			// Validate assignability
			var typeSig = JNIEnvInit.androidRuntime?.TypeManager.GetTypeSignature (type) ?? default;
			if (!typeSig.IsValid || typeSig.SimpleReference == null) {
				throw new ArgumentException ($"Could not determine Java type corresponding to `{type.AssemblyQualifiedName}`.", nameof (targetType));
			}

			JniObjectReference typeClass = default;
			JniObjectReference handleClass = default;
			try {
				try {
					typeClass = JniEnvironment.Types.FindClass (typeSig.SimpleReference);
				} catch (Exception e) {
					throw new ArgumentException ($"Could not find Java class `{typeSig.SimpleReference}`.",
							nameof (targetType),
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

			// Instantiate via reflection
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

		static object CreateProxy (
				[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
				Type type,
				IntPtr handle,
				JniHandleOwnership transfer)
		{
			// Skip Activator.CreateInstance() as that requires public constructors,
			// and we want to hide some constructors for sanity reasons.
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
					PeerCreationHelper.CreateJavaLocationException ());

			static IJavaPeerable GetUninitializedObject (
					[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
					Type type)
			{
				var v = (IJavaPeerable) RuntimeHelpers.GetUninitializedObject (type);
				v.SetJniManagedPeerState (JniManagedPeerStates.Replaceable | JniManagedPeerStates.Activatable);
				return v;
			}
		}

		// Invoker resolution follows the same convention as ManagedTypeManager.GetInvokerTypeCore()
		const string Suffix = "Invoker";
		const string assemblyGetTypeMessage = "'Invoker' types are preserved by the MarkJavaObjects trimmer step.";
		const string makeGenericTypeMessage = "Generic 'Invoker' types are preserved by the MarkJavaObjects trimmer step.";

		[return: DynamicallyAccessedMembers (Constructors)]
		static Type? ResolveInvokerType (Type type)
		{
			if (!type.IsInterface && !type.IsAbstract)
				return null;

			Type[] arguments = type.GetGenericArguments ();
			if (arguments.Length == 0)
				return AssemblyGetType (type.Assembly, type + Suffix);
			Type definition = type.GetGenericTypeDefinition ();
			int bt = definition.FullName!.IndexOf ("`", StringComparison.Ordinal);
			if (bt == -1)
				throw new NotSupportedException ("Generic type doesn't follow generic type naming convention! " + type.FullName);
			Type? suffixDefinition = AssemblyGetType (definition.Assembly,
					definition.FullName.Substring (0, bt) + Suffix + definition.FullName.Substring (bt));
			if (suffixDefinition == null)
				return null;
			return MakeGenericType (suffixDefinition, arguments);
		}

		[UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = assemblyGetTypeMessage)]
		[UnconditionalSuppressMessage ("Trimming", "IL2073", Justification = assemblyGetTypeMessage)]
		[return: DynamicallyAccessedMembers (Constructors)]
		static Type? AssemblyGetType (Assembly assembly, string typeName) =>
			assembly.GetType (typeName);

		[UnconditionalSuppressMessage ("Trimming", "IL2055", Justification = makeGenericTypeMessage)]
		[return: DynamicallyAccessedMembers (Constructors)]
		static Type MakeGenericType (
				[DynamicallyAccessedMembers (Constructors)]
				Type type,
				Type[] arguments) =>
			// FIXME: https://github.com/dotnet/java-interop/issues/1192
			#pragma warning disable IL3050
			type.MakeGenericType (arguments);
			#pragma warning restore IL3050
	}
}
