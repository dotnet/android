using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Java.Interop;

namespace Android.Runtime
{
	/// <summary>
	/// Abstraction for type mapping between Java and .NET types, and peer instance creation.
	/// </summary>
	interface ITypeMap
	{
		/// <summary>
		/// Tries to get the exact .NET type mapping for a given JNI type name.
		/// This performs a direct lookup without walking the Java class hierarchy.
		/// Used by <see cref="PeerCreationHelper.WalkHierarchy"/> to check each class in the hierarchy.
		/// </summary>
		/// <param name="jniTypeName">The JNI type name (e.g., "java/lang/Object").</param>
		/// <returns>The mapped .NET type, or null if no exact mapping exists.</returns>
		Type? TryGetExactTypeMapping (string jniTypeName);

		/// <summary>
		/// Tries to get all .NET types for a given JNI type name.
		/// Returns true if at least one type was found.
		/// </summary>
		bool TryGetTypesForJniName (string jniSimpleReference, [NotNullWhen (true)] out IEnumerable<Type>? types);

		/// <summary>
		/// Tries to get the JNI type name for a given .NET type.
		/// </summary>
		bool TryGetJniNameForType (Type type, [NotNullWhen (true)] out string? jniName);

		/// <summary>
		/// Tries to get all JNI type names for a given .NET type.
		/// Returns empty collection if no mappings exist.
		/// </summary>
		IEnumerable<string> GetJniNamesForType (Type type);

		/// <summary>
		/// Tries to get the invoker type for an interface or abstract class.
		/// Invoker types are concrete implementations that can wrap a JNI handle.
		/// </summary>
		/// <param name="type">The interface or abstract class type.</param>
		/// <param name="invokerType">The invoker type if found.</param>
		/// <returns>True if an invoker type was found, false otherwise.</returns>
		bool TryGetInvokerType (Type type, [NotNullWhen (true)] out Type? invokerType);

		/// <summary>
		/// Gets the JavaPeerProxy for a managed type.
		/// This is used to activate instances when the TypeManager returns the original type
		/// instead of the proxy type.
		/// </summary>
		/// <param name="managedType">The managed type to look up.</param>
		/// <returns>The proxy, or null if not found.</returns>
		JavaPeerProxy? GetProxyForManagedType (Type managedType);

		/// <summary>
		/// Creates a peer instance from a JNI handle.
		/// This is the main entry point for creating managed wrappers for Java objects.
		/// It resolves the type from the JNI handle, finds the appropriate invoker if needed,
		/// and creates the instance.
		/// </summary>
		/// <param name="handle">The JNI object handle.</param>
		/// <param name="transfer">How to handle the reference ownership.</param>
		/// <param name="targetType">Optional target type hint for the instance.</param>
		/// <returns>The created peer instance, or null if creation failed.</returns>
		IJavaPeerable? CreatePeer (
			IntPtr handle,
			JniHandleOwnership transfer,
			Type? targetType);

		/// <summary>
		/// Creates a 1D .NET array (T[]) of the specified element type.
		/// This is AOT-safe and does not use Array.CreateInstance reflection.
		/// </summary>
		/// <param name="elementType">The element type of the array to create. May be T or T[] for nested arrays.</param>
		/// <param name="length">The length of the array.</param>
		/// <param name="rank">The array rank: 1 for T[], 2 for T[][]. Default is 1.</param>
		/// <returns>A new array instance.</returns>
		/// <remarks>
		/// If elementType is itself an array type (T[]), this method unwraps it and
		/// increments the rank to create the correct jagged array type.
		/// Higher-rank arrays (T[][][]) are not supported and will throw.
		/// </remarks>
		Array CreateArray (Type elementType, int length, int rank);

		/// <summary>
		/// Resolves a marshal method function pointer by JNI class name and method index.
		/// Used by Type Mapping API stubs.
		/// </summary>
		IntPtr GetFunctionPointer (ReadOnlySpan<char> className, int methodIndex);
	}
}
