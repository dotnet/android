// 
// AndroidSdk.cs
//  
// Authors:
//       Jonathan Pobst <jpobst@xamarin.com>
//       Andreia Gaita <andreia@xamarin.com>
//       Michael Hutchinson <mhutch@xamarin.com>
// 
// Copyright 2012 Xamarin Inc. All rights reserved.
// 

using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Mono.AndroidTools;
using Xamarin.AndroidTools.Utilities;

using Xamarin.Android.Tools;

using NewVersion    = Xamarin.Android.Tools.AndroidVersion;

namespace Xamarin.AndroidTools
{
	public class AndroidSdk
	{
		private static AndroidSdkInfo sdk;

		public const string AutoRefreshSwitch = "Xamarin.AndroidTools.AndroidSdk.AutoRefresh";

		static AndroidSdk ()
		{
			var pathExt     = Environment.GetEnvironmentVariable ("PATHEXT");
			var pathExts    = pathExt?.Split (new char [] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries);
			ExeExtensions   = new string [(pathExts?.Length ?? 0) + 1];
			if (pathExts != null) {
				Array.Copy (pathExts, 0, ExeExtensions, 0, pathExts.Length);
			}
			ExeExtensions [ExeExtensions.Length - 1] = null;

			// Return early if AutoRefreshSwitch is false
			if (AppContext.TryGetSwitch (AutoRefreshSwitch, out var enabled) && !enabled) {
				return;
			}

			// Run Refresh if AutoRefreshSwitch is not set or true
			Refresh ();
		}

		static  readonly    string[]    ExeExtensions;

		public static void Refresh ()
		{
			Refresh (null, null, null);
		}

#pragma warning disable CS0618 // Type or member is obsolete

		public static void Refresh (string androidSdkPath = null, string androidNdkPath = null, string javaSdkPath = null)
		{
			try {
				sdk = new AndroidSdkInfo (Logger, androidSdkPath, androidNdkPath, javaSdkPath);

				if (AnalyticsService.IsRegistered)
					SendTelemetryEventAsync ();

				AndroidPlatformToolsPath    = Path.Combine(AndroidSdkPath, "platform-tools");
				AndroidToolsPath            = Path.Combine (AndroidSdkPath, "tools");
				AndroidEmulatorPath         = Path.Combine (AndroidSdkPath, "emulator");
				JavaBinPath                 = Path.Combine (JavaSdkPath, "bin");

				AdbExe                      = FindExecutableInDirectory ("adb", AndroidPlatformToolsPath).FirstOrDefault ();
				AndroidExe                  = FindExecutableInDirectory ("android", AndroidToolsPath).FirstOrDefault ();
				EmulatorExe                 = FindExecutableInDirectory ("emulator", AndroidEmulatorPath).FirstOrDefault () ?? FindExecutableInDirectory ("emulator", AndroidToolsPath).FirstOrDefault ();
				JarsignerExe                = FindExecutableInDirectory ("jarsigner", JavaBinPath).FirstOrDefault ();
				JavaExe                     = FindExecutableInDirectory ("java", JavaBinPath).FirstOrDefault ();
				JavacExe                    = FindExecutableInDirectory ("javac", JavaBinPath).FirstOrDefault ();
				KeyToolExe                  = FindExecutableInDirectory ("keytool", JavaBinPath).FirstOrDefault ();
				MonitorExe                  = FindExecutableInDirectory ("monitor", AndroidToolsPath).FirstOrDefault ();
			}
			catch (Exception ex) {
				sdk = null;
				AndroidPlatformToolsPath = AndroidToolsPath = JavaBinPath = null;

				AdbExe = AndroidExe = EmulatorExe = JarsignerExe = JavacExe = KeyToolExe = MonitorExe = null;
				if (ex is InvalidOperationException && ex.Message.Contains (" Android "))
					AndroidLogger.LogError (Properties.Resources.XA5300_Android_SDK);
				else if (ex is InvalidOperationException && ex.Message.Contains (" Java "))
					AndroidLogger.LogError (Properties.Resources.XA5300_Java_SDK);
				else
					AndroidLogger.LogError (Properties.Resources.XA5300_AndroidSdk_Refresh_Exception, ex.ToString ());
			}
		}

#pragma warning restore CS0618

		static System.Threading.Tasks.Task SendTelemetryEventAsync()
		{
			return System.Threading.Tasks.Task.Run(() => {
				var telemetryProperties = new Dictionary<string, object>();

				telemetryProperties["XS.Core.SDK.Android.Version"] = MonoDroidSdk.VersionString;

				TrackApiLevels(telemetryProperties);
				TrackJdkInfo(telemetryProperties);

				AnalyticsService.ReportSdkVersions(telemetryProperties);
			});
		}

		static void Logger (TraceLevel level, string value)
		{
			switch (level) {
			case TraceLevel.Error:
				AndroidLogger.LogError (null, "{0}", value);
				break;
			case TraceLevel.Info:
				AndroidLogger.LogInfo (null, "{0}", value);
				break;
			case TraceLevel.Warning:
				AndroidLogger.LogWarning (null, "{0}", value);
				break;
			case TraceLevel.Verbose:
			default:
				AndroidLogger.LogDebug (null, "{0}", value);
				break;
			}
		}

		public static bool IsInstalled {
			get {
				return !string.IsNullOrEmpty (AndroidSdkPath) && IsJdkInstalled;
			}
		}

		public static bool IsJdkInstalled {
			get {
				return !string.IsNullOrEmpty (JavaSdkPath);
			}
		}

		[Obsolete ("Use OS.IsWindows")]
		public static bool IsWindows {
			get { return OS.IsWindows; }
		}

		[Obsolete ("Use OS.IsMac")]
		public static bool IsMac {
			get { return OS.IsMac; }
		}

		public static string AndroidSdkPath {
			get { return sdk?.AndroidSdkPath; }
		}

		[Obsolete]
		public static string[] AllAndroidSdkPaths {
			get {
				return sdk?.AllAndroidSdkPaths ?? new string [0];
			}
		}

		public static string AndroidNdkPath {
			get { return sdk?.AndroidNdkPath; }
		}

		public static string AndroidNdkHostPlatform {
			get { return sdk?.AndroidNdkHostPlatform; }
		}

		[Obsolete]
		public static string[] AllAndroidNdkPaths {
			get {
				return new string [0];
			}
		}

		public static string JavaSdkPath {
			get { return sdk?.JavaSdkPath; }
		}

		public static string JavaBinPath {
			get;
			private set;
		}

		[Obsolete ("Use GetCommandLineToolsPaths().")]
		public static string AndroidToolsPath {
			get;
			private set;
		}

		public static string AndroidEmulatorPath {
			get;
			private set;
		}

		public static string AndroidPlatformToolsPath {
			get;
			private set;
		}

		/// <summary>The value of the PATH environment variable to be used when running SDK executables.</summary>
		public static string GetEnvironmentPathOverride ()
		{
			if (string.IsNullOrEmpty (JavaBinPath))
				return null;

			var pathEnv = Environment.GetEnvironmentVariable ("PATH");
			if (pathEnv != null)
				return JavaBinPath + Path.PathSeparator + pathEnv;

			return JavaBinPath;
		}

		static string GetShortPathName (string path)
		{
			if (OS.IsWindows)
				return KernelEx.GetShortPathName (path);
			return path;
		}

		static IEnumerable<string> FindExecutableInDirectory (string executable, string dir)
		{
			foreach (var exe in Executables (executable)) {
				var e = Path.Combine (dir, exe);
				if (File.Exists (e))
					yield return e;
			}
		}

		static IEnumerable<string> Executables (string executable)
		{
			foreach (var ext in ExeExtensions) {
				yield return Path.ChangeExtension (executable, ext);
			}
		}

		/// <summary>
		/// The path and executable for adb[.exe].
		/// </summary>
		public static string AdbExe {
			get;
			private set;
		}

		/// <summary>
		/// The path and executable for android[.bat].
		/// </summary>
		[Obsolete ("Part of obsolete `tools` package, do not use.")]
		public static string AndroidExe {
			get;
			private set;
		}

		/// <summary>
		/// The path and executable for monitor.exe/monitor.
		/// </summary>
		[Obsolete ("Part of obsolete `tools` package, do not use.")]
		public static string MonitorExe {
			get;
			private set;
		}

		/// <summary>
		/// The path and executable for emulator.exe/android.
		/// </summary>
		public static string EmulatorExe {
			get;
			private set;
		}

		public static string ZipAlignExe {
			get { return GetZipAlignPath (); }
		}

		[Obsolete ("Use ApkSignerJar. This returns a path to apksigner.jar.")]
		public static string ApkSignerExe => GetApkSignerPath ();

		public static string ApkSignerJar => GetApkSignerPath ();

		public static string JarsignerExe {
			get;
			private set;
		}

		public static string KeyToolExe {
			get;
			private set;
		}

		public static string JavaExe {
			get;
			private set;
		}

		public static string JavacExe {
			get;
			private set;
		}

		/// <summary>
		/// Gets the sdk tools version from <AndroidSdkPath>\tools\source.properties
		/// Returns null if the file was not found.
		/// </summary>
		[Obsolete ("The `tools` package is obsolete.  Use " + nameof (GetCommandLineToolsVersion) + "(string)")]
		public static string SdkToolsVersion {
			get {
				return GetRevisionFromSdkPackageDirectory (Path.Combine (AndroidSdkPath, "tools"));
			}
		}

		/// <summary>
		/// Gets the sdk platform tools version from <AndroidSdkPath>\platform-tools\source.properties.
		/// Returns null if the file was not found.
		/// </summary>
		public static string SdkPlatformToolsVersion {
			get {
				return GetRevisionFromSdkPackageDirectory (Path.Combine (AndroidSdkPath, "platform-tools"));
			}
		}

		/// <summary>
		/// Gets the sdk build tools version from <AndroidSdkPath>\build-tools\<tools-dir>\source.properties.
		/// Returns null if the file was not found.
		/// </summary>
		public static string SdkBuildToolsVersion {
			get {
				if (AndroidSdkPath == null)
					return null;

				var buildToolsDir = Path.Combine (AndroidSdkPath, "build-tools");
				if (Directory.Exists (buildToolsDir)) {
					var toolsDirs = Directory.GetDirectories (buildToolsDir);

					var versions = (from dir in toolsDirs
					                let v = GetRevisionFromSdkPackageDirectory (dir)
					                let version = ParseBuildToolsVersion (v)
					                orderby version
						select v).ToList ();

					return versions.LastOrDefault ();
				}

				return null;
			}
		}

		/// <summary>
		/// Gets the sdk cmdline-tools version from <AndroidSdkPath>\cmdline-tools\<tools-dir>\source.properties.
		/// </summary>
		/// <param name="commandLineToolsPath">The full path to the cmdline-tools\version folder.</param>
		/// <returns>The version of the specified cmdline-tools path, or null if the folder or file was not found.</returns>
		public static string GetCommandLineToolsVersion (string commandLineToolsPath)
		{
			return GetRevisionFromSdkPackageDirectory (commandLineToolsPath);
		}

		static string GetRevisionFromSdkPackageDirectory (string sdkPackageDirectory)
		{
			if (!Directory.Exists (sdkPackageDirectory))
				return null;

			return SdkBuildProperties.LoadProperties (Path.Combine (sdkPackageDirectory, "source.properties")).GetPropertyValue ("Pkg.Revision=");
		}

		static void TrackJdkInfo(Dictionary<string, object> telemetryProperties)
		{
			var props = default(JdkProperties);

			try {
				props = JdkProperties.Get(JavaSdkPath);
			} catch (Exception ex)
			{
				AndroidLogger.LogError(Properties.Resources.XA5300_AndroidSdk_JdkInfo, ex.ToString ());
			}

			telemetryProperties["Xamarin.Core.SDK.Android.JDK.Vendor"] = props?.Vendor;
			telemetryProperties["Xamarin.Core.SDK.Android.JDK.Version"] = props?.Version;
		}

		static void TrackApiLevels (Dictionary<string, object> telemetryProperties)
		{
			List<NewVersion> versions;
			if (IsInstalled && MonoDroidSdk.AndroidVersions != null) {
				versions = GetInstalledPlatformVersions (MonoDroidSdk.AndroidVersions).ToList ();
			}
			else {
				versions = new List<NewVersion> ();
			}

			telemetryProperties ["Xamarin.Core.SDK.Android.Api.Count"] = versions.Count.ToString ();
			if (versions.Count == 0) {
				telemetryProperties ["Xamarin.Core.SDK.Android.Api.Levels"] = string.Empty;
			}
			else {
				var sb = new System.Text.StringBuilder ();

				for (int i = 0; i < versions.Count; i++) {
					sb.Append (versions [i].ApiLevel.ToString ());
					if (versions.Count > 1 && i < versions.Count - 1) {
						sb.Append ("|");
					}
				}

				telemetryProperties ["Xamarin.Core.SDK.Android.Api.Levels"] = sb.ToString ();
			}
		}

		static AndroidBuildToolsVersion ParseBuildToolsVersion (string input)
		{
			AndroidBuildToolsVersion version = null;
			if (AndroidBuildToolsVersion.TryParse (input, out version))
				return version;

			return new AndroidBuildToolsVersion (0, 0, 0, input ?? "");
		}

		static string ValidatePath (string path)
		{
			if (String.IsNullOrEmpty (path))
				throw new InvalidOperationException ("This property is not valid when the SDK is not installed");
			return path;
		}
		
		public static void SetPreferredAndroidSdkPath (string path)
		{
			AndroidSdkInfo.SetPreferredAndroidSdkPath (path);
			
			// Update everything to use new path
			Refresh ();
		}

		public static void SetPreferredJavaSdkPath (string path)
		{
			AndroidSdkInfo.SetPreferredJavaSdkPath (path);

			// Update everything to use new path
			Refresh ();
		}

		public static void SetPreferredAndroidNdkPath (string path)
		{
			AndroidSdkInfo.SetPreferredAndroidNdkPath (path);

			// Update everything to use new path
			Refresh ();
		}

		/// <summary>
		/// Checks that a value is the location of an Android SDK.
		/// </summary>
		public static bool ValidateAndroidSdkLocation (string loc)
		{
			return !string.IsNullOrEmpty (loc) &&
				loc.IndexOfAny (Path.GetInvalidPathChars ()) == -1 &&
				FindExecutableInDirectory ("adb", Path.Combine (loc, "platform-tools")).Any ();
		}

		/// <summary>
		/// Checks that a value is the location of a Java SDK.
		/// </summary>
		public static bool ValidateJavaSdkLocation (string loc)
		{
			return !string.IsNullOrEmpty (loc) &&
				loc.IndexOfAny (Path.GetInvalidPathChars ()) == -1 &&
				FindExecutableInDirectory ("jarsigner", Path.Combine (loc, "bin")).Any ();
		}

		/// <summary>
		/// Checks that a value is the location of an Android NDK.
		/// </summary>
		public static bool ValidateAndroidNdkLocation (string loc)
		{
			return !string.IsNullOrEmpty (loc) &&
				loc.IndexOfAny (Path.GetInvalidPathChars ()) == -1 &&
				FindExecutableInDirectory ("ndk-stack", loc).Any ();
		}

		public static string GetLatestPlatformDirectory ()
		{
			var platformsDir = Path.Combine (AndroidSdkPath, "platforms");
			if (Directory.Exists (platformsDir)) {
				var dirs =
					from p in Directory.EnumerateDirectories (platformsDir, "android-*")
					let version = ToInt32 (Path.GetFileName (p).Substring ("android-".Length))
					orderby version descending
					select new {
						Path    = p,
						Version = version,
					};
				foreach (var d in dirs)
					return d.Path;
			}
			throw new InvalidOperationException ("Could not find latest API level in: " + AndroidSdkPath);
		}

		static int? ToInt32 (string value)
		{
			int v;
			if (int.TryParse (value, out v))
				return v;
			return null;
		}

		public static string GetPlatformDirectory (int apiLevel)
		{
			return sdk?.GetPlatformDirectory (apiLevel);
		}

		public static string GetPlatformDirectory (string osVersion)
		{
			return sdk?.TryGetPlatformDirectoryFromApiLevel (osVersion, MonoDroidSdk.AndroidVersions);
		}

		public static bool IsPlatformInstalled (string osVersion)
		{
			if (sdk == null || MonoDroidSdk.AndroidVersions == null)
				return false;
			var id  = MonoDroidSdk.AndroidVersions.GetIdFromFrameworkVersion (osVersion);
			return sdk.TryGetPlatformDirectoryFromApiLevel (id, MonoDroidSdk.AndroidVersions) != null;
		}

		public static bool IsPlatformInstalled (int apiLevel)
		{
			return apiLevel != 0 && Directory.Exists (GetPlatformDirectory (apiLevel));
		}

		[Obsolete ("Use " + nameof (GetInstalledPlatformVersions) + "(AndroidVersions)")]
		public static IEnumerable<AndroidVersion> GetInstalledPlatformVersions ()
		{
			var knownAndInstalledSdkLevels = AndroidVersion.KnownVersions.Where (v => IsPlatformInstalled (v.ApiLevel));

			return knownAndInstalledSdkLevels.Where (version => {
				var apiLevel = MonoDroidSdk.GetApiLevelForFrameworkVersion (version.OSVersion);
				return MonoDroidSdk.IsSupportedFrameworkLevel (apiLevel);
			});
		}

		public static IEnumerable<NewVersion> GetInstalledPlatformVersions (AndroidVersions versions)
		{
			return sdk?.GetInstalledPlatformVersions (versions) ?? Enumerable.Empty<NewVersion>();
		}

		static string GetZipAlignPath ()
		{
			var zipAlign = OS.IsWindows ? "zipalign.exe" : "zipalign";
			foreach (var p in GetBuildToolsPaths ()) {
				var app = Path.Combine (p, "bin", zipAlign);
				if (File.Exists (app))
					return app;
				app = Path.Combine (p, zipAlign);
				if (File.Exists (app))
					return app;
			}

			var old = Path.Combine (GetShortPathName (Path.Combine (AndroidSdkPath, "tools")), zipAlign);
			if (File.Exists (old))
				return old;

			return null;
		}

		static string GetApkSignerPath ()
		{
			const string apkSigner = "apksigner.jar";

			foreach (var p in GetBuildToolsPaths ())
			{
				var app = Path.Combine (p, "lib", apkSigner);
				if (File.Exists (app))
					return app;
				app = Path.Combine (p, apkSigner);
				if (File.Exists (app))
					return app;
			}

			var old = Path.Combine (GetShortPathName (Path.Combine (AndroidSdkPath, "tools")), apkSigner);

			if (File.Exists (old))
				return old;

			return null;
		}

		static readonly string apkanalyzer = OS.IsWindows ? "apkanalyzer.exe" : "apkanalyzer";

		public static string GetApkAnalyzerPath () => GetApkAnalyzerPath (preferredCommandLineToolsVersion: null);

		public static string GetApkAnalyzerPath (string preferredCommandLineToolsVersion)
		{
			foreach (var p in GetCommandLineToolsPaths (preferredCommandLineToolsVersion)) {
				var cmdLineToolsapkanalyzerPath = FindExecutableInDirectory ("apkanalyzer", Path.Combine (p, "bin")).FirstOrDefault ();
				if (File.Exists (cmdLineToolsapkanalyzerPath)) {
					return cmdLineToolsapkanalyzerPath;
				}
			}

			return GetFallbackApkAnalyzerPath ();
		}

		static string GetFallbackApkAnalyzerPath ()
		{
			var apkanalyzerPath = Path.Combine (AndroidSdkPath, "tools", "bin", apkanalyzer);

			if (File.Exists (apkanalyzerPath))
				return apkanalyzerPath;

			return null;
		}

		static readonly string aapt = OS.IsWindows ? "aapt.exe" : "aapt";

		public static string GetAaptPath (string preferredBuildToolsVersion = null)
		{
			foreach (var p in GetBuildToolsPaths (preferredBuildToolsVersion)) {
				var app = Path.Combine (p, "bin", aapt);
				if (File.Exists (app))
					return app;
				app = Path.Combine (p, aapt);
				if (File.Exists (app))
					return app;
			}
			return null;
		}

		public static IEnumerable<string> GetCommandLineToolsPaths (string preferredCommandLineToolsVersion = null)
		{
			return sdk?.GetCommandLineToolsPaths (preferredCommandLineToolsVersion) ?? Enumerable.Empty<string> ();
		}

		public static IEnumerable<string> GetBuildToolsPaths (string preferredBuildToolsVersion)
		{
			return sdk?.GetBuildToolsPaths (preferredBuildToolsVersion) ?? Enumerable.Empty<string> ();
		}

		public static IEnumerable<string> GetBuildToolsPaths ()
		{
			return sdk?.GetBuildToolsPaths () ?? Enumerable.Empty<string> ();
		}
	}
}
