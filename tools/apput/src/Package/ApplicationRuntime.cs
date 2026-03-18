namespace ApplicationUtility;

/// <summary>
/// Identifies the .NET runtime used by an Android application.
/// </summary>
public enum ApplicationRuntime
{
	Unknown,
	MonoVM,
	CoreCLR,
	StaticCoreCLR,
	NativeAOT,
}
