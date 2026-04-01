namespace Microsoft.Android.Sdk.TrimmableTypeMap;

internal sealed record PropertyInfo
{
	public required string Name { get; init; }
	public string? Value { get; init; }
	public string? Resource { get; init; }
}
