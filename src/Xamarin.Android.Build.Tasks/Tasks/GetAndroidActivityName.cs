using Microsoft.Build.Framework;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	public class GetAndroidActivityName : AndroidTask
	{
		public override string TaskPrefix => "GAAN";

		[Required]
		public string ManifestFile { get; set; }

		[Output]
		public string ActivityName { get; set; }

		public override bool RunTask ()
		{
			var manifest = AndroidAppManifest.Load (ManifestFile, MonoAndroidHelper.SupportedVersions);

			ActivityName = manifest.GetLaunchableUserActivityName ();

			return !Log.HasLoggedErrors;
		}
	}
}
