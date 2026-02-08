using System;

using Java.Interop;

namespace Android.Runtime;

/// <summary>
/// Abstraction for peer creation from a Java object handle.
///
/// <see cref="LegacyTypeMap"/> provides the full legacy implementation with
/// hierarchy walking and reflection-based activation. Future type map
/// implementations (e.g., trimmable typemap) can implement this interface
/// directly with a completely different strategy.
/// </summary>
internal interface ITypeMap
{
	/// <summary>
	/// Create a managed peer instance for a Java object handle.
	/// </summary>
	/// <param name="handle">JNI object reference.</param>
	/// <param name="transfer">Handle ownership semantics.</param>
	/// <param name="targetType">Optional target type hint. If the resolved type is not assignable
	/// to <paramref name="targetType"/>, the target type is preferred.</param>
	/// <returns>The constructed peer, or <c>null</c> if creation failed.</returns>
	IJavaPeerable? CreatePeer (IntPtr handle, JniHandleOwnership transfer, Type? targetType);
}
