using System.Collections.Generic;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

public sealed record IntentFilterInfo
{
	public IReadOnlyList<string> Actions { get; init; } = [];
	public IReadOnlyList<string> Categories { get; init; } = [];
	public IReadOnlyDictionary<string, object?> Properties { get; init; } = new Dictionary<string, object?> ();
}
