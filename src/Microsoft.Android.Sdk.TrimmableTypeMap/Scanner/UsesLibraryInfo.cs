namespace Microsoft.Android.Sdk.TrimmableTypeMap;

internal sealed record UsesLibraryInfo
{
	public required string Name { get; init; }
	public bool Required { get; init; } = true;
}
