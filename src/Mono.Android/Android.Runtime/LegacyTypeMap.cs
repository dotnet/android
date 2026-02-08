using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using Java.Interop;

namespace Android.Runtime;

/// <summary>
/// Abstract base for Java↔.NET type mapping and peer creation.
///
/// Subclasses provide the lookup strategy (<see cref="GetManagedTypes"/>,
/// <see cref="TryGetJniTypeName"/>), and peer activation
/// (via <see cref="ITypeMap.CreatePeer"/>).
///
/// The shared hierarchy walking logic (<see cref="FindClosestManagedType"/>) lives in
/// this base class so every type map gets it automatically.
/// </summary>
internal abstract class LegacyTypeMap : ITypeMap
{
	record CacheKey (string JniTypeName, Type? TargetType);

	readonly ConcurrentDictionary<CacheKey, Type?> _cache = new ();

	/// <summary>
	/// Return all managed types mapped to a JNI type name.
	/// A single JNI class may have multiple aliases — e.g. <c>java/util/ArrayList</c>
	/// maps to <c>JavaList</c>, <c>JavaList&lt;T&gt;</c>, and <c>Java.Util.ArrayList</c>.
	/// </summary>
	/// <param name="jniTypeName">JNI class name, e.g. <c>"android/widget/Button"</c>.</param>
	/// <returns>All matching managed types, or an empty sequence if none found.</returns>
	public abstract IEnumerable<Type> GetManagedTypes (string jniTypeName);

	/// <summary>
	/// Resolve a managed <see cref="Type"/> to its JNI type name.
	/// </summary>
	/// <param name="managedType">The managed type to look up.</param>
	/// <param name="jniTypeName">The JNI class name, or <c>null</c> if not found.</param>
	/// <returns><c>true</c> if the mapping was found; <c>false</c> otherwise.</returns>
	public abstract bool TryGetJniTypeName (Type managedType, [NotNullWhen (true)] out string? jniTypeName);

	/// <summary>
	/// Create a managed peer instance for a Java object handle.
	/// Implementations should use <see cref="FindClosestManagedType"/> to resolve the type,
	/// then perform typemap-specific peer activation.
	/// </summary>
	/// <param name="handle">JNI object reference.</param>
	/// <param name="transfer">Handle ownership semantics.</param>
	/// <param name="targetType">Optional target type hint. If the resolved type is not assignable
	/// to <paramref name="targetType"/>, the target type is preferred.</param>
	/// <returns>The constructed peer, or <c>null</c> if creation failed.</returns>
	public abstract IJavaPeerable? CreatePeer (IntPtr handle, JniHandleOwnership transfer, Type? targetType);

	/// <summary>
	/// Walk the Java class hierarchy to find the best matching managed type.
	/// Starts with the instance's actual class and walks up to <c>java.lang.Object</c>.
	///
	/// At each hierarchy level, all aliases from <see cref="GetManagedTypes"/> are
	/// considered. The walk stops at the first level that has any mapped types.
	/// If <paramref name="targetType"/> is provided, the first alias assignable to
	/// it is preferred. If none match, falls back to <paramref name="targetType"/>.
	///
	/// Results are cached per <c>(className, targetType)</c> pair.
	/// </summary>
	protected Type? FindClosestManagedType (IntPtr handle, Type? targetType)
	{
		IntPtr classPtr = JNIEnv.GetObjectClass (handle);
		string className = JNIEnv.GetClassName (classPtr);

		return _cache.GetOrAdd(
			key: new CacheKey(className, targetType),
			valueFactory: WalkHierarchy,
			factoryArgument: classPtr);

		Type? WalkHierarchy (CacheKey key, IntPtr classPtr)
		{
			var (className, targetType) = key;

			while (classPtr != IntPtr.Zero) {
				foreach (var candidate in GetManagedTypes (className)) {
					if (targetType is null || targetType.IsAssignableFrom (candidate)) {
						JNIEnv.DeleteLocalRef (classPtr);
						return candidate;
					}
				}

				IntPtr superClassPtr = JNIEnv.GetSuperclass (classPtr);
				JNIEnv.DeleteLocalRef (classPtr);
				classPtr = superClassPtr;

				if (classPtr != IntPtr.Zero) {
					className = JNIEnv.GetClassName (classPtr);
				}
			}

			// No match found — fall back to targetType itself.
			return targetType;
		}
	}

}
