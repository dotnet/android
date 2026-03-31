namespace Microsoft.Android.Sdk.TrimmableTypeMap;

public sealed record MetaDataInfo
{
	public required string Name { get; init; }
	public string? Value { get; init; }
	public string? Resource { get; init; }
}
