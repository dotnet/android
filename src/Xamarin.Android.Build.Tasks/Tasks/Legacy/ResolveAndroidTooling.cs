#nullable enable

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
		[Output]
		public string? TargetFrameworkVersion { get; set; }

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
			if (!TargetFrameworkVersion.IsNullOrWhiteSpace ()) {
				TargetFrameworkVersion = TargetFrameworkVersion.Trim ();
				string? id = MonoAndroidHelper.SupportedVersions.GetIdFromFrameworkVersion (TargetFrameworkVersion);
				if (id == null) {
					Log.LogCodedError ("XA0000", Properties.Resources.XA0000_API_for_TargetFrameworkVersion, TargetFrameworkVersion);
					return false;
				}
				AndroidApiLevel = MonoAndroidHelper.SupportedVersions.GetApiLevelFromId (id).ToString ();
				return true;
			}

			if (!AndroidApiLevel.IsNullOrWhiteSpace ()) {
				AndroidApiLevel = AndroidApiLevel.Trim ();
				TargetFrameworkVersion = GetTargetFrameworkVersionFromApiLevel ();
				return TargetFrameworkVersion != null;
			}

			Log.LogCodedError ("XA0000", Properties.Resources.XA0000_API_or_TargetFrameworkVersion_Fail);
			return false;
		}


		string? GetTargetFrameworkVersionFromApiLevel ()
		{
			if (AndroidApiLevel == null)
				return null;
			string? targetFramework = MonoAndroidHelper.SupportedVersions.GetFrameworkVersionFromId (AndroidApiLevel);
			if (targetFramework != null)
				return targetFramework;
			Log.LogCodedError ("XA0000", Properties.Resources.XA0000_TargetFrameworkVersion_for_API, AndroidApiLevel);
			return null;
		}
	}
}
