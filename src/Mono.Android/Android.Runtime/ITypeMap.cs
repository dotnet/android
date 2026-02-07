using System;
using System.Diagnostics.CodeAnalysis;

using Java.Interop;

namespace Android.Runtime
{
	/// <summary>
	/// Abstraction for Java↔.NET type mapping and peer creation.
	///
	/// Implementations provide lookup between JNI type names and .NET types,
	/// and creation of managed peer instances for Java objects.
	///
	/// Native member registration (<c>RegisterNativeMembers</c>) is NOT part of this interface —
	/// it stays in <see cref="JniRuntime.JniTypeManager"/> subclasses, as it depends on the
	/// specific runtime's callback registration mechanism.
	///
	/// To add a new type mapping strategy (e.g., the trimmable typemap), implement this interface
	/// and wire it into <see cref="JNIEnvInit.Initialize"/> based on the appropriate feature flag.
	/// </summary>
	interface ITypeMap
	{
		/// <summary>
		/// Resolve a JNI type name to a managed <see cref="Type"/>.
		/// </summary>
		/// <param name="jniTypeName">JNI class name, e.g. <c>"android/widget/Button"</c>.</param>
		/// <param name="managedType">The resolved managed type, or <c>null</c> if not found.</param>
		/// <returns><c>true</c> if the type was found; <c>false</c> otherwise.</returns>
		bool TryGetManagedType (string jniTypeName, [NotNullWhen (true)] out Type? managedType);

		/// <summary>
		/// Resolve a managed <see cref="Type"/> to its JNI type name.
		/// </summary>
		/// <param name="managedType">The managed type to look up.</param>
		/// <param name="jniTypeName">The JNI class name, or <c>null</c> if not found.</param>
		/// <returns><c>true</c> if the mapping was found; <c>false</c> otherwise.</returns>
		bool TryGetJniTypeName (Type managedType, [NotNullWhen (true)] out string? jniTypeName);

		/// <summary>
		/// Create a managed peer instance for a Java object handle.
		/// Walks the Java class hierarchy to find the best matching .NET type,
		/// resolves invoker types for interfaces/abstract classes, and activates the peer.
		/// </summary>
		/// <param name="handle">JNI object reference.</param>
		/// <param name="transfer">Handle ownership semantics.</param>
		/// <param name="targetType">Optional target type hint. If the resolved type is not assignable
		/// to <paramref name="targetType"/>, the target type is preferred.</param>
		/// <returns>The constructed peer, or <c>null</c> if creation failed.</returns>
		IJavaPeerable? CreatePeer (IntPtr handle, JniHandleOwnership transfer, Type? targetType);
	}
}
