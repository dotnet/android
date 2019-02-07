// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	public class GetJavaPlatformJar : Task
	{
		private XNamespace androidNs = "http://schemas.android.com/apk/res/android";

		[Required]
		public string AndroidSdkPlatform { get; set; }

		public string AndroidManifest { get; set; }

		[Output]
		public string JavaPlatformJarPath { get; set; }

		[Output]
		public string TargetSdkVersion    { get; set; }

		public override bool Execute ()
		{
			var platform = AndroidSdkPlatform;

			XAttribute target_sdk = null;

			// Look for targetSdkVersion in the user's AndroidManifest.xml
			if (!string.IsNullOrWhiteSpace (AndroidManifest)) {
				if (!File.Exists (AndroidManifest)) {
					Log.LogError ("Specified AndroidManifest.xml file does not exist: {0}.", AndroidManifest);
					return false;
				}

				try {
					var doc = XDocument.Load (AndroidManifest);
					var manifest = doc.Root;

					if (manifest != null) {
						var uses_sdk = manifest.Element ("uses-sdk");

						if (uses_sdk != null) {
							target_sdk = uses_sdk.Attribute (androidNs + "targetSdkVersion");

							if (target_sdk != null && !string.IsNullOrWhiteSpace (target_sdk.Value))
								platform = target_sdk.Value;

							var min_sdk = uses_sdk.Attribute (androidNs + "minSdkVersion");
							if (min_sdk != null && (!int.TryParse (min_sdk.Value, out int minSdkVersion) || minSdkVersion < XABuildConfig.NDKMinimumApiAvailable)) {
								LogWarningForManifest (
										warningCode:      "XA4216",
										attribute:        min_sdk,
										message:          "AndroidManifest.xml //uses-sdk/@android:minSdkVersion '{0}' is less than API-{1}, this configuration is not supported.",
										messageArgs:      new object [] { min_sdk?.Value, XABuildConfig.NDKMinimumApiAvailable }
								);
							}
							if (target_sdk != null && (!int.TryParse (target_sdk.Value, out int targetSdkVersion) || targetSdkVersion < XABuildConfig.NDKMinimumApiAvailable)) {
								LogWarningForManifest (
										warningCode:      "XA4216",
										attribute:        target_sdk,
										message:          "AndroidManifest.xml //uses-sdk/@android:targetSdkVersion '{0}' is less than API-{1}, this configuration is not supported.",
										messageArgs:      new object [] { target_sdk?.Value, XABuildConfig.NDKMinimumApiAvailable }
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
			JavaPlatformJarPath =  MonoAndroidHelper.TryGetAndroidJarPath (Log, platform);
			if (JavaPlatformJarPath == null)
				return !Log.HasLoggedErrors;

			TargetSdkVersion = MonoAndroidHelper.SupportedVersions.GetApiLevelFromId (platform).ToString ();

			return !Log.HasLoggedErrors;
		}

		string GetTargetSdkVersion (string target, XAttribute target_sdk)
		{
			string targetFrameworkVersion = MonoAndroidHelper.SupportedVersions.GetIdFromApiLevel (AndroidSdkPlatform);
			string targetSdkVersion       = MonoAndroidHelper.SupportedVersions.GetIdFromApiLevel (target);

			int frameworkSdk, targetSdk;
			if (int.TryParse (targetFrameworkVersion, out frameworkSdk) &&
					int.TryParse (targetSdkVersion, out targetSdk) &&
					targetSdk < frameworkSdk) {
				LogWarningForManifest (
						warningCode:      "XA4211",
						attribute:        target_sdk,
						message:          "AndroidManifest.xml //uses-sdk/@android:targetSdkVersion '{0}' is less than $(TargetFrameworkVersion) '{1}'. Using API-{2} for ACW compilation.",
						messageArgs:      new[]{
							targetSdkVersion,
							MonoAndroidHelper.SupportedVersions.GetIdFromFrameworkVersion (targetFrameworkVersion),
							MonoAndroidHelper.SupportedVersions.GetIdFromApiLevel (targetFrameworkVersion),
						}
				);
				return targetFrameworkVersion;
			}
			return targetSdkVersion;
		}

		void LogWarningForManifest (string warningCode, XAttribute attribute, string message, params object[] messageArgs)
		{
			int lineNumber    = 0;
			int columnNumber  = 0;
			var lineInfo      = attribute as IXmlLineInfo;
			if (lineInfo != null && lineInfo.HasLineInfo ()) {
				lineNumber    = lineInfo.LineNumber;
				columnNumber  = lineInfo.LinePosition;
			}
			Log.LogWarning (
					subcategory:      string.Empty,
					warningCode:      warningCode,
					helpKeyword:      string.Empty,
					file:             AndroidManifest,
					lineNumber:       lineNumber,
					columnNumber:     columnNumber,
					endLineNumber:    0,
					endColumnNumber:  0,
					message:          message,
					messageArgs:      messageArgs
			);
		}
	}
}
