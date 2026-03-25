#nullable enable

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

public sealed record UsesPermissionInfo
{
	public required string Name { get; init; }
	public int? MaxSdkVersion { get; init; }
}
