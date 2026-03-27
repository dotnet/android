using System.Collections.Generic;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

internal sealed record PermissionTreeInfo
{
	public required string Name { get; init; }
	public IReadOnlyDictionary<string, object?> Properties { get; init; } = new Dictionary<string, object?> ();
}
