namespace Microsoft.Android.Sdk.TrimmableTypeMap;

internal sealed record UsesConfigurationInfo
{
	public bool ReqFiveWayNav { get; init; }
	public bool ReqHardKeyboard { get; init; }
	public string? ReqKeyboardType { get; init; }
	public string? ReqNavigation { get; init; }
	public string? ReqTouchScreen { get; init; }
}
