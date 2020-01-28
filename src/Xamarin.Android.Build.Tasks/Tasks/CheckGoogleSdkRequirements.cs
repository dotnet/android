using System;
using System.Collections.Generic;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.IO;
using System.Linq;

using Java.Interop.Tools.Cecil;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	public class CheckGoogleSdkRequirements : AndroidTask 
	{
		public override string TaskPrefix => "CGS";

		[Required]
		public string TargetFrameworkVersion { get; set; }

		[Required]
		public string ManifestFile { get; set; }

		public override bool RunTask ()
		{
			ManifestDocument manifest = new ManifestDocument (ManifestFile, this.Log);

			var compileSdk = MonoAndroidHelper.SupportedVersions.GetApiLevelFromFrameworkVersion (TargetFrameworkVersion);

			if (!int.TryParse (manifest.GetMinimumSdk (), out int minSdk)) {
				minSdk = 1;
			}
			if (!int.TryParse (manifest.GetTargetSdk (), out int targetSdk)) {
				targetSdk = compileSdk.Value;
			}

			//We should throw a warning if the targetSdkVersion is lower than compileSdkVersion(TargetFrameworkVersion).
			if (targetSdk < compileSdk) {
				Log.LogCodedWarning ("XA1006", Properties.Resources.XA1006, compileSdk, targetSdk);
			}
			//We should throw an warning if the compileSdkVersion(TargetFrameworkVersion) is lower than targetSdkVersion.
			if (compileSdk < targetSdk) {
				Log.LogCodedWarning ("XA1008", Properties.Resources.XA1008, compileSdk, targetSdk);
			}
			//We should throw an warning if the minSdkVersion is greater than targetSdkVers1ion.
			if (minSdk > targetSdk) {
				Log.LogCodedWarning ("XA1007", Properties.Resources.XA1007, minSdk, targetSdk);
			}

			return !Log.HasLoggedErrors;
		}
	}
}