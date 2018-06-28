// 
// ResolveSdksTask.cs
//  
// Author:
//       Michael Hutchinson <mhutchinson@novell.com>
//       Jonathan Pryor <jonp@xamarin.com>
// 
// Copyright (c) 2010 Novell, Inc.
// Copyright (c) 2013 Xamarin Inc.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;
using System.Xml.Linq;
using Xamarin.Android.Tools;
using System.Text.RegularExpressions;

namespace Xamarin.Android.Tasks
{
	public class ResolveSdks : Task
	{
		[Output]
		public string AndroidApiLevel { get; set; }

		[Output]
		public string AndroidApiLevelName { get; set; }

		[Output]
		public string SupportedApiLevel { get; set; }

		public string AndroidSdkBuildToolsVersion { get; set; }

		public string BuildingInsideVisualStudio { get; set; }

		public string ProjectFilePath             { get; set; }
		public bool   UseLatestAndroidPlatformSdk { get; set; }
		public bool   AotAssemblies               { get; set; }

		public string JavaToolExe { get; set; }
		public string JavacToolExe { get; set; }

		public string LatestSupportedJavaVersion { get; set; }

		public string MinimumSupportedJavaVersion { get; set; }

		[Output]
		public string[] ReferenceAssemblyPaths { get; set; }

		public string CacheFile { get; set;}

		public string SequencePointsMode { get; set;}

		[Output]
		public string TargetFrameworkVersion { get; set; }

		[Output]
		public string MonoAndroidToolsPath { get; set; }

		[Output]
		public string MonoAndroidBinPath { get; set; }

		[Output]
		public string MonoAndroidIncludePath { get; set; }

		[Output]
		public string AndroidNdkPath { get; set; }

		[Output]
		public string AndroidSdkPath { get; set; }

		[Output]
		public string JavaSdkPath { get; set; }

		[Output]
		public string AndroidSdkBuildToolsPath { get; set; }

		[Output]
		public string AndroidSdkBuildToolsBinPath { get; set; }

		[Output]
		public string ZipAlignPath { get; set; }

		[Output]
		public string AndroidSequencePointsMode { get; set; }

		[Output]
		public string LintToolPath { get; set; }

		[Output]
		public string ApkSignerJar { get; set; }

		[Output]
		public bool AndroidUseApkSigner { get; set; }

		[Output]
		public bool AndroidUseAapt2 { get; set; }

		[Output]
		public string Aapt2Version { get; set; }

		[Output]
		public string JdkVersion { get; set; }

		[Output]
		public string MinimumRequiredJdkVersion { get; set; }

		static bool             IsWindows = Path.DirectorySeparatorChar == '\\';
		static readonly string  ZipAlign  = IsWindows ? "zipalign.exe" : "zipalign";
		static readonly string  Aapt      = IsWindows ? "aapt.exe" : "aapt";
		static readonly string  Aapt2      = IsWindows ? "aapt2.exe" : "aapt2";
		static readonly string  Android   = IsWindows ? "android.bat" : "android";
		static readonly string  Lint      = IsWindows ? "lint.bat" : "lint";
		static readonly string  ApkSigner = "apksigner.jar";


		public override bool Execute ()
		{
			try {
				return RunTask();
			}
			finally {
			}
		}

		public bool RunTask ()
		{
			Log.LogDebugMessage ("ResolveSdksTask:");
			Log.LogDebugMessage ("  AndroidApiLevel: {0}", AndroidApiLevel);
			Log.LogDebugMessage ("  AndroidSdkBuildToolsVersion: {0}", AndroidSdkBuildToolsVersion);
			Log.LogDebugMessage ($"  {nameof (AndroidSdkPath)}: {AndroidSdkPath}");
			Log.LogDebugMessage ($"  {nameof (AndroidNdkPath)}: {AndroidNdkPath}");
			Log.LogDebugMessage ($"  {nameof (JavaSdkPath)}: {JavaSdkPath}");
			Log.LogDebugTaskItems ("  ReferenceAssemblyPaths: ", ReferenceAssemblyPaths);
			Log.LogDebugMessage ("  TargetFrameworkVersion: {0}", TargetFrameworkVersion);
			Log.LogDebugMessage ("  UseLatestAndroidPlatformSdk: {0}", UseLatestAndroidPlatformSdk);
			Log.LogDebugMessage ("  SequencePointsMode: {0}", SequencePointsMode);
			Log.LogDebugMessage ("  LintToolPath: {0}", LintToolPath);

			// OS X:    $prefix/lib/xamarin.android/xbuild/Xamarin/Android
			// Windows: %ProgramFiles(x86)%\MSBuild\Xamarin\Android
			if (string.IsNullOrEmpty (MonoAndroidToolsPath)) {
				MonoAndroidToolsPath  = Path.GetDirectoryName (typeof (ResolveSdks).Assembly.Location);
			}
			MonoAndroidBinPath  = MonoAndroidHelper.GetOSBinPath () + Path.DirectorySeparatorChar;

			MonoAndroidHelper.RefreshSupportedVersions (ReferenceAssemblyPaths);

			try {
				MonoAndroidHelper.RefreshAndroidSdk (AndroidSdkPath, AndroidNdkPath, JavaSdkPath, Log);
			}
			catch (InvalidOperationException e) {
				if (e.Message.Contains (" Android ")) {
					Log.LogCodedError ("XA5300", "The Android SDK Directory could not be found. Please set via /p:AndroidSdkDirectory.");
				}
				if (e.Message.Contains (" Java ")) {
					Log.LogCodedError ("XA5300", "The Java SDK Directory could not be found. Please set via /p:JavaSdkDirectory.");
				}
				return false;
			}

			this.AndroidNdkPath = MonoAndroidHelper.AndroidSdk.AndroidNdkPath;
			this.AndroidSdkPath = MonoAndroidHelper.AndroidSdk.AndroidSdkPath;
			this.JavaSdkPath    = MonoAndroidHelper.AndroidSdk.JavaSdkPath;

			if (string.IsNullOrEmpty (AndroidSdkPath)) {
				Log.LogCodedError ("XA5300", "The Android SDK Directory could not be found. Please set via /p:AndroidSdkDirectory.");
				return false;
			}
			if (string.IsNullOrEmpty (JavaSdkPath)) {
				Log.LogCodedError ("XA5300", "The Java SDK Directory could not be found. Please set via /p:JavaSdkDirectory.");
				return false;
			}

			if (!ValidateJavaVersion (TargetFrameworkVersion, AndroidSdkBuildToolsVersion))
				return false;

			string toolsZipAlignPath = Path.Combine (AndroidSdkPath, "tools", ZipAlign);
			bool findZipAlign = (string.IsNullOrEmpty (ZipAlignPath) || !Directory.Exists (ZipAlignPath)) && !File.Exists (toolsZipAlignPath);

			var lintPaths = new string [] {
				LintToolPath ?? string.Empty,
				Path.Combine (AndroidSdkPath, "tools"),
				Path.Combine (AndroidSdkPath, "tools", "bin"),
			};

			LintToolPath = null;
			foreach ( var path in lintPaths) {
				if (File.Exists (Path.Combine (path, Lint))) {
					LintToolPath = path;
					break;
				}
			}

			foreach (var dir in MonoAndroidHelper.AndroidSdk.GetBuildToolsPaths (AndroidSdkBuildToolsVersion)) {
				Log.LogDebugMessage ("Trying build-tools path: {0}", dir);
				if (dir == null || !Directory.Exists (dir))
					continue;

				var toolsPaths = new string[] {
					Path.Combine (dir),
					Path.Combine (dir, "bin"), 
				};
					
				string aapt = toolsPaths.FirstOrDefault (x => File.Exists (Path.Combine (x, Aapt)));
				if (string.IsNullOrEmpty (aapt)) {
					Log.LogDebugMessage ("Could not find `{0}`; tried: {1}", Aapt,
						string.Join (";", toolsPaths.Select (x => Path.Combine (x, Aapt))));
					continue;
				}
				AndroidSdkBuildToolsPath = Path.GetFullPath (dir);
				AndroidSdkBuildToolsBinPath = Path.GetFullPath (aapt);

				string zipalign = toolsPaths.FirstOrDefault (x => File.Exists (Path.Combine (x, ZipAlign)));
				if (findZipAlign && string.IsNullOrEmpty (zipalign)) {
					Log.LogDebugMessage ("Could not find `{0}`; tried: {1}", ZipAlign,
						string.Join (";", toolsPaths.Select (x => Path.Combine (x, ZipAlign))));
					continue;
				}
				else
					break;
			}

			if (string.IsNullOrEmpty (AndroidSdkBuildToolsPath)) {
				Log.LogCodedError ("XA5205",
						string.Format (
							"Cannot find `{0}`. Please install the Android SDK Build-tools package with the `{1}{2}tools{2}{3}` program.",
							Aapt, AndroidSdkPath, Path.DirectorySeparatorChar, Android));
				return false;
			}

			ApkSignerJar = Path.Combine (AndroidSdkBuildToolsBinPath, "lib", ApkSigner);
			AndroidUseApkSigner = File.Exists (ApkSignerJar);

			bool aapt2Installed = File.Exists (Path.Combine (AndroidSdkBuildToolsBinPath, Aapt2));
			if (aapt2Installed && AndroidUseAapt2) {
				if (!GetAapt2Version ()) {
					AndroidUseAapt2 = false;
					aapt2Installed = false;
					Log.LogCodedWarning ("XA0111", "Could not get the `aapt2` version. Disabling `aapt2` support. Please check it is installed correctly.");
				}
			}
			if (AndroidUseAapt2) {
				if (!aapt2Installed) {
					AndroidUseAapt2 = false;
					Log.LogCodedWarning ("XA0112", "`aapt2` is not installed. Disabling `aapt2` support. Please check it is installed correctly.");
				}
			}

			if (string.IsNullOrEmpty (ZipAlignPath) || !Directory.Exists (ZipAlignPath)) {
				ZipAlignPath = new[]{
					Path.Combine (AndroidSdkBuildToolsPath),
					Path.Combine (AndroidSdkBuildToolsBinPath),
					Path.Combine (AndroidSdkPath, "tools"),
				}
					.Where (p => File.Exists (Path.Combine (p, ZipAlign)))
					.FirstOrDefault ();
			}
			if (string.IsNullOrEmpty (ZipAlignPath)) {
				Log.LogCodedError ("XA5205",
						string.Format (
							"Cannot find `{0}`. Please install the Android SDK Build-tools package with the `{1}{2}tools{2}{3}` program.",
							ZipAlign, AndroidSdkPath, Path.DirectorySeparatorChar, Android));
				return false;
			}

			if (!ValidateApiLevels ())
				return false;

			if (!MonoAndroidHelper.SupportedVersions.FrameworkDirectories.Any (p => Directory.Exists (Path.Combine (p, TargetFrameworkVersion)))) {
				Log.LogError (
					subcategory:      string.Empty,
					errorCode:        "XA0001",
					helpKeyword:      string.Empty,
					file:             ProjectFilePath,
					lineNumber:       0,
					columnNumber:     0,
					endLineNumber:    0,
					endColumnNumber:  0,
					message:          "Unsupported or invalid $(TargetFrameworkVersion) value of '{0}'. Please update your Project Options.",
					messageArgs:      new[]{
						TargetFrameworkVersion,
					}
				);
				return false;
			}

			int apiLevel;
			var augustDeadline = new DateTime (2018, 8, 1);
			var novemberDeadline = new DateTime (2018, 11, 1);
			if (int.TryParse (AndroidApiLevel, out apiLevel)) {
				if (apiLevel < 26 && DateTime.Now >= augustDeadline)
					Log.LogCodedWarning ("XA0113", $"Google Play requires that new applications must use a TargetFrameworkVersion of v8.0 (API level 26) or above. You are currently targeting {TargetFrameworkVersion} (API level {AndroidApiLevel}).");
				if (apiLevel < 26 && DateTime.Now >= novemberDeadline)
					Log.LogCodedWarning ("XA0114", $"Google Play requires that application updates must use a TargetFrameworkVersion of v8.0 (API level 26) or above. You are currently targeting {TargetFrameworkVersion} (API level {AndroidApiLevel}).");
			}

			SequencePointsMode mode;
			if (!Aot.TryGetSequencePointsMode (SequencePointsMode ?? "None", out mode))
				Log.LogCodedError ("XA0104", "Invalid Sequence Point mode: {0}", SequencePointsMode);
			AndroidSequencePointsMode = mode.ToString ();

			MonoAndroidHelper.TargetFrameworkDirectories = ReferenceAssemblyPaths;

			AndroidApiLevelName = MonoAndroidHelper.SupportedVersions.GetIdFromApiLevel (AndroidApiLevel);

			Log.LogDebugMessage ("ResolveSdksTask Outputs:");
			Log.LogDebugMessage ("  AndroidApiLevel: {0}", AndroidApiLevel);
			Log.LogDebugMessage ("  AndroidApiLevelName: {0}", AndroidApiLevelName);
			Log.LogDebugMessage ("  AndroidNdkPath: {0}", AndroidNdkPath);
			Log.LogDebugMessage ("  AndroidSdkBuildToolsPath: {0}", AndroidSdkBuildToolsPath);
			Log.LogDebugMessage ("  AndroidSdkBuildToolsBinPath: {0}", AndroidSdkBuildToolsBinPath);
			Log.LogDebugMessage ("  AndroidSdkPath: {0}", AndroidSdkPath);
			Log.LogDebugMessage ("  JavaSdkPath: {0}", JavaSdkPath);
			Log.LogDebugMessage ("  JdkVersion: {0}", JdkVersion);
			Log.LogDebugMessage ("  MinimumRequiredJdkVersion: {0}", MinimumRequiredJdkVersion);
			Log.LogDebugMessage ("  MonoAndroidBinPath: {0}", MonoAndroidBinPath);
			Log.LogDebugMessage ("  MonoAndroidToolsPath: {0}", MonoAndroidToolsPath);
			Log.LogDebugMessage ("  TargetFrameworkVersion: {0}", TargetFrameworkVersion);
			Log.LogDebugMessage ("  ZipAlignPath: {0}", ZipAlignPath);
			Log.LogDebugMessage ("  SupportedApiLevel: {0}", SupportedApiLevel);
			Log.LogDebugMessage ("  AndroidSequencePointMode: {0}", AndroidSequencePointsMode);
			Log.LogDebugMessage ("  LintToolPath: {0}", LintToolPath);
			Log.LogDebugMessage ("  AndroidUseApkSigner: {0}", AndroidUseApkSigner);
			Log.LogDebugMessage ("  AndroidUseAapt2: {0}", AndroidUseAapt2);
			Log.LogDebugMessage ("  Aapt2Version: {0}", Aapt2Version);

			if (!string.IsNullOrEmpty (CacheFile)) {
				Directory.CreateDirectory (Path.GetDirectoryName (CacheFile));

				var document = new XDocument (
					new XDeclaration ("1.0", "UTF-8", null),
					new XElement ("Sdk",
						new XElement ("AndroidApiLevel", AndroidApiLevel),
						new XElement ("AndroidApiLevelName", AndroidApiLevelName),
						new XElement ("AndroidNdkPath", AndroidNdkPath),
						new XElement ("AndroidSdkBuildToolsPath", AndroidSdkBuildToolsPath),
						new XElement ("AndroidSdkBuildToolsBinPath", AndroidSdkBuildToolsBinPath),
						new XElement ("AndroidSdkPath", AndroidSdkPath),
						new XElement ("JavaSdkPath", JavaSdkPath),
						new XElement ("MonoAndroidBinPath", MonoAndroidBinPath),
						new XElement ("MonoAndroidToolsPath", MonoAndroidToolsPath),
						new XElement ("ReferenceAssemblyPaths",
								(ReferenceAssemblyPaths ?? new string [0])
								.Select(e => new XElement ("ReferenceAssemblyPath", e))),
						new XElement ("TargetFrameworkVersion", TargetFrameworkVersion),
						new XElement ("ZipAlignPath", ZipAlignPath),
						new XElement ("MonoAndroidIncludePath", MonoAndroidIncludePath),
						new XElement ("SupportedApiLevel", SupportedApiLevel),
						new XElement ("AndroidSequencePointsMode", AndroidSequencePointsMode.ToString ()),
						new XElement ("LintToolPath", LintToolPath)
					));
				document.Save (CacheFile);
			}

			//note: this task does not error out if it doesn't find all things. that's the job of the targets
			return !Log.HasLoggedErrors;
		}

		// `java -version` will produce values such as:
		//  java version "9.0.4"
		//  java version "1.8.0_77"
		static  readonly  Regex JavaVersionRegex  = new Regex (@"version ""(?<version>[\d\.]+)(_d+)?[^""]*""");

		// `javac -version` will produce values such as:
		//  javac 9.0.4
		//  javac 1.8.0_77
		static  readonly  Regex JavacVersionRegex = new Regex (@"(?<version>[\d\.]+)(_d+)?");

		//  Android Asset Packaging Tool (aapt) 2:19
		static readonly Regex Aapt2VersionRegex = new Regex (@"(?<version>[\d\:]+)(\d+)?");

		Version GetJavaVersionForFramework (string targetFrameworkVersion)
		{
			var apiLevel = MonoAndroidHelper.SupportedVersions.GetApiLevelFromFrameworkVersion (targetFrameworkVersion);
			if (apiLevel >= 24)
				return new Version (1, 8);
			else if (apiLevel == 23)
				return new Version (1, 7);
			else
				return new Version (1, 6);
		}

		Version GetJavaVersionForBuildTools (string buildToolsVersion)
		{
			Version buildTools;
			if (!Version.TryParse (buildToolsVersion, out buildTools)) {
				return Version.Parse (LatestSupportedJavaVersion);
			}
			if (buildTools >= new Version (24, 0, 1))
				return new Version (1, 8);
			return Version.Parse (MinimumSupportedJavaVersion);
		}

		bool ValidateJavaVersion (string targetFrameworkVersion, string buildToolsVersion)
		{
			var java  = JavaToolExe   ?? (OS.IsWindows ? "java.exe" : "java");
			var javac = JavacToolExe  ?? (OS.IsWindows ? "javac.exe" : "javac");

			return ValidateJavaVersion (java, JavaVersionRegex, targetFrameworkVersion, buildToolsVersion) &&
				ValidateJavaVersion (javac, JavacVersionRegex, targetFrameworkVersion, buildToolsVersion);
		}

		bool ValidateJavaVersion (string javaExe, Regex versionRegex, string targetFrameworkVersion, string buildToolsVersion)
		{
			Version requiredJavaForFrameworkVersion = GetJavaVersionForFramework (targetFrameworkVersion);
			Version requiredJavaForBuildTools = GetJavaVersionForBuildTools (buildToolsVersion);

			Version required = requiredJavaForFrameworkVersion > requiredJavaForBuildTools ? requiredJavaForFrameworkVersion : requiredJavaForBuildTools;

			MinimumRequiredJdkVersion = required.ToString ();
			
			var sb = new StringBuilder ();
			
			var javaTool = Path.Combine (JavaSdkPath, "bin", javaExe);
			try {
				MonoAndroidHelper.RunProcess (javaTool, "-version", (s, e) => {
						if (!string.IsNullOrEmpty (e.Data))
							sb.AppendLine (e.Data);
					}, (s, e) => {
						if (!string.IsNullOrEmpty (e.Data))
							sb.AppendLine (e.Data);
					}
				);
			} catch (Exception ex) {
				Log.LogWarningFromException (ex);
				Log.LogCodedWarning ("XA0034", $"Failed to get the Java SDK version. Please ensure you have Java {required} or above installed.");
				return false;
			}
			var versionInfo = sb.ToString ();
			var versionNumberMatch = versionRegex.Match (versionInfo);
			Version versionNumber;
			if (versionNumberMatch.Success && Version.TryParse (versionNumberMatch.Groups ["version"]?.Value, out versionNumber)) {
				JdkVersion  = versionNumberMatch.Groups ["version"].Value;
				Log.LogMessage (MessageImportance.Normal, $"Found Java SDK version {versionNumber}.");
				if (versionNumber < requiredJavaForFrameworkVersion) {
					Log.LogCodedError ("XA0031", $"Java SDK {requiredJavaForFrameworkVersion} or above is required when targeting FrameworkVersion {targetFrameworkVersion}.");
				}
				if (versionNumber < requiredJavaForBuildTools) {
					Log.LogCodedError ("XA0032", $"Java SDK {requiredJavaForBuildTools} or above is required when using build-tools {buildToolsVersion}.");
				}
				if (versionNumber > Version.Parse (LatestSupportedJavaVersion)) {
					Log.LogCodedError ("XA0030", $"Building with JDK Version `{versionNumber}` is not supported. Please install JDK version `{LatestSupportedJavaVersion}`. See https://aka.ms/xamarin/jdk9-errors");
				}
			} else
				Log.LogCodedWarning ("XA0033", $"Failed to get the Java SDK version as it does not appear to contain a valid version number. `{javaExe} -version` returned: ```{versionInfo}```");
			return !Log.HasLoggedErrors;
		}

		bool GetAapt2Version ()
		{
			var sb = new StringBuilder ();
			var aapt2Tool = Path.Combine (AndroidSdkBuildToolsBinPath, Aapt2);
			try {
				MonoAndroidHelper.RunProcess (aapt2Tool, "version",  (s, e) => {
						if (!string.IsNullOrEmpty (e.Data))
							sb.AppendLine (e.Data);
					}, (s, e) => {
						if (!string.IsNullOrEmpty (e.Data))
							sb.AppendLine (e.Data);
					}
				);
			} catch (Exception ex) {
				Log.LogWarningFromException (ex);
				return false;
			}
			var versionInfo = sb.ToString ();
			var versionNumberMatch = Aapt2VersionRegex.Match (versionInfo);
			Log.LogDebugMessage ($"`{aapt2Tool} version` returned: ```{versionInfo}```");
			if (versionNumberMatch.Success && Version.TryParse (versionNumberMatch.Groups ["version"]?.Value.Replace (":", "."), out Version versionNumber)) {
				Aapt2Version = versionNumber.ToString ();
				return true;
			}
			return false;
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
				AndroidApiLevel         = GetMaxInstalledApiLevel ().ToString ();
				SupportedApiLevel       = GetMaxStableApiLevel ().ToString ();
				int maxInstalled, maxSupported = 0;
				if (int.TryParse (AndroidApiLevel, out maxInstalled) && int.TryParse (SupportedApiLevel, out maxSupported) && maxInstalled > maxSupported) {
					Log.LogDebugMessage ($"API Level {AndroidApiLevel} is greater than the maximum supported API level of {SupportedApiLevel}. " +
						"Support for this API will be added in a future release.");
					AndroidApiLevel = SupportedApiLevel;
				}
				if (!string.IsNullOrWhiteSpace (TargetFrameworkVersion)) {
					var userSelected = MonoAndroidHelper.SupportedVersions.GetApiLevelFromFrameworkVersion (TargetFrameworkVersion);
					// overwrite using user version only if it is 
					// above the maxStableApi and a valid apiLevel.
					if (userSelected != null && userSelected > maxSupported && userSelected <= maxInstalled) {
						AndroidApiLevel   = userSelected.ToString ();
						SupportedApiLevel = userSelected.ToString ();
					}
				}
				TargetFrameworkVersion  = GetTargetFrameworkVersionFromApiLevel ();
				return TargetFrameworkVersion != null;
			}

			if (!string.IsNullOrWhiteSpace (TargetFrameworkVersion)) {
				TargetFrameworkVersion  = TargetFrameworkVersion.Trim ();
				string id   = MonoAndroidHelper.SupportedVersions.GetIdFromFrameworkVersion (TargetFrameworkVersion);
				if (id == null) {
					Log.LogCodedError ("XA0000",
							"Could not determine API level for $(TargetFrameworkVersion) of '{0}'.",
							TargetFrameworkVersion);
					return false;
				}
				AndroidApiLevel     = MonoAndroidHelper.SupportedVersions.GetApiLevelFromId (id).ToString ();
				SupportedApiLevel   = AndroidApiLevel;
				return true;
			}

			if (!string.IsNullOrWhiteSpace (AndroidApiLevel)) {
				AndroidApiLevel         = AndroidApiLevel.Trim ();
				SupportedApiLevel       = GetMaxSupportedApiLevel (AndroidApiLevel);
				TargetFrameworkVersion  = GetTargetFrameworkVersionFromApiLevel ();
				return TargetFrameworkVersion != null;
			}

			Log.LogCodedError ("XA0000", "Could not determine $(AndroidApiLevel) or $(TargetFrameworkVersion); SHOULD NOT BE REACHED.");
			return false;
		}

		int GetMaxInstalledApiLevel ()
		{
			string platformsDir = Path.Combine (AndroidSdkPath, "platforms");
			var apiIds = Directory.EnumerateDirectories (platformsDir)
				.Select (platformDir => Path.GetFileName (platformDir))
				.Where (dir => dir.StartsWith ("android-", StringComparison.OrdinalIgnoreCase))
				.Select (dir => dir.Substring ("android-".Length))
				.Select (apiName => MonoAndroidHelper.SupportedVersions.GetIdFromApiLevel (apiName));
			int maxApiLevel = int.MinValue;
			foreach (var id in apiIds) {
				int? v = MonoAndroidHelper.SupportedVersions.GetApiLevelFromId (id);
				if (!v.HasValue)
					continue;
				maxApiLevel = Math.Max (maxApiLevel, v.Value);
			}
			if (maxApiLevel < 0)
				Log.LogCodedError ("XA5300",
						"No Android platforms installed at '{0}'. Please install an SDK Platform with the `{1}{2}tools{2}{3}` program.",
						platformsDir, Path.DirectorySeparatorChar, Android);
			return maxApiLevel;
		}

		int GetMaxStableApiLevel ()
		{
			return MonoAndroidHelper.SupportedVersions.MaxStableVersion.ApiLevel;
		}

		string GetMaxSupportedApiLevel (string apiLevel)
		{
			int level = 0;
			if (!int.TryParse (apiLevel, NumberStyles.Integer, CultureInfo.InvariantCulture, out level))
				return apiLevel;
			if (ReferenceAssemblyPaths == null)
				return apiLevel;
			foreach (string versionedDir in ReferenceAssemblyPaths) {
				string parent   = Path.GetDirectoryName (versionedDir.TrimEnd (Path.DirectorySeparatorChar));
				for ( int l = level ; l > 0; l--) {
					string tfv = MonoAndroidHelper.SupportedVersions.GetFrameworkVersionFromApiLevel (l);
					if (tfv == null)
						continue;
					string dir = Path.Combine (parent, tfv);
					if (Directory.Exists (dir))
						return l.ToString ();
				}
			}
			return apiLevel;
		}

		string GetTargetFrameworkVersionFromApiLevel ()
		{
			string targetFramework = MonoAndroidHelper.SupportedVersions.GetFrameworkVersionFromId (SupportedApiLevel) ??
				MonoAndroidHelper.SupportedVersions.GetFrameworkVersionFromId (AndroidApiLevel);
			if (targetFramework != null)
				return targetFramework;
			Log.LogCodedError ("XA0000",
					"Could not determine $(TargetFrameworkVersion) for API level '{0}.'",
					AndroidApiLevel);
			return null;
		}

		void ErrorHandler (string task, string message)
		{
			Log.LogError ($"{task} {message}");
		}

		void WarningHandler (string task, string message)
		{
			Log.LogWarning ($"{task} {message}");
		}

		void DebugHandler (string task, string message)
		{
			Log.LogDebugMessage ($"DEBUG {task} {message}");
		}

		void InfoHandler (string task, string message)
		{
			Log.LogMessage ($"{task} {message}");
		}
	}
}

