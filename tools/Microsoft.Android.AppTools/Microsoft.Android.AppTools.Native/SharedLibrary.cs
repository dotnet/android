using Xamarin.Android.Tools;

namespace Microsoft.Android.AppTools;

public class SharedLibrary
{
	public SharedLibraryKind Kind { get; private set; } = SharedLibraryKind.Other;
	public AndroidTargetArch TargetArchitecture { get; private set; } = AndroidTargetArch.None;
}
