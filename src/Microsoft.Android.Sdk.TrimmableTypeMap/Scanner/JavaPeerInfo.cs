using System;
using System.Collections.Generic;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Represents a Java peer type discovered during assembly scanning.
/// Contains all data needed by downstream generators (TypeMap IL, UCO wrappers, JCW Java sources).
/// Generators consume this data model — they never touch PEReader/MetadataReader.
/// </summary>
public sealed record JavaPeerInfo
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

	/// <summary>
	/// JNI name of the base Java type, e.g., "android/app/Activity" for a type
	/// that extends Activity. Null for java/lang/Object or types without a Java base.
	/// Needed by JCW Java source generation ("extends" clause).
	/// </summary>
	public string? BaseJavaName { get; init; }

	/// <summary>
	/// JNI names of Java interfaces this type implements, e.g., ["android/view/View$OnClickListener"].
	/// Needed by JCW Java source generation ("implements" clause).
	/// </summary>
	public IReadOnlyList<string> ImplementedInterfaceJavaNames { get; init; } = Array.Empty<string> ();

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
	/// May be set after scanning when the manifest references a type
	/// that the scanner did not mark as unconditional.
	/// </summary>
	public bool IsUnconditional { get; set; }

	/// <summary>
	/// True for Application and Instrumentation types. These types cannot call
	/// <c>registerNatives</c> in their static initializer because the native library
	/// (<c>libmonodroid.so</c>) is not loaded until after the Application class is instantiated.
	/// Registration is deferred to <c>ApplicationRegistration.registerApplications()</c>.
	/// </summary>
	public bool CannotRegisterInStaticConstructor { get; init; }

	/// <summary>
	/// Marshal methods: methods with [Register(name, sig, connector)], [Export], or
	/// constructor registrations ([Register(".ctor", sig, "")] / [JniConstructorSignature]).
	/// Constructors are identified by <see cref="MarshalMethodInfo.IsConstructor"/>.
	/// Ordered — the index in this list is the method's ordinal for RegisterNatives.
	/// </summary>
	public IReadOnlyList<MarshalMethodInfo> MarshalMethods { get; init; } = Array.Empty<MarshalMethodInfo> ();

	/// <summary>
	/// Java constructors to emit in the JCW .java file.
	/// Each has a JNI signature and an ordinal index for the nctor_N native method.
	/// </summary>
	public IReadOnlyList<JavaConstructorInfo> JavaConstructors { get; init; } = [];

	/// <summary>
	/// Java fields from [ExportField] attributes.
	/// Each field is initialized by calling the annotated method.
	/// </summary>
	public IReadOnlyList<JavaFieldInfo> JavaFields { get; init; } = [];

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

	/// <summary>
	/// Android component attribute data ([Activity], [Service], [BroadcastReceiver], [ContentProvider],
	/// [Application], [Instrumentation]) if present on this type. Used for manifest generation.
	/// </summary>
	public ComponentInfo? ComponentAttribute { get; init; }
}

/// <summary>
/// Describes a marshal method (a method with [Register] or [Export]) on a Java peer type.
/// Contains all data needed to generate a UCO wrapper, a JCW native declaration,
/// and a RegisterNatives call.
/// </summary>
public sealed record MarshalMethodInfo
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
	/// True if this is a constructor registration.
	/// </summary>
	public bool IsConstructor { get; init; }

	/// <summary>
	/// True if this method comes from an [Export] attribute (rather than [Register]).
	/// [Export] methods use the C# method's access modifier in the JCW Java file
	/// instead of always being "public".
	/// </summary>
	public bool IsExport { get; init; }

	/// <summary>
	/// Java access modifier for [Export] methods ("public", "protected", "private").
	/// Null for [Register] methods (always "public").
	/// </summary>
	public string? JavaAccess { get; init; }

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

	/// <summary>
	/// True if this method was collected from an implemented interface
	/// (Pass 4: CollectInterfaceMethodImplementations), not from the type itself.
	/// </summary>
	public bool IsInterfaceImplementation { get; init; }
}

/// <summary>
/// Describes a JNI parameter for UCO method generation.
/// </summary>
public sealed record JniParameterInfo
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
/// Describes a Java constructor to emit in the JCW .java source file.
/// </summary>
public sealed record JavaConstructorInfo
{
	/// <summary>
	/// JNI constructor signature, e.g., "(Landroid/content/Context;)V".
	/// </summary>
	public required string JniSignature { get; init; }

	/// <summary>
	/// Ordinal index for the native constructor method (nctor_0, nctor_1, ...).
	/// </summary>
	public required int ConstructorIndex { get; init; }

	/// <summary>
	/// For [Export] constructors: super constructor arguments string.
	/// Null for [Register] constructors.
	/// </summary>
	public string? SuperArgumentsString { get; init; }
}

/// <summary>
/// Describes a Java field from an [ExportField] attribute.
/// The field is initialized by calling the annotated method.
/// </summary>
public sealed record JavaFieldInfo
{
	/// <summary>
	/// Java field name, e.g., "STATIC_INSTANCE".
	/// </summary>
	public required string FieldName { get; init; }

	/// <summary>
	/// Java type name for the field, e.g., "java.lang.String".
	/// </summary>
	public required string JavaTypeName { get; init; }

	/// <summary>
	/// Name of the method that initializes this field, e.g., "GetInstance".
	/// </summary>
	public required string InitializerMethodName { get; init; }

	/// <summary>
	/// Java access modifier ("public", "protected", "private").
	/// </summary>
	public required string Visibility { get; init; }

	/// <summary>
	/// Whether the field is static.
	/// </summary>
	public bool IsStatic { get; init; }
}

/// <summary>
/// Describes how to call the activation constructor for a Java peer type.
/// </summary>
public sealed record ActivationCtorInfo
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

public enum ActivationCtorStyle
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

/// <summary>
/// The kind of Android component (Activity, Service, etc.).
/// </summary>
public enum ComponentKind
{
	Activity,
	Service,
	BroadcastReceiver,
	ContentProvider,
	Application,
	Instrumentation,
}

/// <summary>
/// Describes an Android component attribute ([Activity], [Service], etc.) on a Java peer type.
/// All named property values from the attribute are stored in <see cref="Properties"/>.
/// </summary>
public sealed record ComponentInfo
{
	/// <summary>
	/// The kind of component.
	/// </summary>
	public required ComponentKind Kind { get; init; }

	/// <summary>
	/// All named property values from the component attribute.
	/// Keys are property names (e.g., "Label", "Exported", "MainLauncher").
	/// Values are the raw decoded values (string, bool, int for enums, etc.).
	/// </summary>
	public IReadOnlyDictionary<string, object?> Properties { get; init; } = new Dictionary<string, object?> ();

	/// <summary>
	/// Intent filters declared on this component via [IntentFilter] attributes.
	/// </summary>
	public IReadOnlyList<IntentFilterInfo> IntentFilters { get; init; } = [];

	/// <summary>
	/// Metadata entries declared on this component via [MetaData] attributes.
	/// </summary>
	public IReadOnlyList<MetaDataInfo> MetaData { get; init; } = [];

	/// <summary>
	/// Whether the component type has a public parameterless constructor.
	/// Required for manifest inclusion — XA4213 error if missing.
	/// </summary>
	public bool HasPublicDefaultConstructor { get; init; }
}

/// <summary>
/// Describes an [IntentFilter] attribute on a component type.
/// </summary>
public sealed record IntentFilterInfo
{
	/// <summary>
	/// Action names from the first constructor argument (string[]).
	/// </summary>
	public IReadOnlyList<string> Actions { get; init; } = [];

	/// <summary>
	/// Category names.
	/// </summary>
	public IReadOnlyList<string> Categories { get; init; } = [];

	/// <summary>
	/// Named properties (DataScheme, DataHost, DataPath, Label, Icon, Priority, etc.).
	/// </summary>
	public IReadOnlyDictionary<string, object?> Properties { get; init; } = new Dictionary<string, object?> ();
}

/// <summary>
/// Describes a [MetaData] attribute on a component type.
/// </summary>
public sealed record MetaDataInfo
{
	/// <summary>
	/// The metadata name (first constructor argument).
	/// </summary>
	public required string Name { get; init; }

	/// <summary>
	/// The Value property, if set.
	/// </summary>
	public string? Value { get; init; }

	/// <summary>
	/// The Resource property, if set.
	/// </summary>
	public string? Resource { get; init; }
}

/// <summary>
/// Assembly-level manifest attributes collected from all scanned assemblies.
/// Aggregated across assemblies — used to generate top-level manifest elements
/// like <![CDATA[<uses-permission>]]>, <![CDATA[<uses-feature>]]>, etc.
/// </summary>
public sealed class AssemblyManifestInfo
{
	public List<PermissionInfo> Permissions { get; } = [];
	public List<PermissionGroupInfo> PermissionGroups { get; } = [];
	public List<PermissionTreeInfo> PermissionTrees { get; } = [];
	public List<UsesPermissionInfo> UsesPermissions { get; } = [];
	public List<UsesFeatureInfo> UsesFeatures { get; } = [];
	public List<UsesLibraryInfo> UsesLibraries { get; } = [];
	public List<UsesConfigurationInfo> UsesConfigurations { get; } = [];
	public List<MetaDataInfo> MetaData { get; } = [];
	public List<PropertyInfo> Properties { get; } = [];

	/// <summary>
	/// Assembly-level [Application] attribute properties (merged from all assemblies).
	/// Null if no assembly-level [Application] attribute was found.
	/// </summary>
	public Dictionary<string, object?>? ApplicationProperties { get; set; }
}

public sealed record PermissionInfo
{
	public required string Name { get; init; }
	public IReadOnlyDictionary<string, object?> Properties { get; init; } = new Dictionary<string, object?> ();
}

public sealed record PermissionGroupInfo
{
	public required string Name { get; init; }
	public IReadOnlyDictionary<string, object?> Properties { get; init; } = new Dictionary<string, object?> ();
}

public sealed record PermissionTreeInfo
{
	public required string Name { get; init; }
	public IReadOnlyDictionary<string, object?> Properties { get; init; } = new Dictionary<string, object?> ();
}

public sealed record UsesPermissionInfo
{
	public required string Name { get; init; }
	public int? MaxSdkVersion { get; init; }
}

public sealed record UsesFeatureInfo
{
	/// <summary>
	/// Feature name (e.g., "android.hardware.camera"). Null for GL ES version features.
	/// </summary>
	public string? Name { get; init; }

	/// <summary>
	/// OpenGL ES version (e.g., 0x00020000 for 2.0). Zero for named features.
	/// </summary>
	public int GLESVersion { get; init; }

	public bool Required { get; init; } = true;
}

public sealed record UsesLibraryInfo
{
	public required string Name { get; init; }
	public bool Required { get; init; } = true;
}

public sealed record UsesConfigurationInfo
{
	public bool ReqFiveWayNav { get; init; }
	public bool ReqHardKeyboard { get; init; }
	public string? ReqKeyboardType { get; init; }
	public string? ReqNavigation { get; init; }
	public string? ReqTouchScreen { get; init; }
}

public sealed record PropertyInfo
{
	public required string Name { get; init; }
	public string? Value { get; init; }
	public string? Resource { get; init; }
}
