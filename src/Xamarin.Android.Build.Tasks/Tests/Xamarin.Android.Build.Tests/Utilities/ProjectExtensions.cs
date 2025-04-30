using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests;

public static class ProjectExtensions
{
	/// <summary>
	/// Sets the appropriate MSBuild property to use a specific .NET runtime.
	/// NOTE: $(EnablePreviewFeatures) ignores warning XA1040: The CoreCLR/NativeAOT runtime on Android is an experimental feature and not yet suitable for production use.
	/// </summary>
	public static void SetRuntime (this XamarinProject project, AndroidRuntime runtime)
	{
		switch (runtime) {
			case AndroidRuntime.CoreCLR:
				project.SetProperty ("UseMonoRuntime", "false");
				project.SetProperty ("EnablePreviewFeatures", "true");
				break;
			case AndroidRuntime.NativeAOT:
				project.SetProperty ("PublishAot", "true");
				project.SetProperty ("EnablePreviewFeatures", "true");
				break;
			default:
				// MonoVM or default can just use default settings
				break;
		}
	}
}
