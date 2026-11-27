namespace Microsoft.Android.Sdk.TrimmableTypeMap;

public sealed record GrantUriPermissionInfo
{
	public string? Path { get; init; }
	public string? PathPattern { get; init; }
	public string? PathPrefix { get; init; }
}
