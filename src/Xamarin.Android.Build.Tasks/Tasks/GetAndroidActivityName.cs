#nullable enable
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	[MSBuildMultiThreadableTask]
	public class GetAndroidActivityName : MultithreadedAndroidTask
	{
		public override string TaskPrefix => "GAAN";

		[Required]
		public string ManifestFile { get; set; } = "";

		public string []? ReferenceAssemblyPaths { get; set; }

		[Output]
		public string? ActivityName { get; set; }

		public override bool RunTask ()
		{
			var manifest = AndroidAppManifest.Load (TaskEnvironment.GetAbsolutePath (ManifestFile),
				MonoAndroidHelper.GetSupportedVersions (BuildEngine4, ReferenceAssemblyPaths, TaskEnvironment));

			ActivityName = manifest.GetLaunchableUserActivityName ();

			return !Log.HasLoggedErrors;
		}
	}
}
