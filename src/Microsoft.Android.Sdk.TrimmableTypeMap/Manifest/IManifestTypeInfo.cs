using System.Collections.Generic;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Component kind for manifest generation.
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
/// </summary>
sealed class ComponentAttributeInfo
{
	/// <summary>
	/// Full attribute type name, e.g., "Android.App.ActivityAttribute".
	/// </summary>
	public string AttributeType { get; set; } = "";

	/// <summary>
	/// Named property values, e.g., { "MainLauncher": true, "Label": "My App" }.
	/// Values are decoded .NET types (string, bool, int, Type name as string, enum as int).
	/// </summary>
	public IReadOnlyDictionary<string, object> Properties { get; set; } = new Dictionary<string, object> ();

	/// <summary>
	/// Constructor arguments (positional), e.g., ContentProviderAttribute(string[] authorities).
	/// </summary>
	public IReadOnlyList<object> ConstructorArguments { get; set; } = [];
}

/// <summary>
/// Abstraction over a Java peer type for manifest generation.
/// Implemented by both Cecil (legacy) and SRM (trimmable) adapters.
/// Contains all data ManifestDocument.Merge() needs without Cecil/SRM dependency.
/// </summary>
interface IManifestTypeInfo
{
	string FullName { get; }
	string Namespace { get; }

	/// <summary>Java type name in dotted format, e.g., "my.app.MainActivity".</summary>
	string JavaName { get; }

	/// <summary>Compat Java name (e.g., with md5 hash for auto-generated names).</summary>
	string CompatJavaName { get; }

	bool IsAbstract { get; }

	/// <summary>Required for component types — XA4213 if missing.</summary>
	bool HasPublicParameterlessConstructor { get; }

	ManifestComponentKind ComponentKind { get; }

	/// <summary>Raw data for the primary component attribute. Null if ComponentKind is None.</summary>
	ComponentAttributeInfo? ComponentAttribute { get; }

	IReadOnlyList<ComponentAttributeInfo> IntentFilters { get; }
	IReadOnlyList<ComponentAttributeInfo> MetaDataEntries { get; }
	IReadOnlyList<ComponentAttributeInfo> PropertyAttributes { get; }
	ComponentAttributeInfo? LayoutAttribute { get; }
	IReadOnlyList<ComponentAttributeInfo> GrantUriPermissions { get; }
}
