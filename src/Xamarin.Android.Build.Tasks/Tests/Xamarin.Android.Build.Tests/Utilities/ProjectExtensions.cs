using System;

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
		DoSetRuntime (project, runtime);
		EnablePreviewFeaturesIfNeeded (project, runtime);
	}

	public static void SetRuntime (this XamarinAndroidApplicationProject project, AndroidRuntime runtime)
	{
		if (runtime != AndroidRuntime.NativeAOT) {
			DoSetRuntime (project, runtime);
			return;
		}
		project.SetPublishAot (true);
		project.SetProperty ("_SkipNdkResolution", "true");
		EnablePreviewFeaturesIfNeeded (project, runtime);
	}

	static void EnablePreviewFeaturesIfNeeded (XamarinProject project, AndroidRuntime runtime)
	{
		if (runtime != AndroidRuntime.NativeAOT) {
			return;
		}

		project.SetProperty ("EnablePreviewFeatures", "true");
	}

	static void DoSetRuntime (XamarinProject project, AndroidRuntime runtime)
	{
		switch (runtime) {
			case AndroidRuntime.CoreCLR:
				project.SetProperty ("UseMonoRuntime", "false");
				break;
			case AndroidRuntime.NativeAOT:
				project.SetProperty ("PublishAot", "true");
				break;
			case AndroidRuntime.MonoVM:
				project.SetProperty ("UseMonoRuntime", "true");
				break;

			default:
				throw new NotSupportedException ($"Unsupported runtime {runtime}");
		}
	}
}
