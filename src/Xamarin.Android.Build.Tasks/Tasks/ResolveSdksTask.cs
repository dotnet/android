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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;
using System.Xml.Linq;
using Xamarin.Android.Build.Utilities;

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

		static bool             IsWindows = Path.DirectorySeparatorChar == '\\';
		static readonly string  ZipAlign  = IsWindows ? "zipalign.exe" : "zipalign";
		static readonly string  Aapt      = IsWindows ? "aapt.exe" : "aapt";
		static readonly string  Android   = IsWindows ? "android.bat" : "android";


		public override bool Execute ()
		{
			Log.LogDebugMessage ("ResolveSdksTask:");
			Log.LogDebugMessage ("  AndroidApiLevel: {0}", AndroidApiLevel);
			Log.LogDebugMessage ("  AndroidSdkBuildToolsVersion: {0}", AndroidSdkBuildToolsVersion);
			Log.LogDebugTaskItems ("  ReferenceAssemblyPaths: ", ReferenceAssemblyPaths);
			Log.LogDebugMessage ("  TargetFrameworkVersion: {0}", TargetFrameworkVersion);
			Log.LogDebugMessage ("  UseLatestAndroidPlatformSdk: {0}", UseLatestAndroidPlatformSdk);
			Log.LogDebugMessage ("  SequencePointsMode: {0}", SequencePointsMode);

			MonoAndroidHelper.RefreshAndroidSdk (AndroidSdkPath, AndroidNdkPath, JavaSdkPath);
			MonoAndroidHelper.RefreshMonoDroidSdk (MonoAndroidToolsPath, MonoAndroidBinPath, ReferenceAssemblyPaths);

			// OS X:    $prefix/lib/mandroid
			// Windows: %ProgramFiles(x86)%\MSBuild\Xamarin\Android
			this.MonoAndroidToolsPath = MonoDroidSdk.RuntimePath;

			// OS X:    $prefix/bin
			// Windows: %ProgramFiles(x86)%\MSBuild\Xamarin\Android
			this.MonoAndroidBinPath = MonoDroidSdk.BinPath;

			if (this.MonoAndroidBinPath == null) {
				Log.LogCodedError ("XA0020", "Could not find mandroid!");
				return false;
			}

			string include;
			if (MonoAndroidToolsPath != null &&
					Directory.Exists (include = Path.Combine (MonoAndroidToolsPath, "include")))
				MonoAndroidIncludePath = include;

			this.AndroidNdkPath = AndroidSdk.AndroidNdkPath;
			this.AndroidSdkPath = AndroidSdk.AndroidSdkPath;
			this.JavaSdkPath = AndroidSdk.JavaSdkPath;

			if (string.IsNullOrEmpty (AndroidSdkPath)) {
				Log.LogCodedError ("XA5205", "The Android SDK Directory could not be found. Please set via /p:AndroidSdkDirectory.");
				return false;
			}

			string toolsZipAlignPath = Path.Combine (AndroidSdkPath, "tools", ZipAlign);
			bool findZipAlign = (string.IsNullOrEmpty (ZipAlignPath) || !Directory.Exists (ZipAlignPath)) && !File.Exists (toolsZipAlignPath);

			foreach (var dir in AndroidSdk.GetBuildToolsPaths (AndroidSdkBuildToolsVersion)) {
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

			string  frameworksPath  = Path.GetDirectoryName (MonoDroidSdk.FrameworkPath);
			if (!Directory.Exists (Path.Combine (frameworksPath, TargetFrameworkVersion))) {
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

			SequencePointsMode mode;
			if (!Aot.TryGetSequencePointsMode (SequencePointsMode ?? "None", out mode))
				Log.LogCodedError ("XA0104", "Invalid Sequence Point mode: {0}", SequencePointsMode);
			AndroidSequencePointsMode = mode.ToString ();


			AndroidApiLevelName = MonoAndroidHelper.GetPlatformApiLevelName (AndroidApiLevel);

			Log.LogDebugMessage ("ResolveSdksTask Outputs:");
			Log.LogDebugMessage ("  AndroidApiLevel: {0}", AndroidApiLevel);
			Log.LogDebugMessage ("  AndroidApiLevelName: {0}", AndroidApiLevelName);
			Log.LogDebugMessage ("  AndroidNdkPath: {0}", AndroidNdkPath);
			Log.LogDebugMessage ("  AndroidSdkBuildToolsPath: {0}", AndroidSdkBuildToolsPath);
			Log.LogDebugMessage ("  AndroidSdkBuildToolsBinPath: {0}", AndroidSdkBuildToolsBinPath);
			Log.LogDebugMessage ("  AndroidSdkPath: {0}", AndroidSdkPath);
			Log.LogDebugMessage ("  JavaSdkPath: {0}", JavaSdkPath);
			Log.LogDebugMessage ("  MonoAndroidBinPath: {0}", MonoAndroidBinPath);
			Log.LogDebugMessage ("  MonoAndroidToolsPath: {0}", MonoAndroidToolsPath);
			Log.LogDebugMessage ("  MonoAndroidIncludePath: {0}", MonoAndroidIncludePath);
			Log.LogDebugMessage ("  TargetFrameworkVersion: {0}", TargetFrameworkVersion);
			Log.LogDebugMessage ("  ZipAlignPath: {0}", ZipAlignPath);
			Log.LogDebugMessage ("  SupportedApiLevel: {0}", SupportedApiLevel);
			Log.LogDebugMessage ("  AndroidSequencePointMode: {0}", AndroidSequencePointsMode);

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
						new XElement ("AndroidSequencePointsMode", AndroidSequencePointsMode.ToString ())
					));
				document.Save (CacheFile);
			}

			//note: this task does not error out if it doesn't find all things. that's the job of the targets
			return !Log.HasLoggedErrors;
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
				SupportedApiLevel       = GetMaxSupportedApiLevel (AndroidApiLevel);
				TargetFrameworkVersion  = GetTargetFrameworkVersionFromApiLevel ();
				return TargetFrameworkVersion != null;
			}

			if (!string.IsNullOrWhiteSpace (AndroidApiLevel)) {
				AndroidApiLevel         = AndroidApiLevel.Trim ();
				SupportedApiLevel       = GetMaxSupportedApiLevel (AndroidApiLevel);
				TargetFrameworkVersion  = GetTargetFrameworkVersionFromApiLevel ();
				return TargetFrameworkVersion != null;
			}

			if (!string.IsNullOrWhiteSpace (TargetFrameworkVersion)) {
				TargetFrameworkVersion  = TargetFrameworkVersion.Trim ();
				string apiLevel = MonoDroidSdk.GetApiLevelForFrameworkVersion (TargetFrameworkVersion);
				if (apiLevel == null) {
					Log.LogCodedError ("XA0000",
							"Could not determine API level for $(TargetFrameworkVersion) of '{0}'.",
							TargetFrameworkVersion);
					return false;
				}
				AndroidApiLevel = apiLevel;
				SupportedApiLevel = apiLevel;
				return true;
			}
			Log.LogCodedError ("XA0000", "Could not determine $(AndroidApiLevel) or $(TargetFrameworkVersion); SHOULD NOT BE REACHED.");
			return false;
		}

		int GetMaxInstalledApiLevel ()
		{
			string platformsDir = Path.Combine (AndroidSdkPath, "platforms");
			var apiLevels = Directory.EnumerateDirectories (platformsDir)
				.Select (platformDir => Path.GetFileName (platformDir))
				.Where (dir => dir.StartsWith ("android-", StringComparison.OrdinalIgnoreCase))
				.Select (dir => dir.Substring ("android-".Length))
				.Select (apiName => MonoAndroidHelper.GetPlatformApiLevel (apiName));
			int maxApiLevel = int.MinValue;
			foreach (var level in apiLevels) {
				int v;
				if (!int.TryParse (level, NumberStyles.Integer, CultureInfo.InvariantCulture, out v))
					continue;
				maxApiLevel = Math.Max (maxApiLevel, v);
			}
			if (maxApiLevel < 0)
				Log.LogCodedError ("XA5300",
						"No Android platforms installed at '{0}'. Please install an SDK Platform with the `{1}{2}tools{2}{3}` program.",
						platformsDir, Path.DirectorySeparatorChar, Android);
			return maxApiLevel;
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
					string tfv = MonoDroidSdk.GetFrameworkVersionForApiLevel (l.ToString ());
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
			string targetFramework = MonoDroidSdk.GetFrameworkVersionForApiLevel (SupportedApiLevel) ??
				MonoDroidSdk.GetFrameworkVersionForApiLevel (AndroidApiLevel);
			if (targetFramework != null)
				return targetFramework;
			Log.LogCodedError ("XA0000",
					"Could not determine $(TargetFrameworkVersion) for API level '{0}.'",
					AndroidApiLevel);
			return null;
		}
	}
}

