#nullable enable

using System;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks
{
	public class CheckGoogleSdkRequirements : AndroidTask 
	{
		public override string TaskPrefix => "CGS";

		[Required]
		public string AndroidApiLevel { get; set; } = "";

		[Required]
		public string ManifestFile { get; set; } = "";

		public override bool RunTask ()
		{
			ManifestDocument manifest = new ManifestDocument (ManifestFile);

			int? compileSdk = null;

			if (MonoAndroidHelper.TryParseApiLevel (AndroidApiLevel, out Version version)) {
				compileSdk = version.Major;
			}

			if (!int.TryParse (manifest.GetMinimumSdk (), out int minSdk)) {
				minSdk = 1;
			}
			if (!int.TryParse (manifest.GetTargetSdk (), out int targetSdk)) {
				// 21 is minimum supported for .NET 6+, but should be better than putting 1 here.
				targetSdk = compileSdk ?? 21;
			}

			//We should throw a warning if the targetSdkVersion is lower than compileSdkVersion(TargetFrameworkVersion).
			if (compileSdk.HasValue && targetSdk < compileSdk) {
				Log.LogCodedWarning ("XA1006", Properties.Resources.XA1006, compileSdk.Value, targetSdk);
			}
			//We should throw an warning if the compileSdkVersion(TargetFrameworkVersion) is lower than targetSdkVersion.
			if (compileSdk.HasValue && compileSdk < targetSdk) {
				Log.LogCodedWarning ("XA1008", Properties.Resources.XA1008, compileSdk.Value, targetSdk);
			}
			//We should throw an warning if the minSdkVersion is greater than targetSdkVers1ion.
			if (minSdk > targetSdk) {
				Log.LogCodedWarning ("XA1007", Properties.Resources.XA1007, minSdk, targetSdk);
			}

			return !Log.HasLoggedErrors;
		}
	}
}
