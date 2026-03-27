
using System.Collections.Generic;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

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
