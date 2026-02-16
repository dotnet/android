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
	/// Managed type namespace, e.g., "Android.App".
	/// </summary>
	public string ManagedTypeNamespace { get; set; } = "";

	/// <summary>
	/// Managed type short name (without namespace), e.g., "Activity".
	/// </summary>
	public string ManagedTypeShortName { get; set; } = "";

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
	/// Java constructors to emit in the JCW .java file.
	/// Each has a JNI signature and an ordinal index for the nctor_N native method.
	/// </summary>
	public IReadOnlyList<JavaConstructorInfo> JavaConstructors { get; set; } = Array.Empty<JavaConstructorInfo> ();

	/// <summary>
	/// Information about the activation constructor for this type.
	/// May reference a base type's constructor if the type doesn't define its own.
	/// </summary>
	public ActivationCtorInfo? ActivationCtor { get; set; }

	/// <summary>
	/// Java fields generated from [ExportField] attributes.
	/// Each field is initialized by calling the associated managed method.
	/// </summary>
	public IReadOnlyList<ExportFieldInfo> ExportFields { get; set; } = Array.Empty<ExportFieldInfo> ();

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

	/// <summary>
	/// Component attribute data ([Activity], [Service], etc.). Null if no component attributes.
	/// </summary>
	public ComponentData? ComponentData { get; set; }
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
	/// Full name of the type that declares the managed method (may be a base type).
	/// </summary>
	public string DeclaringTypeName { get; set; } = "";

	/// <summary>
	/// Assembly name of the type that declares the managed method.
	/// Needed for cross-assembly UCO wrapper generation.
	/// </summary>
	public string DeclaringAssemblyName { get; set; } = "";

	/// <summary>
	/// The native callback method name, e.g., "n_onCreate".
	/// This is the actual method the UCO wrapper delegates to.
	/// </summary>
	public string NativeCallbackName { get; set; } = "";

	/// <summary>
	/// JNI parameter types for UCO generation.
	/// </summary>
	public IReadOnlyList<JniParameterInfo> Parameters { get; set; } = Array.Empty<JniParameterInfo> ();

	/// <summary>
	/// JNI return type descriptor, e.g., "V", "Landroid/os/Bundle;".
	/// </summary>
	public string JniReturnType { get; set; } = "";

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

	/// <summary>
	/// For [Export] methods: managed return type name, e.g., "System.String".
	/// Null for [Register] methods and constructors.
	/// </summary>
	public string? ManagedReturnType { get; set; }

	/// <summary>
	/// True if the method is static. Relevant for [Export] static methods.
	/// </summary>
	public bool IsStatic { get; set; }
}

/// <summary>
/// Describes a Java field generated from a method annotated with [ExportField].
/// The Java side declares a field initialized by calling the method.
/// </summary>
sealed class ExportFieldInfo
{
	/// <summary>
	/// The Java field name, e.g., "STATIC_INSTANCE".
	/// </summary>
	public string FieldName { get; set; } = "";

	/// <summary>
	/// Name of the managed method that initializes the field.
	/// Used both as the Java initializer method name and the native callback method name.
	/// </summary>
	public string MethodName { get; set; } = "";

	/// <summary>
	/// JNI return type descriptor, e.g., "Ljava/lang/String;".
	/// Determines the Java field type.
	/// </summary>
	public string JniReturnType { get; set; } = "";

	/// <summary>
	/// Whether the method (and thus the field) is static.
	/// </summary>
	public bool IsStatic { get; set; }
}

/// <summary>
/// Describes a JNI parameter for UCO method generation.
/// </summary>
sealed class JniParameterInfo
{
	/// <summary>
	/// JNI type descriptor, e.g., "Landroid/os/Bundle;", "I", "Z".
	/// </summary>
	public string JniType { get; set; } = "";

	/// <summary>
	/// Managed parameter type name, e.g., "Android.OS.Bundle", "System.Int32".
	/// </summary>
	public string ManagedType { get; set; } = "";
}

/// <summary>
/// Describes a Java constructor to emit in the JCW .java source file.
/// </summary>
sealed class JavaConstructorInfo
{
	/// <summary>
	/// JNI constructor signature, e.g., "(Landroid/content/Context;)V".
	/// </summary>
	public string JniSignature { get; set; } = "";

	/// <summary>
	/// Ordinal index for the native constructor method (nctor_0, nctor_1, ...).
	/// </summary>
	public int ConstructorIndex { get; set; }

	/// <summary>
	/// JNI parameter types parsed from the signature.
	/// Used to generate the Java constructor parameter list.
	/// </summary>
	public IReadOnlyList<JniParameterInfo> Parameters { get; set; } = Array.Empty<JniParameterInfo> ();

	/// <summary>
	/// For [Export] constructors: super constructor arguments string.
	/// Null for [Register] constructors.
	/// </summary>
	public string? SuperArgumentsString { get; set; }

	/// <summary>
	/// Whether this constructor is from [Export] attribute.
	/// </summary>
	public bool IsExport { get; set; }

	/// <summary>
	/// For [Export] constructors: Java exception types that the constructor declares it can throw.
	/// Null for [Register] constructors.
	/// </summary>
	public IReadOnlyList<string>? ThrownNames { get; set; }
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
