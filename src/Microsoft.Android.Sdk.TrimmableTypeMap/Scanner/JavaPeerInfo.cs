using System;
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
	/// Compat JNI type name, e.g., "myapp.namespace/MyType" for user types (uses raw namespace, not CRC64).
	/// For MCW binding types (with [Register]), this equals <see cref="JavaName"/>.
	/// Used by acw-map.txt to support legacy custom view name resolution in layout XMLs.
	/// </summary>
	public required string CompatJniName { get; init; }

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
	/// Marshal methods: methods with [Register(name, sig, connector)], [Export], or
	/// constructor registrations ([Register(".ctor", sig, "")] / [JniConstructorSignature]).
	/// Constructors are identified by <see cref="MarshalMethodInfo.IsConstructor"/>.
	/// Ordered — the index in this list is the method's ordinal for RegisterNatives.
	/// </summary>
	public IReadOnlyList<MarshalMethodInfo> MarshalMethods { get; init; } = Array.Empty<MarshalMethodInfo> ();

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
	/// True if this is an open generic type definition.
	/// Generic types get TypeMap entries but CreateInstance throws NotSupportedException.
	/// </summary>
	public bool IsGenericDefinition { get; init; }
}

/// <summary>
/// Describes a marshal method (a method with [Register] or [Export]) on a Java peer type.
/// Contains all data needed to generate a UCO wrapper, a JCW native declaration,
/// and a RegisterNatives call.
/// </summary>
sealed record MarshalMethodInfo
{
	/// <summary>
	/// JNI method name, e.g., "onCreate".
	/// This is the Java method name (without n_ prefix).
	/// </summary>
	public required string JniName { get; init; }

	/// <summary>
	/// JNI method signature, e.g., "(Landroid/os/Bundle;)V".
	/// Contains both parameter types and return type.
	/// </summary>
	public required string JniSignature { get; init; }

	/// <summary>
	/// The connector string from [Register], e.g., "GetOnCreate_Landroid_os_Bundle_Handler".
	/// Null for [Export] methods.
	/// </summary>
	public string? Connector { get; init; }

	/// <summary>
	/// Name of the managed method this maps to, e.g., "OnCreate".
	/// </summary>
	public required string ManagedMethodName { get; init; }

	/// <summary>
	/// Full name of the type that declares the managed method (may be a base type).
	/// Empty when the declaring type is the same as the peer type.
	/// </summary>
	public string DeclaringTypeName { get; init; } = "";

	/// <summary>
	/// Assembly name of the type that declares the managed method.
	/// Needed for cross-assembly UCO wrapper generation.
	/// Empty when the declaring type is the same as the peer type.
	/// </summary>
	public string DeclaringAssemblyName { get; init; } = "";

	/// <summary>
	/// The native callback method name, e.g., "n_onCreate".
	/// This is the actual method the UCO wrapper delegates to.
	/// </summary>
	public required string NativeCallbackName { get; init; }

	/// <summary>
	/// JNI parameter types for UCO generation.
	/// </summary>
	public IReadOnlyList<JniParameterInfo> Parameters { get; init; } = Array.Empty<JniParameterInfo> ();

	/// <summary>
	/// JNI return type descriptor, e.g., "V", "Landroid/os/Bundle;".
	/// </summary>
	public required string JniReturnType { get; init; }

	/// <summary>
	/// True if this is a constructor registration.
	/// </summary>
	public bool IsConstructor { get; init; }

	/// <summary>
	/// For [Export] methods: Java exception types that the method declares it can throw.
	/// Null for [Register] methods.
	/// </summary>
	public IReadOnlyList<string>? ThrownNames { get; init; }

	/// <summary>
	/// For [Export] methods: super constructor arguments string.
	/// Null for [Register] methods.
	/// </summary>
	public string? SuperArgumentsString { get; init; }
}

/// <summary>
/// Describes a JNI parameter for UCO method generation.
/// </summary>
sealed record JniParameterInfo
{
	/// <summary>
	/// JNI type descriptor, e.g., "Landroid/os/Bundle;", "I", "Z".
	/// </summary>
	public required string JniType { get; init; }

	/// <summary>
	/// Managed parameter type name, e.g., "Android.OS.Bundle", "System.Int32".
	/// </summary>
	public string ManagedType { get; init; } = "";
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
