using System;

namespace Android.Runtime
{
	/// <summary>
	/// Provides a shared algorithm for walking the Java class hierarchy to find a registered .NET type.
	/// Both legacy (LlvmIrTypeMap) and trimmable (TrimmableTypeMap) use identical hierarchy walking logic.
	/// </summary>
	static class JavaHierarchyWalker
	{
		/// <summary>
		/// Walks the Java class hierarchy starting from <paramref name="class_ptr"/> and queries
		/// <paramref name="typeMap"/> for each class until a type mapping is found.
		/// </summary>
		/// <param name="class_ptr">The starting Java class pointer. This ref is NOT deleted by this method.</param>
		/// <param name="class_name">The JNI name of the starting class.</param>
		/// <param name="typeMap">The type map to query for exact type mappings.</param>
		/// <returns>The first mapped .NET type found, or null if no mapping exists for any class in the hierarchy.</returns>
		/// <remarks>
		/// The algorithm:
		/// 1. Call typeMap.TryGetExactTypeMapping with the current class name
		/// 2. If found, return the result
		/// 3. Otherwise, get the superclass and repeat
		/// 4. Stop when there's no superclass (we've reached java.lang.Object's parent)
		///
		/// Local refs created for superclasses are deleted after use.
		/// The original class_ptr is NOT deleted.
		/// </remarks>
		public static Type? WalkHierarchy (IntPtr class_ptr, string class_name, ITypeMap typeMap)
		{
			if (class_ptr == IntPtr.Zero) {
				return null;
			}

			Type? result = null;
			IntPtr currentPtr = class_ptr;
			string? currentName = class_name;

			while (currentPtr != IntPtr.Zero) {
				if (currentName != null) {
					result = typeMap.TryGetExactTypeMapping (currentName);
					if (result != null) {
						break;
					}
				}

				IntPtr super_class_ptr = JNIEnv.GetSuperclass (currentPtr);

				// Delete local refs we created, but not the original class_ptr
				if (currentPtr != class_ptr) {
					JNIEnv.DeleteLocalRef (currentPtr);
				}

				currentPtr = super_class_ptr;
				currentName = currentPtr != IntPtr.Zero
					? Java.Interop.TypeManager.GetClassName (currentPtr)
					: null;
			}

			// Clean up the last pointer if it's not the original
			if (currentPtr != IntPtr.Zero && currentPtr != class_ptr) {
				JNIEnv.DeleteLocalRef (currentPtr);
			}

			return result;
		}
	}
}
