namespace Microsoft.Android.Sdk.TrimmableTypeMap;

internal sealed record UsesFeatureInfo
{
	public string? Name { get; init; }
	public int GLESVersion { get; init; }
	public bool Required { get; init; } = true;
}
