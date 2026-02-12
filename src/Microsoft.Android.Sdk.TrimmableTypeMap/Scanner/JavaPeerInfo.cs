using System;
using System.Collections.Generic;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Represents a Java peer type discovered during assembly scanning.
/// Contains all data needed by downstream generators (TypeMap IL, UCO wrappers, JCW Java sources).
/// Generators consume this data model — they never touch PEReader/MetadataReader.
/// </summary>
sealed class JavaPeerInfo
{
	/// <summary>
	/// JNI type name, e.g., "android/app/Activity".
	/// Extracted from the [Register] attribute.
	/// </summary>
	public string JavaName { get; set; } = "";

	/// <summary>
	/// Compat JNI type name, e.g., "myapp.namespace/MyType" for user types (uses raw namespace, not CRC64).
	/// For MCW binding types (with [Register]), this equals <see cref="JavaName"/>.
	/// Used by acw-map.txt to support legacy custom view name resolution in layout XMLs.
	/// </summary>
	public string CompatJniName { get; set; } = "";

	/// <summary>
	/// Full managed type name, e.g., "Android.App.Activity".
	/// </summary>
	public string ManagedTypeName { get; set; } = "";

	/// <summary>
	/// Assembly name the type belongs to, e.g., "Mono.Android".
	/// </summary>
	public string AssemblyName { get; set; } = "";

	/// <summary>
	/// JNI name of the base Java type, e.g., "android/app/Activity" for a type
	/// that extends Activity. Null for java/lang/Object or types without a Java base.
	/// Needed by JCW Java source generation ("extends" clause).
	/// </summary>
	public string? BaseJavaName { get; set; }

	/// <summary>
	/// JNI names of Java interfaces this type implements, e.g., ["android/view/View$OnClickListener"].
	/// Needed by JCW Java source generation ("implements" clause).
	/// </summary>
	public IReadOnlyList<string> ImplementedInterfaceJavaNames { get; set; } = Array.Empty<string> ();

	public bool IsInterface { get; set; }
	public bool IsAbstract { get; set; }

	/// <summary>
	/// If true, this is a Managed Callable Wrapper (MCW) binding type.
	/// No JCW or RegisterNatives will be generated for it.
	/// </summary>
	public bool DoNotGenerateAcw { get; set; }

	/// <summary>
	/// Types with component attributes ([Activity], [Service], etc.),
	/// custom views from layout XML, or manifest-declared components
	/// are unconditionally preserved (not trimmable).
	/// </summary>
	public bool IsUnconditional { get; set; }

	/// <summary>
	/// Marshal methods: methods with [Register(name, sig, connector)], [Export], or
	/// constructor registrations ([Register(".ctor", sig, "")] / [JniConstructorSignature]).
	/// Constructors are identified by <see cref="MarshalMethodInfo.IsConstructor"/>.
	/// Ordered — the index in this list is the method's ordinal for RegisterNatives.
	/// </summary>
	public IReadOnlyList<MarshalMethodInfo> MarshalMethods { get; set; } = Array.Empty<MarshalMethodInfo> ();

	/// <summary>
	/// Information about the activation constructor for this type.
	/// May reference a base type's constructor if the type doesn't define its own.
	/// </summary>
	public ActivationCtorInfo? ActivationCtor { get; set; }

	/// <summary>
	/// For interfaces and abstract types, the name of the invoker type
	/// used to instantiate instances from Java.
	/// </summary>
	public string? InvokerTypeName { get; set; }

	/// <summary>
	/// True if this is an open generic type definition.
	/// Generic types get TypeMap entries but CreateInstance throws NotSupportedException.
	/// </summary>
	public bool IsGenericDefinition { get; set; }
}

/// <summary>
/// Describes a marshal method (a method with [Register] or [Export]) on a Java peer type.
/// Contains all data needed to generate a UCO wrapper, a JCW native declaration,
/// and a RegisterNatives call.
/// </summary>
sealed class MarshalMethodInfo
{
	/// <summary>
	/// JNI method name, e.g., "onCreate".
	/// This is the Java method name (without n_ prefix).
	/// </summary>
	public string JniName { get; set; } = "";

	/// <summary>
	/// JNI method signature, e.g., "(Landroid/os/Bundle;)V".
	/// Contains both parameter types and return type.
	/// </summary>
	public string JniSignature { get; set; } = "";

	/// <summary>
	/// The connector string from [Register], e.g., "GetOnCreate_Landroid_os_Bundle_Handler".
	/// Null for [Export] methods.
	/// </summary>
	public string? Connector { get; set; }

	/// <summary>
	/// Name of the managed method this maps to, e.g., "OnCreate".
	/// </summary>
	public string ManagedMethodName { get; set; } = "";

	/// <summary>
	/// True if this is a constructor registration.
	/// </summary>
	public bool IsConstructor { get; set; }

	/// <summary>
	/// For [Export] methods: Java exception types that the method declares it can throw.
	/// Null for [Register] methods.
	/// </summary>
	public IReadOnlyList<string>? ThrownNames { get; set; }

	/// <summary>
	/// For [Export] methods: super constructor arguments string.
	/// Null for [Register] methods.
	/// </summary>
	public string? SuperArgumentsString { get; set; }
}

/// <summary>
/// Describes how to call the activation constructor for a Java peer type.
/// </summary>
sealed class ActivationCtorInfo
{
	/// <summary>
	/// The type that declares the activation constructor.
	/// May be the type itself or a base type.
	/// </summary>
	public string DeclaringTypeName { get; set; } = "";

	/// <summary>
	/// The assembly containing the declaring type.
	/// </summary>
	public string DeclaringAssemblyName { get; set; } = "";

	/// <summary>
	/// The style of activation constructor found.
	/// </summary>
	public ActivationCtorStyle Style { get; set; }
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
