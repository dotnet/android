using System.Collections.Generic;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

public enum ComponentKind
{
	Activity,
	Service,
	BroadcastReceiver,
	ContentProvider,
	Application,
	Instrumentation,
}

public sealed record ComponentInfo
{
	public required ComponentKind Kind { get; init; }
	public IReadOnlyDictionary<string, object?> Properties { get; init; } = new Dictionary<string, object?> ();
	public IReadOnlyList<IntentFilterInfo> IntentFilters { get; init; } = [];
	public IReadOnlyList<MetaDataInfo> MetaData { get; init; } = [];
}
