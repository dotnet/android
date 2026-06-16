// 
// MonoDroidSdk.cs
//  
// Author:
//       Michael Hutchinson <mhutch@xamarin.com>
//       Andreia Gaita <andreia@xamarin.com>
// 
// Copyright (c) 2012 Xamarin Inc
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
using System.IO;
using System.Linq;
using Mono.AndroidTools;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;

using Xamarin.Android.Tools;

namespace Xamarin.AndroidTools
{
	public class MonoDroidSdk
	{
		#region Obsolete
		static MonoDroidSdk oldSdk;

		[Obsolete("Use static MonoDroidSdk members")]
		public MonoDroidSdk (string mfaSdkPath = null, string mfaFrameworkPath = null)
		{
		}

		[Obsolete("Use static MonoDroidSdk members")]
		public static MonoDroidSdk Sdk {
			get {
				if (oldSdk == null)
					oldSdk = new MonoDroidSdk ();
				return oldSdk;
			}
		}

		[Obsolete("Use MonoDroidSdk.SdkPath")]
		public string MonoAndroidSdkPath { get { return MonoDroidSdk.SdkPath; } }

		[Obsolete("Use MonoDroidSdk.BinPath")]
		public string MonoAndroidBinPath { get { return MonoDroidSdk.BinPath; } }

		[Obsolete("Use MonoDroidSdk.RuntimePath")]
		public string MonoAndroidRuntimePath { get { return MonoDroidSdk.RuntimePath; } }

		[Obsolete("Use MonoDroidSdk.FrameworkPath")]
		public string MonoAndroidFrameworkPath { get { return MonoDroidSdk.FrameworkPath; } }

		[Obsolete("Use MonoDroidSdk.GeneratorTool")]
		public string GeneratorExe {
			get { return Path.Combine (MonoAndroidBinPath, Generator); }
		}

		[Obsolete ("Use MonoDroidSdk.SharedRuntimeVersion")]
		public int GetCurrentSharedRuntimeVersion ()
		{
			return MonoDroidSdk.SharedRuntimeVersion;
		}

		[Obsolete ("Use MonoDroidSdk.SharedRuntimeVersion")]
		public int CurrentRuntimeVersion { get { return MonoDroidSdk.SharedRuntimeVersion; } }

		[Obsolete("Do not use.")]
		public static string Generator;

		[Obsolete ("Do not use.")]
		public static string Javac;

		#endregion

		static MonoDroidSdkBase sdk;

		static MonoDroidSdk ()
		{
			#pragma warning disable 618
			Javac = OS.IsWindows ? "javac.exe" : "javac";
			Generator = "generator.exe";
			#pragma warning restore 618
		}

		static MonoDroidSdkBase GetSdk ()
		{
			if (sdk == null) {
				Refresh ();
			}
			return sdk;
		}

		public static void Refresh (string mfaSdkPath, string mfaFrameworkPath)
		{
			Refresh (runtimePath:mfaSdkPath, binPath:mfaSdkPath, bclPath:mfaFrameworkPath);
		}

		public static void Refresh (string runtimePath = null, string binPath = null, string bclPath = null)
		{
			if (OS.IsWindows) {
				sdk = new MonoDroidSdkWindows ();
			} else {
				sdk = new MonoDroidSdkUnix ();
			}

			try {
				sdk.Initialize (runtimePath, binPath, bclPath);
				var v = LoadVersionInfo ();
				if (v != null) {
					VersionString = v.Item1;
					Version = v.Item2;
					if (!string.IsNullOrEmpty (v.Item3))
						VersionString += "." + v.Item3;
				}
				else {
					AndroidLogger.LogWarning (Properties.Resources.XA5300_MonoDroidSdk_XA_Version);
					VersionString = String.Empty;
					Version = null;
					sdk.Reset ();
				}
			} catch (Exception ex) {
				AndroidLogger.LogError (Properties.Resources.XA5300_MonoDroidSdk_Refresh_Exception, ex.ToString ());
			}
		}

		/// <summary>
		/// Refreshes the SDK information if a different version of XA is installed
		/// </summary>
		public static void RefreshIfSdkChanged ()
		{
			var v = LoadVersionInfo ();
			var vs =v?.Item1;
			if (v?.Item3 != null) {
				vs += "." + v.Item3;
			}
			if (v != null && VersionString == vs && Version == v.Item2) {
				return;
			}
			Refresh ();
		}

		[Obsolete ("Do not use.")]
		public static string AdbTool {
			get { return AndroidSdk.AdbExe; }
		}

		[Obsolete ("Do not use.")]
		public static string SdkPath { get { return GetSdk ().SdkPath; } }
		public static string BinPath { get { return GetSdk ().BinPath; } }
		public static string IncludePath { get { return GetSdk ().IncludePath; } }
		public static string RuntimePath { get { return GetSdk ().RuntimePath; } }
		public static string FrameworkPath { get { return GetSdk ().BclPath; } }
		public static string LibrariesPath { get { return GetSdk ().LibrariesPath; } }
		public static int SharedRuntimeVersion { get { return GetSdk ().SharedRuntimeVersion; } }
		public static bool IsInstalled { get { return !string.IsNullOrEmpty (GetSdk ().BinPath); } }

		public static AndroidVersions AndroidVersions { get { return GetSdk ().AndroidVersions; } }

		static readonly string[] sharedRuntimeAbis = new[] { "arm64-v8a", "armeabi-v7a", "x86", "x86_64" };

		public static string[] SharedRuntimeAbis {
			get {
				return sharedRuntimeAbis;
			}
		}
		static readonly string[] sharedRuntimeAndBundleAbis = new [] { "arm64-v8a", "armeabi-v7a", "x86" };

		public static string[] SharedRuntimeAndBundleAbis {
			get {
				return sharedRuntimeAndBundleAbis;
			}
		}

		public static string DefaultAbi {
			get {
				return "armeabi-v7a";
			}
		}

		[Obsolete ("Do not use.")]
		public static string GeneratorToolExe {
			get {
				#pragma warning disable 618
				return Path.Combine (RuntimePath, "generator.exe");
				#pragma warning restore 618
			}
		}

		[Obsolete ("Do not use.")]
		public static string JavaDocToMDocExe {
			get { return Path.Combine (RuntimePath, "javadoc-to-mdoc.exe"); }
		}
		
		[Obsolete ("Do not use.")]
		public static string MDocExe {
			get { return Path.Combine (RuntimePath, "mdoc.exe"); }
		}

		[Obsolete ("Do not use.")]
		public static string GetPlatformRuntimePackage (int apiLevel)
		{
			return Path.Combine (RuntimePath, "platforms", "android-" + apiLevel, "Mono.Android.Platform.apk");
		}

		[Obsolete ("Do not use.")]
		public static int GetPlatformRuntimePackageVersion (int apiLevel)
		{
			string manifest = Path.Combine (RuntimePath, "platforms", "android-" + apiLevel, "Mono.Android.Platform.xml");

			return MonoDroidSdkBase.GetManifestVersion (manifest);
		}

		public static string GetSharedRuntimePackage (bool debug = true, string arch = "")
		{
			if (debug) {
				arch = "";
			}

			string packageName;
			switch (arch) {
			case "armeabi-v7a":
			case "x86":
				packageName = string.Format ("Mono.Android.DebugRuntime-{0}.apk", arch);
				break;
			default:
				packageName = "Mono.Android.DebugRuntime-debug.apk";
				break;
			}
			return Path.Combine (RuntimePath, packageName);
		}

		/// <summary>
		/// Gets all the api levels we are currently supporting (shipping assemblies for)
		/// </summary>
		/// <returns></returns>
		public static int[] SupportedApiLevels {
			get { return GetSupportedApiLevels ().ToArray (); }
		}

		static IEnumerable<int> GetSupportedApiLevels ()
		{
			foreach (var apiLevel in GetSdk ().GetSupportedApiLevels ()) {
				int value;
				if (int.TryParse (apiLevel, out value))
					yield return value;
			}
		}

		[Obsolete ("Do not use.")]
		public static string GetPlatformNativeLibPath (string abi)
		{
			return GetSdk ().GetPlatformNativeLibPath (abi);
		}

		[Obsolete ("Do not use.")]
		public static string GetPlatformNativeLibPath (AndroidTargetArch arch)
		{
			return GetSdk ().GetPlatformNativeLibPath (arch);
		}

		public static Version Version { get; private set; }
		public static string VersionString { get; private set; }

		static Tuple<string,Version, string> LoadVersionInfo ()
		{
			try {
				var sdk = GetSdk ();

				string versionFile = sdk.FindVersionFile ();
				if (versionFile == null) {
#pragma warning disable 0618
					AndroidLogger.LogInfo (null, $"Did not find Xamarin.Android at path {sdk.SdkPath}");
#pragma warning restore 0618
					return null;
				}

				var str = File.ReadAllText (versionFile).Trim ();
				var version = ParseVersion (str);
				if (version < new Version (4, 8)) {
					AndroidLogger.LogInfo (null, "Xamarin.Android version {0} is too old", str);
					return null;
				}

				var versionRev = string.Empty;
				var versionRevisionFile = Path.GetDirectoryName (versionFile);
				versionRevisionFile = Path.Combine (versionRevisionFile, "Version.rev");
				if (File.Exists (versionRevisionFile)) {
					versionRev = File.ReadAllText (versionRevisionFile).Trim ();
				}

				AndroidLogger.LogInfo (null, "Found Xamarin.Android {0}.{1}", str, versionRev);
				return Tuple.Create (str, version, versionRev);
			} catch (Exception ex) {
				AndroidLogger.LogError (Properties.Resources.XA5300_MonoDroidSdk_Refresh_Exception, ex.ToString ());
				return null;
			}
		}

		internal static Version ParseVersion (string versionString)
		{
			if (!TryParseVersion (versionString, out Version version))
				throw new FormatException (String.Format ("Version string '{0}' is not valid.", versionString));
			return version;
		}

		internal static bool TryParseVersion (string versionString, out Version version)
		{
			version = null;

			// More accepting than Version.Parse. Only care about first 3 parts.
			var split = versionString.Trim ().Split ('.');
			if (!TryParseComponent (split, 0, out int major) ||
					!TryParseComponent (split, 1, out int minor) ||
					!TryParseComponent (split, 2, out int build))
				return false;

			version = new Version (major, minor, build);
			return true;
		}

		// A component is valid when it is missing (defaults to 0) or a non-negative Int32.
		static bool TryParseComponent (string [] split, int index, out int value)
		{
			value = 0;
			if (split.Length <= index)
				return true;
			return int.TryParse (split [index], out value) && value >= 0;
		}

		public static bool SupportsSplitApk {
			get {
				return Version >= new Version (4, 14);
			}
		}

		public static bool SupportsAot {
			get {
				return Version >= new Version (5, 0, 99);
			}
		}

		public static bool SupportsMultiDex {
			get {
				return Version >= new Version (5, 0, 99);
			}
		}

		public static bool SupportsProguard {
			get {
				return Version >= new Version (5, 0, 99);
			}
		}

		public static bool SupportsArm64 {
			get {
				return Version >= new Version (5, 0, 99);
			}
		}

		public static string GetApiLevelForFrameworkVersion (string framework)
		{
			return GetSdk ().GetApiLevelForFrameworkVersion (framework);
		}

		public static string GetFrameworkVersionForApiLevel (string apiLevel)
		{
			return GetSdk ().GetFrameworkVersionForApiLevel (apiLevel);
		}

		/// <summary>
		/// Determines if the given apiLevel is supported by an installed Framework
		/// </summary>
		public static bool IsSupportedFrameworkLevel (string apiLevel)
		{
			return GetSdk ().IsSupportedFrameworkLevel (apiLevel);
		}
	}
}
