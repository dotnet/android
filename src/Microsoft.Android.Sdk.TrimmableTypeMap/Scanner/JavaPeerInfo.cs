using System.Collections.Generic;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Represents a Java peer type discovered during assembly scanning.
/// Contains all data needed by downstream generators (TypeMap IL, UCO wrappers, JCW Java sources).
/// Generators consume this data model — they never touch PEReader/MetadataReader.
/// </summary>
sealed record JavaPeerInfo
{
	/// <summary>
	/// JNI type name, e.g., "android/app/Activity".
	/// Extracted from the [Register] attribute.
	/// </summary>
	public required string JavaName { get; init; }

	/// <summary>
	/// Full managed type name, e.g., "Android.App.Activity".
	/// </summary>
	public required string ManagedTypeName { get; init; }

	/// <summary>
	/// Managed type namespace, e.g., "Android.App".
	/// </summary>
	public required string ManagedTypeNamespace { get; init; }

	/// <summary>
	/// Managed type short name (without namespace), e.g., "Activity".
	/// </summary>
	public required string ManagedTypeShortName { get; init; }

	/// <summary>
	/// Assembly name the type belongs to, e.g., "Mono.Android".
	/// </summary>
	public required string AssemblyName { get; init; }
	public bool IsInterface { get; init; }
	public bool IsAbstract { get; init; }

	/// <summary>
	/// If true, this is a Managed Callable Wrapper (MCW) binding type.
	/// No JCW or RegisterNatives will be generated for it.
	/// </summary>
	public bool DoNotGenerateAcw { get; init; }

	/// <summary>
	/// Types with component attributes ([Activity], [Service], etc.),
	/// custom views from layout XML, or manifest-declared components
	/// are unconditionally preserved (not trimmable).
	/// </summary>
	public bool IsUnconditional { get; init; }

	/// <summary>
	/// Information about the activation constructor for this type.
	/// May reference a base type's constructor if the type doesn't define its own.
	/// </summary>
	public ActivationCtorInfo? ActivationCtor { get; init; }

	/// <summary>
	/// For interfaces and abstract types, the name of the invoker type
	/// used to instantiate instances from Java.
	/// </summary>
	public string? InvokerTypeName { get; init; }

	/// <summary>
	/// JNI name of the base Java peer type, if any.
	/// </summary>
	public string? BaseJavaName { get; init; }

	/// <summary>
	/// JNI names of Java interfaces implemented by this type.
	/// </summary>
	public IReadOnlyList<string> ImplementedInterfaceJavaNames { get; init; } = [];

	/// <summary>
	/// True if this is an open generic type definition.
	/// Generic types get TypeMap entries but CreateInstance throws NotSupportedException.
	/// </summary>
	public bool IsGenericDefinition { get; init; }
}

/// <summary>
/// Describes how to call the activation constructor for a Java peer type.
/// </summary>
sealed record ActivationCtorInfo
{
	/// <summary>
	/// The type that declares the activation constructor.
	/// May be the type itself or a base type.
	/// </summary>
	public required string DeclaringTypeName { get; init; }

	/// <summary>
	/// The assembly containing the declaring type.
	/// </summary>
	public required string DeclaringAssemblyName { get; init; }

	/// <summary>
	/// The style of activation constructor found.
	/// </summary>
	public required ActivationCtorStyle Style { get; init; }
}

enum ActivationCtorStyle
{
	/// <summary>
	/// Xamarin.Android style: (IntPtr handle, JniHandleOwnership transfer)
	/// </summary>
	XamarinAndroid,

	/// <summary>
	/// Java.Interop style: (ref JniObjectReference reference, JniObjectReferenceOptions options)
	/// </summary>
	JavaInterop,
}
