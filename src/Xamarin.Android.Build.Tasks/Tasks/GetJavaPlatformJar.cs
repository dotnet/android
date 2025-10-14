// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

#nullable enable

using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Xamarin.Android.Tools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class GetJavaPlatformJar : AndroidTask
	{
		public override string TaskPrefix => "GJP";

		private XNamespace androidNs = "http://schemas.android.com/apk/res/android";

		[Required]
		public string AndroidApiLevel { get; set; } = "";

		public string? AndroidManifest { get; set; }

		public bool DesignTimeBuild { get; set; }

		public bool BuildingInsideVisualStudio { get; set; }

		public string? SupportedOSPlatformVersion { get; set; }

		public string? TargetFramework { get; set; }

		public string? AndroidSdkDirectory { get; set; }

		public string? TargetPlatformVersion { get; set; }

		[Output]
		public string? JavaPlatformJarPath { get; set; }

		[Output]
		public string? TargetSdkVersion { get; set; }

		public override bool RunTask ()
		{
			var platform = AndroidApiLevel;

			XAttribute? target_sdk = null;

			int supportedOsPlatformVersionAsInt = MonoAndroidHelper.ConvertSupportedOSPlatformVersionToApiLevel (SupportedOSPlatformVersion);
			if (supportedOsPlatformVersionAsInt < XABuildConfig.AndroidMinimumDotNetApiLevel.Major) {
				Log.LogCodedError ("XA4216", Properties.Resources.XA4216_SupportedOSPlatformVersion, supportedOsPlatformVersionAsInt, XABuildConfig.AndroidMinimumDotNetApiLevel);
			}

			// Look for targetSdkVersion in the user's AndroidManifest.xml
			if (!AndroidManifest.IsNullOrWhiteSpace ()) {
				if (!File.Exists (AndroidManifest)) {
					Log.LogCodedError ("XA1018", Properties.Resources.XA1018, AndroidManifest);
					return false;
				}

				try {
					var doc = XDocument.Load (AndroidManifest);
					var manifest = doc.Root;

					if (manifest != null) {
						var uses_sdk = manifest.Element ("uses-sdk");

						if (uses_sdk != null) {
							target_sdk = uses_sdk.Attribute (androidNs + "targetSdkVersion");

							if (target_sdk != null && !target_sdk.Value.IsNullOrWhiteSpace ())
								platform = target_sdk.Value;

							var min_sdk = uses_sdk.Attribute (androidNs + "minSdkVersion");
							if (min_sdk != null) {
								var failedToParseMinSdk = !int.TryParse (min_sdk.Value, out int minSdkVersion);

								if (failedToParseMinSdk || minSdkVersion < XABuildConfig.AndroidMinimumDotNetApiLevel.Major) {
									Log.LogCodedError ("XA4216", Properties.Resources.XA4216_MinSdkVersion, min_sdk?.Value, XABuildConfig.AndroidMinimumDotNetApiLevel);
								}

								if (failedToParseMinSdk || minSdkVersion != supportedOsPlatformVersionAsInt) {
									Log.LogCodedError ("XA1036", Properties.Resources.XA1036, min_sdk?.Value, SupportedOSPlatformVersion);
								}
							}
							if (target_sdk != null && (!int.TryParse (target_sdk.Value, out int targetSdkVersion) || targetSdkVersion < XABuildConfig.AndroidMinimumDotNetApiLevel.Major)) {
								Log.LogWarningForXmlNode (
										code:             "XA4216",
										file:             AndroidManifest,
										node:             target_sdk,
										message:          Properties.Resources.XA4216_TargetSdkVersion,
										messageArgs:      new object [] { target_sdk?.Value ?? "", XABuildConfig.AndroidMinimumDotNetApiLevel }
								);
							}
						}
					}
				} catch (Exception ex) {
					// If they're manifest is bad, let's error them out
					Log.LogErrorFromException (ex, true);
					return false;
				}
			}

			platform            = GetTargetSdkVersion (platform, target_sdk);
			JavaPlatformJarPath =  MonoAndroidHelper.TryGetAndroidJarPath (Log, platform,
				designTimeBuild: DesignTimeBuild, buildingInsideVisualStudio: BuildingInsideVisualStudio,
				targetFramework: TargetFramework, androidSdkDirectory: AndroidSdkDirectory);
			TargetSdkVersion = MonoAndroidHelper.SupportedVersions.GetApiLevelFromId (platform).ToString ();
			if (JavaPlatformJarPath == null)
				return !Log.HasLoggedErrors;

			return !Log.HasLoggedErrors;
		}

		string GetTargetSdkVersion (string target, XAttribute? target_sdk)
		{
			string targetFrameworkVersion = MonoAndroidHelper.SupportedVersions.GetIdFromApiLevel (AndroidApiLevel) ?? "";
			string targetSdkVersion       = MonoAndroidHelper.SupportedVersions.GetIdFromApiLevel (target) ?? "";

			// For .NET 6+ projects, use TargetPlatformVersion directly
			string targetPlatformVersionDisplay = !TargetPlatformVersion.IsNullOrEmpty () ? TargetPlatformVersion : "";

			if (!int.TryParse (targetFrameworkVersion, out int frameworkSdk)) {
				// AndroidApiLevel is likely a *preview* API level; use it.
				Log.LogWarningForXmlNode (
						code:             "XA4211",
						file:             AndroidManifest,
						node:             target_sdk,
						message:          Properties.Resources.XA4211,
						messageArgs:      new [] {
							targetSdkVersion,
							targetPlatformVersionDisplay,
							MonoAndroidHelper.SupportedVersions.GetIdFromApiLevel (targetFrameworkVersion) ?? "",
						}
				);
				return targetFrameworkVersion;
			}
			if (int.TryParse (targetSdkVersion, out int targetSdk) &&
					targetSdk < frameworkSdk) {
				Log.LogWarningForXmlNode (
						code:             "XA4211",
						file:             AndroidManifest,
						node:             target_sdk,
						message:          Properties.Resources.XA4211,
						messageArgs:      new [] {
							targetSdkVersion,
							targetPlatformVersionDisplay,
							MonoAndroidHelper.SupportedVersions.GetIdFromApiLevel (targetFrameworkVersion) ?? "",
						}
				);
				return targetFrameworkVersion;
			}
			return targetSdkVersion ?? targetFrameworkVersion;
		}
	}
}
