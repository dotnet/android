using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using Xamarin.ProjectTools;
using NUnit.Framework;
using System.Linq;
using System.Threading;
using System.Text;

namespace Xamarin.Android.Build.Tests
{
	public class BaseTest
	{
		static BaseTest ()
		{
			try {
				var adbTarget = Environment.GetEnvironmentVariable ("ADB_TARGET");
				int sdkVersion = -1;
				HasDevices = int.TryParse (RunAdbCommand ($"{adbTarget} shell getprop ro.build.version.sdk").Trim(), out sdkVersion) && sdkVersion != -1;
			} catch (Exception ex) {
				Console.Error.WriteLine ("Failed to determine whether there is Android target emulator or not" + ex);
			}
		}

		public static readonly bool HasDevices;

		protected bool IsWindows {
			get { return Environment.OSVersion.Platform == PlatformID.Win32NT; }
		}

		public string CacheRootPath {
			get {
				return IsWindows ? Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData)
					: Environment.GetFolderPath (Environment.SpecialFolder.Personal);
			}
		}

		public string CachePath {
			get {
				return IsWindows ? Path.Combine (CacheRootPath, "Xamarin")
					: Path.Combine (CacheRootPath, ".local", "share", "Xamarin");
			}
		}

		public string StagingPath {
			get { return Environment.GetFolderPath (Environment.SpecialFolder.MyDocuments); }
		}

		public string Root {
			get {
				return Path.GetDirectoryName (new Uri (typeof (XamarinProject).Assembly.CodeBase).LocalPath);
			}
		}

		char [] invalidChars = { '{', '}', '(', ')', '$', ':', ';', '\"', '\'', ',', '=' };

		public string TestName {
			get {
				var result = TestContext.CurrentContext.Test.Name;
				foreach (var c in invalidChars.Concat (Path.GetInvalidPathChars ().Concat (Path.GetInvalidFileNameChars ()))) {
					result = result.Replace (c, '_');
				}
				return result.Replace ("_", string.Empty);
			}
		}

		public static string AndroidSdkPath {
			get {
				var home = Environment.GetFolderPath (Environment.SpecialFolder.Personal);
				var sdkPath = Environment.GetEnvironmentVariable ("ANDROID_SDK_PATH");
				if (string.IsNullOrEmpty (sdkPath))
					sdkPath = Path.Combine (home, "android-toolchain", "sdk");
				return sdkPath;
			}
		}

		protected void WaitFor(int milliseconds)
		{
			var pause = new ManualResetEvent(false);
			pause.WaitOne(milliseconds);
		}

		protected static string RunAdbCommand (string command, bool ignoreErrors = true)
		{
			string ext = Environment.OSVersion.Platform != PlatformID.Unix ? ".exe" : "";
			string adb = Path.Combine (AndroidSdkPath, "platform-tools", "adb" + ext);
			var proc = System.Diagnostics.Process.Start (new System.Diagnostics.ProcessStartInfo (adb, command) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false });
			if (!proc.WaitForExit ((int)TimeSpan.FromSeconds (30).TotalMilliseconds)) {
				proc.Kill ();
				proc.WaitForExit ();
			}
			var result = proc.StandardOutput.ReadToEnd ().Trim () + proc.StandardError.ReadToEnd ().Trim ();
			return result;
		}

		protected string RunProcess (string exe, string args) {
			var proc = System.Diagnostics.Process.Start (new System.Diagnostics.ProcessStartInfo (exe, args) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false });
			if (!proc.WaitForExit ((int)TimeSpan.FromSeconds(30).TotalMilliseconds)) {
				proc.Kill ();
				proc.WaitForExit ();
			}
			var result = proc.StandardOutput.ReadToEnd ().Trim () + proc.StandardError.ReadToEnd ().Trim ();
			return result;
		}

		protected string CreateFauxAndroidSdkDirectory (string path, string buildToolsVersion, int minApiLevel = 10, int maxApiLevel = 26)
		{
			var androidSdkDirectory = Path.Combine (Root, path);
			var androidSdkToolsPath = Path.Combine (androidSdkDirectory, "tools");
			var androidSdkBinPath = Path.Combine (androidSdkToolsPath, "bin");
			var androidSdkPlatformToolsPath = Path.Combine (androidSdkDirectory, "platform-tools");
			var androidSdkPlatformsPath = Path.Combine (androidSdkDirectory, "platforms");
			var androidSdkBuildToolsPath = Path.Combine (androidSdkDirectory, "build-tools", buildToolsVersion);
			Directory.CreateDirectory (androidSdkDirectory);
			Directory.CreateDirectory (androidSdkToolsPath);
			Directory.CreateDirectory (androidSdkBinPath);
			Directory.CreateDirectory (androidSdkPlatformToolsPath);
			Directory.CreateDirectory (androidSdkPlatformsPath);
			Directory.CreateDirectory (androidSdkBuildToolsPath);

			File.WriteAllText (Path.Combine (androidSdkPlatformToolsPath, IsWindows ? "adb.exe" : "adb"), "");
			File.WriteAllText (Path.Combine (androidSdkBuildToolsPath, IsWindows ? "zipalign.exe" : "zipalign"), "");
			File.WriteAllText (Path.Combine (androidSdkBuildToolsPath, IsWindows ? "aapt.exe" : "aapt"), "");

			for (int i=minApiLevel; i < maxApiLevel; i++) {
				var dir = Path.Combine (androidSdkPlatformsPath, $"android-{i}");
				Directory.CreateDirectory(dir);
				File.WriteAllText (Path.Combine (dir, "android.jar"), "");
			}
			return androidSdkDirectory;
		}

		public struct ApiInfo {
			public int Id;
			public int Level;
			public string Name;
			public string FrameworkVersion;
			public bool Stable;
		}

		protected string CreateFauxReferencesDirectory (string path, ApiInfo [] versions)
		{

			string referencesDirectory = Path.Combine (Root, path);
			Directory.CreateDirectory (referencesDirectory);
			Directory.CreateDirectory (Path.Combine (referencesDirectory, "MonoAndroid", "v1.0"));
			File.WriteAllText (Path.Combine (referencesDirectory, "MonoAndroid", "v1.0", "mscorlib.dll"), "");
			foreach (var v in versions) {
				Directory.CreateDirectory (Path.Combine (referencesDirectory, "MonoAndroid", v.FrameworkVersion));
				Directory.CreateDirectory (Path.Combine (referencesDirectory, "MonoAndroid", v.FrameworkVersion, "RedistList"));
				File.WriteAllText (Path.Combine (referencesDirectory, "MonoAndroid", v.FrameworkVersion, "MonoAndroid.dll"), "");
				File.WriteAllText (Path.Combine (referencesDirectory, "MonoAndroid", v.FrameworkVersion, "AndroidApiInfo.xml"),
					$"<AndroidApiInfo>\n<Id>{v.Id}</Id>\n<Level>{v.Level}</Level>\n<Name>{v.Name}</Name>\n<Version>{v.FrameworkVersion}</Version>\n<Stable>{v.Stable}</Stable>\n</AndroidApiInfo>");
				File.WriteAllText (Path.Combine (referencesDirectory, "MonoAndroid", v.FrameworkVersion, "RedistList", "FrameworkList.xml"),
					$"<FileList Redist=\"MonoAndroid\" Name=\"Xamarin.Android {v.FrameworkVersion} Support\" IncludeFramework=\"v1.0\"></FileList>");
			}
			return referencesDirectory;
		}

		protected string CreateFauxJavaSdkDirectory (string path, string javaVersion, out string javaExe)
		{
			javaExe = IsWindows ? "Java.cmd" : "java.bash";
			var jarSigner = IsWindows ? "jarsigner.exe" : "jarsigner";
			var javaPath = Path.Combine (Root, path);
			var javaBinPath = Path.Combine (javaPath, "bin");
			Directory.CreateDirectory (javaBinPath);
			var sb = new StringBuilder ();
			if (IsWindows) {
				sb.AppendLine ("@echo off");
				sb.AppendLine ($"echo java version \"{javaVersion}\"");
				sb.AppendLine ($"echo Java(TM) SE Runtime Environment (build {javaVersion}-b13)");
				sb.AppendLine ($"echo Java HotSpot(TM) 64-Bit Server VM (build 25.101-b13, mixed mode)");
			} else {
				sb.AppendLine ("#!/bin/bash");
				sb.AppendLine ($"echo \"java version \\\"{javaVersion}\\\"\"");
				sb.AppendLine ($"echo \"Java(TM) SE Runtime Environment (build {javaVersion}-b13)\"");
				sb.AppendLine ($"echo \"Java HotSpot(TM) 64-Bit Server VM (build 25.101-b13, mixed mode)\"");
			}
			
			File.WriteAllText (Path.Combine (javaBinPath, javaExe), sb.ToString ());
			if (!IsWindows) {
				RunProcess ("chmod", $"u+x {Path.Combine (javaBinPath, javaExe)}");
			}
			File.WriteAllText (Path.Combine (javaBinPath, jarSigner), "");
			return javaPath;
		}

		protected ProjectBuilder CreateApkBuilder (string directory, bool cleanupAfterSuccessfulBuild = false, bool cleanupOnDispose = true)
		{
			TestContext.CurrentContext.Test.Properties ["Output"] = new string [] { Path.Combine (Root, directory) };
			return BuildHelper.CreateApkBuilder (directory, cleanupAfterSuccessfulBuild, cleanupOnDispose);
		}

		protected ProjectBuilder CreateDllBuilder (string directory, bool cleanupAfterSuccessfulBuild = false, bool cleanupOnDispose = true)
		{
			TestContext.CurrentContext.Test.Properties ["Output"] = new string [] { Path.Combine (Root, directory) };
			return BuildHelper.CreateDllBuilder (directory, cleanupAfterSuccessfulBuild, cleanupOnDispose);
		}

		[OneTimeSetUp]
		public void FixtureSetup ()
		{
			// Clean the Resource Cache.
			if (string.IsNullOrEmpty (Environment.GetEnvironmentVariable ("BUILD_HOST")))
				return;
			if (Directory.Exists (CachePath)) {
				foreach (var subDir in Directory.GetDirectories (CachePath, "*", SearchOption.TopDirectoryOnly)) {
					// ignore known useful directories.
					if (subDir.EndsWith ("Mono for Android", StringComparison.OrdinalIgnoreCase) ||
						subDir.EndsWith ("Cache", StringComparison.OrdinalIgnoreCase) ||
						subDir.EndsWith ("Log", StringComparison.OrdinalIgnoreCase)
						|| subDir.EndsWith ("Logs", StringComparison.OrdinalIgnoreCase))
						continue;
					Console.WriteLine ("[FixtureSetup] Removing Resource Cache Directory {0}", subDir);
					Directory.Delete (subDir, recursive: true);
				}
			}
		}

		[SetUp]
		public void TestSetup ()
		{
			TestContext.Out.WriteLine ($"[TESTLOG] Test {TestName} Starting");
		}

		[TearDown]
		protected virtual void CleanupTest ()
		{
			TestContext.Out.WriteLine ($"[TESTLOG] Test {TestName} Complete");
			if (System.Diagnostics.Debugger.IsAttached || TestContext.CurrentContext.Test.Properties ["Output"] == null)
					return;
			// find the "root" directory just below "temp" and clean from there because
			// some tests create multiple subdirectories
			var items = (IList)TestContext.CurrentContext.Test.Properties ["Output"];
			if (items.Count == 0)
				return;
			var output = Path.GetFullPath (items[0].ToString ());
			while (!Directory.GetParent (output).Name.EndsWith ("temp", StringComparison.OrdinalIgnoreCase)) {
					output = Directory.GetParent (output).FullName;
			}
			if (!Directory.Exists (output))
				return;
			if (TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Passed || 
			    TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Skipped) {
				FileSystemUtils.SetDirectoryWriteable (output);
				Directory.Delete (output, recursive: true);
			} else {
				foreach (var file in Directory.GetFiles (Path.Combine (output), "build.log", SearchOption.AllDirectories)) {
					TestContext.Out.WriteLine ("*************************************************************************");
					TestContext.Out.WriteLine (file);
					TestContext.Out.WriteLine ();
					using (StreamReader reader = new StreamReader (file)) {
						string line;
						while ((line = reader.ReadLine ()) != null) {
							TestContext.Out.WriteLine (line);
							TestContext.Out.Flush ();
						}
					}
					TestContext.Out.WriteLine ("*************************************************************************");
					TestContext.Out.Flush ();
				}
			}
		}
	}
}

