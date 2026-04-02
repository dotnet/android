namespace Microsoft.Android.Sdk.TrimmableTypeMap;

internal sealed record UsesPermissionInfo
{
	public required string Name { get; init; }
	public int? MaxSdkVersion { get; init; }
	public string? UsesPermissionFlags { get; init; }
}
