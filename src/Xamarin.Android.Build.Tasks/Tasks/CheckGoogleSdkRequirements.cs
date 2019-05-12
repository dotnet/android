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
	public class CheckGoogleSdkRequirements : Task 
	{
		[Required]
		public string TargetFrameworkVersion { get; set; }

		[Required]
		public string ManifestFile { get; set; }

		public override bool Execute ()
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
				Log.LogCodedWarning ("XA1006",
					$"You are building against version of Android ({compileSdk}) that is more recent than your targetSdkVersion specifies ({targetSdk}). Set your targetSdkVersion to the highest version of Android available to match your TargetFrameworkVersion ({compileSdk}).");
			}
			//We should throw an warning if the compileSdkVersion(TargetFrameworkVersion) is lower than targetSdkVersion.
			if (compileSdk < targetSdk) {
				Log.LogCodedWarning ("XA1008",
					$"The TargetFrameworkVersion ({compileSdk}) must not be lower than targetSdkVersion ({targetSdk}). You should either, increase the `$(TargetFrameworkVersion)` of your project. Or decrease the `android:targetSdkVersion` in your `AndroidManifest.xml` to correct this issue.");
			}
			//We should throw an warning if the minSdkVersion is greater than targetSdkVers1ion.
			if (minSdk > targetSdk) {
				Log.LogCodedWarning ("XA1007",
					$"The minSdkVersion ({minSdk}) is greater than targetSdkVersion. Please change the value such that minSdkVersion is less than or equal to targetSdkVersion ({targetSdk}).");
			}

			return !Log.HasLoggedErrors;
		}
	}
}