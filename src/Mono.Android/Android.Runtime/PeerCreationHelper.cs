using System;
using System.Collections.Concurrent;

using Java.Interop;

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
		static readonly ConcurrentDictionary<string, Type> _hierarchyCache = new ConcurrentDictionary<string, Type> (StringComparer.Ordinal);

		/// <summary>
		/// Walk the Java class hierarchy to find the best matching managed type.
		/// Starts with the instance's actual class and walks up to java.lang.Object.
		///
		/// Results are cached: once a JNI class name has been resolved to a managed type
		/// (possibly by walking up the hierarchy), subsequent lookups for the same class
		/// name hit the cache directly without JNI calls.
		/// </summary>
		internal static Type? WalkHierarchyForType (
			ITypeMap typeMap,
			IntPtr handle,
			Type? targetType)
		{
			IntPtr class_ptr = JNIEnv.GetObjectClass (handle);
			string class_name = Java.Interop.TypeManager.GetClassName (class_ptr);
			string original_class_name = class_name;

			// Check cache first
			if (_hierarchyCache.TryGetValue (original_class_name, out var cached)) {
				JNIEnv.DeleteLocalRef (class_ptr);
				return ApplyTargetType (cached, targetType);
			}

			Type? type = null;

			while (class_ptr != IntPtr.Zero) {
				if (typeMap.TryGetManagedType (class_name, out var resolved)) {
					type = resolved;
					break;
				}

				IntPtr super_class_ptr = JNIEnv.GetSuperclass (class_ptr);
				JNIEnv.DeleteLocalRef (class_ptr);
				class_ptr = super_class_ptr;
				if (class_ptr != IntPtr.Zero) {
					class_name = Java.Interop.TypeManager.GetClassName (class_ptr);
				}
			}

			if (class_ptr != IntPtr.Zero) {
				JNIEnv.DeleteLocalRef (class_ptr);
			}

			// Cache the walk result so we don't re-walk next time
			if (type != null) {
				_hierarchyCache [original_class_name] = type;
			}

			return ApplyTargetType (type, targetType);
		}

		static Type? ApplyTargetType (Type? type, Type? targetType)
		{
			if (targetType != null &&
					(type == null ||
					 !targetType.IsAssignableFrom (type))) {
				return targetType;
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
