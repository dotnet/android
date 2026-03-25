#nullable enable

using System.Collections.Generic;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

public sealed record PermissionGroupInfo
{
	public required string Name { get; init; }
	public IReadOnlyDictionary<string, object?> Properties { get; init; } = new Dictionary<string, object?> ();
}
