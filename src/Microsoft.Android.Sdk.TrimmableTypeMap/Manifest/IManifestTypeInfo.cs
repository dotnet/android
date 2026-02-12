using System.Collections.Generic;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Component kind for manifest generation.
/// Determines which XML element is generated in the merged AndroidManifest.xml.
/// </summary>
enum ManifestComponentKind
{
	None,
	Activity,
	Service,
	BroadcastReceiver,
	ContentProvider,
	Application,
	Instrumentation,
}

/// <summary>
/// Holds raw attribute property data extracted from either Cecil or SRM.
/// The consumer (ManifestDocument) creates strongly-typed attribute objects from this data.
/// </summary>
sealed class ComponentAttributeInfo
{
	/// <summary>
	/// Full attribute type name, e.g., "Android.App.ActivityAttribute".
	/// </summary>
	public string AttributeType { get; set; } = "";

	/// <summary>
	/// Named property values from the attribute, e.g., { "MainLauncher": true, "Label": "My App" }.
	/// Values are already decoded to their .NET types (string, bool, int, Type name as string, enum as int).
	/// </summary>
	public IReadOnlyDictionary<string, object> Properties { get; set; } = new Dictionary<string, object> ();

	/// <summary>
	/// Constructor arguments (positional), e.g., for ContentProviderAttribute(string[] authorities).
	/// Values are already decoded.
	/// </summary>
	public IReadOnlyList<object> ConstructorArguments { get; set; } = [];
}

/// <summary>
/// Abstraction over a Java peer type for manifest generation.
/// Implemented by both Cecil (legacy) and SRM (trimmable) adapters.
/// This interface contains all data ManifestDocument.Merge() needs,
/// without any dependency on Cecil or SRM.
/// </summary>
interface IManifestTypeInfo
{
	/// <summary>
	/// Full managed type name, e.g., "MyApp.MainActivity".
	/// </summary>
	string FullName { get; }

	/// <summary>
	/// Managed type namespace, e.g., "MyApp".
	/// </summary>
	string Namespace { get; }

	/// <summary>
	/// Java type name in dotted format for manifest, e.g., "my.app.MainActivity".
	/// Derived from JNI name by replacing '/' with '.'.
	/// </summary>
	string JavaName { get; }

	/// <summary>
	/// Compatibility Java name (e.g., with md5 hash for auto-generated names).
	/// Used for backward-compatible manifest entries.
	/// </summary>
	string CompatJavaName { get; }

	/// <summary>
	/// Whether this is an abstract type. Abstract types are skipped during manifest generation.
	/// </summary>
	bool IsAbstract { get; }

	/// <summary>
	/// Whether this type has a public parameterless constructor.
	/// Required for component types (Activity, Service, etc.) — XA4213 if missing.
	/// </summary>
	bool HasPublicParameterlessConstructor { get; }

	/// <summary>
	/// The kind of Android manifest component this type represents.
	/// Determined by base class: Activity, Service, BroadcastReceiver, ContentProvider,
	/// Application, Instrumentation, or None for non-component types.
	/// </summary>
	ManifestComponentKind ComponentKind { get; }

	/// <summary>
	/// Raw property data for the primary component attribute (e.g., [Activity], [Service]).
	/// Null if ComponentKind is None.
	/// </summary>
	ComponentAttributeInfo? ComponentAttribute { get; }

	/// <summary>
	/// Intent filter attributes applied to this type.
	/// Each entry contains the raw property data for one [IntentFilter] attribute.
	/// </summary>
	IReadOnlyList<ComponentAttributeInfo> IntentFilters { get; }

	/// <summary>
	/// Metadata attributes applied to this type.
	/// Each entry contains the raw property data for one [MetaData] attribute.
	/// </summary>
	IReadOnlyList<ComponentAttributeInfo> MetaDataEntries { get; }

	/// <summary>
	/// Property attributes applied to this type.
	/// Each entry contains the raw property data for one [Property] attribute.
	/// </summary>
	IReadOnlyList<ComponentAttributeInfo> PropertyAttributes { get; }

	/// <summary>
	/// Layout attribute data, if present. Used only for Activity types.
	/// </summary>
	ComponentAttributeInfo? LayoutAttribute { get; }

	/// <summary>
	/// Grant URI permission attributes. Used only for ContentProvider types.
	/// </summary>
	IReadOnlyList<ComponentAttributeInfo> GrantUriPermissions { get; }
}
