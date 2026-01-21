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
		const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

		/// <summary>
		/// Tries to get all .NET types for a given JNI type name.
		/// Returns true if at least one type was found.
		/// </summary>
		bool TryGetTypesForJniName (string jniSimpleReference, [NotNullWhen (true)] out IEnumerable<Type>? types);

		/// <summary>
		/// Tries to get the invoker type for an interface or abstract type.
		/// </summary>
		bool TryGetInvokerType (Type type, [NotNullWhen (true)][DynamicallyAccessedMembers (Constructors)] out Type? invokerType);

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
			[DynamicallyAccessedMembers (Constructors)]
			Type? targetType);
	}
}
