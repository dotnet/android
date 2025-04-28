using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests;

public static class ProjectExtensions
{
	/// <summary>
	/// Sets the appropriate MSBuild property to use a specific .NET runtime.
	/// </summary>
	public static void SetRuntime (this XamarinProject project, AndroidRuntime runtime)
	{
		switch (runtime) {
			case AndroidRuntime.CoreCLR:
				project.SetProperty ("UseMonoRuntime", "false");
				break;
			case AndroidRuntime.NativeAOT:
				project.SetProperty ("PublishAot", "true");
				break;
			default:
				// MonoVM or default can just use default settings
				break;
		}
	}
}
