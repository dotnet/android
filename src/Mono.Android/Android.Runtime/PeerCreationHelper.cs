using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

using Java.Interop;
using Microsoft.Android.Runtime;

namespace Android.Runtime
{
	/// <summary>
	/// Shared hierarchy walking logic for peer creation.
	///
	/// <see cref="ITypeMap.CreatePeer"/> implementations use <see cref="WalkHierarchyForType"/>
	/// to find the managed type, then perform typemap-specific peer instantiation.
	/// </summary>
	internal static class PeerCreationHelper
	{
		/// <summary>
		/// Walk the Java class hierarchy to find the best matching managed type.
		/// Starts with the instance's actual class and walks up to java.lang.Object.
		/// </summary>
		/// <param name="typeMap">The type map to use for JNI→managed type lookups.</param>
		/// <param name="handle">JNI handle to the Java object.</param>
		/// <param name="targetType">
		/// Optional hint for the expected type. If the resolved type is not assignable to
		/// <paramref name="targetType"/>, <paramref name="targetType"/> is returned instead.
		/// </param>
		/// <returns>
		/// The managed type to instantiate, or <c>null</c> if no wrapper exists
		/// (which should not happen — java.lang.Object must always be mapped).
		/// </returns>
		internal static Type? WalkHierarchyForType (
			ITypeMap typeMap,
			IntPtr handle,
			Type? targetType)
		{
			Type? type = null;
			IntPtr class_ptr = JNIEnv.GetObjectClass (handle);
			string? class_name = Java.Interop.TypeManager.GetClassName (class_ptr);

			while (class_ptr != IntPtr.Zero) {
				if (typeMap.TryGetManagedType (class_name!, out var resolved)) {
					type = resolved;
					break;
				}

				IntPtr super_class_ptr = JNIEnv.GetSuperclass (class_ptr);
				JNIEnv.DeleteLocalRef (class_ptr);
				class_name = null;
				class_ptr = super_class_ptr;
				if (class_ptr != IntPtr.Zero) {
					class_name = Java.Interop.TypeManager.GetClassName (class_ptr);
				}
			}

			if (class_ptr != IntPtr.Zero) {
				JNIEnv.DeleteLocalRef (class_ptr);
				class_ptr = IntPtr.Zero;
			}

			if (targetType != null &&
					(type == null ||
					 !targetType.IsAssignableFrom (type))) {
				type = targetType;
			}

			return type;
		}

		internal static Exception CreateJavaLocationException ()
		{
			using (var loc = new Java.Lang.Error ("Java callstack:"))
				return new JavaLocationException (loc.ToString ());
		}
	}
}
