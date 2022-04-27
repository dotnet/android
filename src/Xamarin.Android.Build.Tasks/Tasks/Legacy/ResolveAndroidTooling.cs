using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks.Legacy
{
	/// <summary>
	/// ResolveAndroidTooling does lot of the grunt work ResolveSdks used to do:
	/// - Modify TargetFrameworkVersion
	/// - Calculate ApiLevel and ApiLevelName
	/// - Find the paths of various Android tooling that other tasks need to call
	/// </summary>
	public class ResolveAndroidTooling : Xamarin.Android.Tasks.ResolveAndroidTooling
	{
		public bool UseLatestAndroidPlatformSdk { get; set; }

		[Output]
		public string TargetFrameworkVersion { get; set; }

		protected override bool Validate ()
		{
			if (!ValidateApiLevels ())
				return false;

			if (!MonoAndroidHelper.SupportedVersions.FrameworkDirectories.Any (p => Directory.Exists (Path.Combine (p, TargetFrameworkVersion)))) {
				Log.LogError (
					subcategory: string.Empty,
					errorCode: "XA0001",
					helpKeyword: string.Empty,
					file: ProjectFilePath,
					lineNumber: 0,
					columnNumber: 0,
					endLineNumber: 0,
					endColumnNumber: 0,
					message: Properties.Resources.XA0001,
					messageArgs: new [] {
						TargetFrameworkVersion,
					}
				);
				return false;
			}

			int apiLevel;
			if (AndroidApplication && int.TryParse (AndroidApiLevel, out apiLevel)) {
				if (apiLevel < 30)
					Log.LogCodedWarning ("XA0113", Properties.Resources.XA0113, "v11.0", "30", TargetFrameworkVersion, AndroidApiLevel);
				if (apiLevel < 21)
					Log.LogCodedWarning ("XA0117", Properties.Resources.XA0117, TargetFrameworkVersion);
			}

			return true;
		}

		protected override void LogOutputs ()
		{
			base.LogOutputs ();

			Log.LogDebugMessage ($"  {nameof (TargetFrameworkVersion)}: {TargetFrameworkVersion}");
		}

		bool ValidateApiLevels ()
		{
			// Priority:
			//    $(UseLatestAndroidPlatformSdk) > $(AndroidApiLevel) > $(TargetFrameworkVersion)
			//
			// If $(TargetFrameworkVersion) isn't set, and $(AndroidApiLevel) isn't
			// set, act as if $(UseLatestAndroidPlatformSdk) is True
			//
			// If $(UseLatestAndroidPlatformSdk) is true, we do as it says: use the
			// latest installed version.
			//
			// Otherwise, if $(AndroidApiLevel) is set, use it and set $(TargetFrameworkVersion).
			//    Rationale: monodroid/samples/xbuild.make uses $(AndroidApiLevel)
			//    to build for a specific API level.
			// Otherwise, if $(TargetFrameworkVersion) is set, use it and set $(AndroidApiLevel).

			UseLatestAndroidPlatformSdk = UseLatestAndroidPlatformSdk ||
				(string.IsNullOrWhiteSpace (AndroidApiLevel) && string.IsNullOrWhiteSpace (TargetFrameworkVersion));

			if (UseLatestAndroidPlatformSdk) {
				int maxInstalled = GetMaxInstalledApiLevel ();
				int maxSupported = GetMaxStableApiLevel ();
				AndroidApiLevel = maxInstalled.ToString ();
				if (maxInstalled > maxSupported) {
					Log.LogDebugMessage ($"API Level {maxInstalled} is greater than the maximum supported API level of {maxSupported}. " +
						"Support for this API will be added in a future release.");
				}
				if (!string.IsNullOrWhiteSpace (TargetFrameworkVersion)) {
					var userSelected = MonoAndroidHelper.SupportedVersions.GetApiLevelFromFrameworkVersion (TargetFrameworkVersion);
					// overwrite using user version only if it is 
					// above the maxStableApi and a valid apiLevel.
					if (userSelected != null && userSelected > maxSupported && userSelected <= maxInstalled) {
						maxInstalled =
							maxSupported = userSelected.Value;
						AndroidApiLevel = userSelected.ToString ();
					}
				}

				for (int apiLevel = maxSupported; apiLevel >= MonoAndroidHelper.SupportedVersions.MinStableVersion.ApiLevel; apiLevel--) {
					var id = MonoAndroidHelper.SupportedVersions.GetIdFromApiLevel (apiLevel);
					var apiPlatformDir = MonoAndroidHelper.AndroidSdk.TryGetPlatformDirectoryFromApiLevel (id, MonoAndroidHelper.SupportedVersions);
					if (apiPlatformDir != null && Directory.Exists (apiPlatformDir)) {
						var targetFramework = MonoAndroidHelper.SupportedVersions.GetFrameworkVersionFromId (id);
						if (targetFramework != null && MonoAndroidHelper.SupportedVersions.InstalledBindingVersions.Any (b => b.FrameworkVersion == targetFramework)) {
							AndroidApiLevel = apiLevel.ToString ();
							TargetFrameworkVersion = targetFramework;
							break;
						}
					}
				}
				return TargetFrameworkVersion != null;
			}

			if (!string.IsNullOrWhiteSpace (TargetFrameworkVersion)) {
				TargetFrameworkVersion = TargetFrameworkVersion.Trim ();
				string id = MonoAndroidHelper.SupportedVersions.GetIdFromFrameworkVersion (TargetFrameworkVersion);
				if (id == null) {
					Log.LogCodedError ("XA0000", Properties.Resources.XA0000_API_for_TargetFrameworkVersion, TargetFrameworkVersion);
					return false;
				}
				AndroidApiLevel = MonoAndroidHelper.SupportedVersions.GetApiLevelFromId (id).ToString ();
				return true;
			}

			if (!string.IsNullOrWhiteSpace (AndroidApiLevel)) {
				AndroidApiLevel = AndroidApiLevel.Trim ();
				TargetFrameworkVersion = GetTargetFrameworkVersionFromApiLevel ();
				return TargetFrameworkVersion != null;
			}

			Log.LogCodedError ("XA0000", Properties.Resources.XA0000_API_or_TargetFrameworkVersion_Fail);
			return false;
		}


		int GetMaxInstalledApiLevel ()
		{
			int maxApiLevel = int.MinValue;
			string platformsDir = Path.Combine (AndroidSdkPath, "platforms");
			if (Directory.Exists (platformsDir)) {
				var apiIds = Directory.EnumerateDirectories (platformsDir)
					.Select (platformDir => Path.GetFileName (platformDir))
					.Where (dir => dir.StartsWith ("android-", StringComparison.OrdinalIgnoreCase))
					.Select (dir => dir.Substring ("android-".Length))
					.Select (apiName => MonoAndroidHelper.SupportedVersions.GetIdFromApiLevel (apiName));
				foreach (var id in apiIds) {
					int? v = MonoAndroidHelper.SupportedVersions.GetApiLevelFromId (id);
					if (!v.HasValue)
						continue;
					maxApiLevel = Math.Max (maxApiLevel, v.Value);
				}
			}
			if (maxApiLevel < 0)
				Log.LogCodedError ("XA5300", Properties.Resources.XA5300_Android_Platforms,
						platformsDir, AndroidSdkPath, Path.DirectorySeparatorChar, Android);
			return maxApiLevel;
		}

		string GetTargetFrameworkVersionFromApiLevel ()
		{
			string targetFramework = MonoAndroidHelper.SupportedVersions.GetFrameworkVersionFromId (AndroidApiLevel);
			if (targetFramework != null)
				return targetFramework;
			Log.LogCodedError ("XA0000", Properties.Resources.XA0000_TargetFrameworkVersion_for_API, AndroidApiLevel);
			return null;
		}
	}
}
