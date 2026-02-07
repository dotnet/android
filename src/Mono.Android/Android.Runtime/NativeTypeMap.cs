using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Java.Interop;
using Microsoft.Android.Runtime;

namespace Android.Runtime
{
	/// <summary>
	/// <see cref="ITypeMap"/> implementation that wraps the existing native P/Invoke type mapping.
	///
	/// Java→.NET lookups use <c>monovm_typemap_java_to_managed()</c> (Mono) or
	/// <c>clr_typemap_java_to_managed()</c> (CoreCLR), with a managed cache
	/// via <see cref="TypeManagerMapDictionaries"/>.
	///
	/// .NET→Java lookups use <see cref="JNIEnv.TypemapManagedToJava"/>.
	///
	/// Invoker type resolution uses <see cref="JavaObjectExtensions.GetInvokerType"/>.
	/// </summary>
	class NativeTypeMap : ITypeMap
	{
		[MethodImplAttribute (MethodImplOptions.InternalCall)]
		static extern Type monodroid_typemap_java_to_managed (string java_type_name);

		static Type monovm_typemap_java_to_managed (string java_type_name)
		{
			return monodroid_typemap_java_to_managed (java_type_name);
		}

		[UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "Value of java_type_name isn't statically known.")]
		static Type? clr_typemap_java_to_managed (string java_type_name)
		{
			bool result = RuntimeNativeMethods.clr_typemap_java_to_managed (java_type_name, out IntPtr managedAssemblyNamePointer, out uint managedTypeTokenId);
			if (!result || managedAssemblyNamePointer == IntPtr.Zero) {
				return null;
			}

			string managedAssemblyName = Marshal.PtrToStringAnsi (managedAssemblyNamePointer);
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

		public bool TryGetManagedType (string jniTypeName, [NotNullWhen (true)] out Type? managedType)
		{
			lock (TypeManagerMapDictionaries.AccessLock) {
				managedType = GetJavaToManagedTypeCore (jniTypeName);
			}
			return managedType != null;
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

		public bool TryGetJniTypeName (Type managedType, [NotNullWhen (true)] out string? jniTypeName)
		{
			jniTypeName = JNIEnv.TypemapManagedToJava (managedType);
			return jniTypeName != null;
		}

		public IJavaPeerable? CreatePeer (IntPtr handle, JniHandleOwnership transfer, Type? targetType)
		{
			return PeerCreationHelper.CreatePeer (this, JavaObjectExtensions.GetInvokerType, handle, transfer, targetType);
		}
	}
}
