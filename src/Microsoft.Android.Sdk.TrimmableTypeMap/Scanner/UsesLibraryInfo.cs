#nullable enable

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

public sealed record UsesLibraryInfo
{
	public required string Name { get; init; }
	public bool Required { get; init; } = true;
}
