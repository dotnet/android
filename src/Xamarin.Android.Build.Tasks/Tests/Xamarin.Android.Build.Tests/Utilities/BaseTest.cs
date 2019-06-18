﻿using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Xamarin.ProjectTools;
using XABuildPaths = Xamarin.Android.Build.Paths;

namespace Xamarin.Android.Build.Tests
{
	public class BaseTest
	{
		public static ConcurrentDictionary<string, string> TestOutputDirectories = new ConcurrentDictionary<string, string> ();

		[SetUpFixture]
		public class SetUp {
			public static bool HasDevices {
				get;
				private set;
			}

			public static string DeviceAbi {
				get;
				private set;
			}

			public static bool CommercialBuildAvailable {
				get;
				private set;
			}

			public static string AndroidMSBuildDirectory {
				get;
				private set;
			}

			public static bool IsMacOS {
				get;
				private set;
			}

			public static bool IsLinux {
				get;
				private set;
			}

			public static bool IsWindows {
				get;
				private set;
			}

			static SetUp ()
			{
				using (var builder = new Builder ()) {
					CommercialBuildAvailable = File.Exists (Path.Combine (builder.AndroidMSBuildDirectory, "Xamarin.Android.Common.Debugging.targets"));
					AndroidMSBuildDirectory = builder.AndroidMSBuildDirectory;
				}
				IsWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
				IsMacOS = !IsWindows && IsDarwin ();
				IsLinux = !IsWindows && !IsMacOS;
			}

			[OneTimeSetUp]
			public void BeforeAllTests ()
			{
				try {
					var adbTarget = Environment.GetEnvironmentVariable ("ADB_TARGET");
					int sdkVersion = -1;
					var result = RunAdbCommand ($"{adbTarget} shell getprop ro.build.version.sdk");
					if (result.Contains ("*")) {
						//NOTE: We may get a result of:
						//
						//27* daemon not running; starting now at tcp:5037
						//* daemon started successfully
						result = result.Split ('*').First ().Trim ();
					}
					HasDevices = int.TryParse (result, out sdkVersion) && sdkVersion != -1;
					if (HasDevices) {
						if (sdkVersion >= 21)
							DeviceAbi = RunAdbCommand ("shell getprop ro.product.cpu.abilist64").Trim ();

						if (string.IsNullOrEmpty (DeviceAbi))
							DeviceAbi = RunAdbCommand ("shell getprop ro.product.cpu.abi") ?? RunAdbCommand ("shell getprop ro.product.cpu.abi2");
					}
				} catch (Exception ex) {
					Console.Error.WriteLine ("Failed to determine whether there is Android target emulator or not: " + ex);
				}
			}

			[OneTimeTearDown]
			public void AfterAllTests ()
			{
				if (System.Diagnostics.Debugger.IsAttached)
					return;

				//NOTE: adb.exe can cause a couple issues on Windows
				//	1) it holds a lock on ~/android-toolchain, so a future build that needs to delete/recreate would fail
				//	2) the MSBuild <Exec /> task *can* hang until adb.exe exits

				try {
					RunAdbCommand ("kill-server", true);
				} catch (Exception ex) {
					Console.Error.WriteLine ("Failed to run adb kill-server: " + ex);
				}

				//NOTE: in case `adb kill-server` fails, kill the process as a last resort
				foreach (var p in Process.GetProcessesByName ("adb.exe"))
					p.Kill ();
			}

			[DllImport ("libc")]
			static extern int uname (IntPtr buf);

			static bool IsDarwin ()
			{
				IntPtr buf = IntPtr.Zero;
				try {
					buf = Marshal.AllocHGlobal (8192);
					if (uname (buf) == 0) {
						string os = Marshal.PtrToStringAnsi (buf);
						return os == "Darwin";
					}
				} catch {
				} finally {
					if (buf != IntPtr.Zero)
						Marshal.FreeHGlobal (buf);
				}
				return false;
			}
		}

		protected bool HasDevices => SetUp.HasDevices;

		protected string DeviceAbi => SetUp.DeviceAbi;

		protected bool IsWindows => SetUp.IsWindows;

		protected bool IsMacOS => SetUp.IsMacOS;

		protected bool IsLinux => SetUp.IsLinux;

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
				return Path.GetFullPath (XABuildPaths.TestOutputDirectory);
			}
		}

		public static bool CommercialBuildAvailable => SetUp.CommercialBuildAvailable;

		public static string AndroidMSBuildDirectory => SetUp.AndroidMSBuildDirectory;

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
				var home = Environment.GetFolderPath (Environment.SpecialFolder.UserProfile);
				var sdkPath = Environment.GetEnvironmentVariable ("ANDROID_SDK_PATH");
				if (string.IsNullOrEmpty (sdkPath))
					sdkPath = Path.Combine (home, "android-toolchain", "sdk");
				return sdkPath;
			}
		}

		public static string AndroidNdkPath {
			get {
				var home = Environment.GetFolderPath (Environment.SpecialFolder.UserProfile);
				var sdkPath = Environment.GetEnvironmentVariable ("ANDROID_NDK_PATH");
				if (string.IsNullOrEmpty (sdkPath))
					sdkPath = Path.Combine (home, "android-toolchain", "ndk");
				return sdkPath;
			}
		}

		protected static void WaitFor(int milliseconds)
		{
			var pause = new ManualResetEvent(false);
			pause.WaitOne(milliseconds);
		}

		protected static string RunAdbCommand (string command, bool ignoreErrors = true, int timeout = 30)
		{
			string ext = Environment.OSVersion.Platform != PlatformID.Unix ? ".exe" : "";
			string adb = Path.Combine (AndroidSdkPath, "platform-tools", "adb" + ext);
			return RunProcess (adb, command);
		}

		protected static string RunProcess (string exe, string args)
		{
			var info = new ProcessStartInfo (exe, args) {
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden,
			};
			using (var proc = Process.Start (info)) {
				if (!proc.WaitForExit ((int)TimeSpan.FromSeconds (30).TotalMilliseconds)) {
					proc.Kill ();
					TestContext.Out.WriteLine ($"{nameof (RunProcess)} timed out: {exe} {args}");
					return null; //Don't try to read stdout/stderr
				}
				var result = proc.StandardOutput.ReadToEnd ().Trim () + proc.StandardError.ReadToEnd ().Trim ();
				return result;
			}
		}

		protected string CreateFauxAndroidNdkDirectory (string path)
		{
			var androidNdkDirectory = Path.Combine (Root, path);
			Directory.CreateDirectory (androidNdkDirectory);
			Directory.CreateDirectory (Path.Combine (androidNdkDirectory, "toolchains"));
			var sb  = new StringBuilder ();
			if (IsWindows) {
				sb.AppendLine ("@echo off");
				sb.AppendLine ($"echo GNU Make 3.81");
			} else {
				sb.AppendLine ("#!/bin/bash");
				sb.AppendLine ($"echo \"GNU Make 3.81\"");
			}
			CreateFauxExecutable (Path.Combine (androidNdkDirectory, IsWindows ? "ndk-build.cmd" : "ndk-build"), sb);
			sb.Clear();
			if (IsWindows) {
				sb.AppendLine("@echo off");
			} else {
				sb.AppendLine("#!/bin/bash");
			}
			CreateFauxExecutable (Path.Combine (androidNdkDirectory, IsWindows ? "ndk-stack.cmd" : "ndk-stack"), sb);
			return androidNdkDirectory;
		}

		protected string CreateFauxAndroidSdkDirectory (string path, string buildToolsVersion, ApiInfo [] apiLevels = null)
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
			File.WriteAllText (Path.Combine (androidSdkToolsPath, IsWindows ? "lint.bat" : "lint"), "");

			List<ApiInfo> defaults = new List<ApiInfo> ();
			for (int i = 10; i < 26; i++) {
				defaults.Add (new ApiInfo () {
					Id = i.ToString (),
				});
			}
			foreach (var level in apiLevels ?? defaults.ToArray ()) {
				var dir = Path.Combine (androidSdkPlatformsPath, $"android-{level.Id}");
				Directory.CreateDirectory(dir);
				File.WriteAllText (Path.Combine (dir, "android.jar"), "");
			}
			return androidSdkDirectory;
		}

		public struct ApiInfo {
			public string Id;
			public int Level;
			public string Name;
			public string FrameworkVersion;
			public bool Stable;
		}

		protected string CreateFauxReferencesDirectory (string path, ApiInfo [] versions)
		{
			string referencesDirectory = Path.Combine (Root, path);
			Directory.CreateDirectory (referencesDirectory);
			Directory.CreateDirectory (Path.Combine (referencesDirectory, "MonoAndroid", "v1.0", "RedistList"));
			File.WriteAllText (Path.Combine (referencesDirectory, "MonoAndroid", "v1.0", "mscorlib.dll"), "");
			File.WriteAllText (Path.Combine (referencesDirectory, "MonoAndroid", "v1.0", "RedistList", "FrameworkList.xml"),
				$"<FileList Redist=\"MonoAndroid\" Name=\"Xamarin.Android Base Class Libraries\"></FileList>");
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

		protected string CreateFauxJavaSdkDirectory (string path, string javaVersion, out string javaExe, out string javacExe)
		{
			javaExe = IsWindows ? "Java.cmd" : "java.bash";
			javacExe  = IsWindows ? "Javac.cmd" : "javac.bash";
			var jarSigner = IsWindows ? "jarsigner.exe" : "jarsigner";
			var javaPath = Path.Combine (Root, path);
			var javaBinPath = Path.Combine (javaPath, "bin");
			Directory.CreateDirectory (javaBinPath);

			CreateFauxJavaExe (Path.Combine (javaBinPath, javaExe), javaVersion);
			CreateFauxJavacExe (Path.Combine (javaBinPath, javacExe), javaVersion);

			File.WriteAllText (Path.Combine (javaBinPath, jarSigner), "");
			return javaPath;
		}

		void CreateFauxJavaExe (string javaExeFullPath, string version)
		{
			var sb  = new StringBuilder ();
			if (IsWindows) {
				sb.AppendLine ("@echo off");
				sb.AppendLine ($"echo java version \"{version}\"");
				sb.AppendLine ($"echo Java(TM) SE Runtime Environment (build {version}-b13)");
				sb.AppendLine ($"echo Java HotSpot(TM) 64-Bit Server VM (build 25.101-b13, mixed mode)");
			} else {
				sb.AppendLine ("#!/bin/bash");
				sb.AppendLine ($"echo \"java version \\\"{version}\\\"\"");
				sb.AppendLine ($"echo \"Java(TM) SE Runtime Environment (build {version}-b13)\"");
				sb.AppendLine ($"echo \"Java HotSpot(TM) 64-Bit Server VM (build 25.101-b13, mixed mode)\"");
			}
			CreateFauxExecutable (javaExeFullPath, sb);
		}

		void CreateFauxJavacExe (string javacExeFullPath, string version)
		{
			var sb  = new StringBuilder ();
			if (IsWindows) {
				sb.AppendLine ("@echo off");
				sb.AppendLine ($"echo javac {version}");
			} else {
				sb.AppendLine ("#!/bin/bash");
				sb.AppendLine ($"echo \"javac {version}\"");
			}
			CreateFauxExecutable (javacExeFullPath, sb);
		}

		void CreateFauxExecutable (string exeFullPath, StringBuilder sb) {
			File.WriteAllText (exeFullPath, sb.ToString ());
			if (!IsWindows) {
				RunProcess ("chmod", $"u+x {exeFullPath}");
			}
		}

		protected ProjectBuilder CreateApkBuilder (string directory, bool cleanupAfterSuccessfulBuild = false, bool cleanupOnDispose = false)
		{
			TestOutputDirectories [TestContext.CurrentContext.Test.ID] = Path.Combine (Root, directory);
			return BuildHelper.CreateApkBuilder (directory, cleanupAfterSuccessfulBuild, cleanupOnDispose);
		}

		protected ProjectBuilder CreateDllBuilder (string directory, bool cleanupAfterSuccessfulBuild = false, bool cleanupOnDispose = false)
		{
			TestOutputDirectories [TestContext.CurrentContext.Test.ID] = Path.Combine (Root, directory);
			return BuildHelper.CreateDllBuilder (directory, cleanupAfterSuccessfulBuild, cleanupOnDispose);
		}

		protected bool FileCompare (string file1, string file2)
		{
			if (!File.Exists (file1) || !File.Exists (file2))
				return false;
			using (var stream1 = File.OpenRead (file1)) {
				using (var stream2 = File.OpenRead (file2)) {
					return StreamCompare (stream1, stream2);
				}
			}
		}

		protected bool StreamCompare (Stream stream1, Stream stream2)
		{
			Assert.IsNotNull (stream1, "stream1 of StreamCompare should not be null");
			Assert.IsNotNull (stream2, "stream2 of StreamCompare should not be null");
			byte[] f1 = ReadAllBytesIgnoringLineEndings (stream1);
			byte[] f2 = ReadAllBytesIgnoringLineEndings (stream2);

			var hash = MD5.Create ();
			var f1hash = Convert.ToBase64String (hash.ComputeHash (f1));
			var f2hash = Convert.ToBase64String (hash.ComputeHash (f2));
			return f1hash.Equals (f2hash);
		}

		protected byte[] ReadAllBytesIgnoringLineEndings (Stream stream)
		{
			using (var memoryStream = new MemoryStream ()) {
				int readByte;
				while ((readByte = stream.ReadByte()) != -1) {
					byte b = (byte)readByte;
					if (b != '\r' && b != '\n') {
						memoryStream.WriteByte (b);
					}
				}
 				return memoryStream.ToArray ();
			}
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
			TestContext.Out.Flush ();
		}

		[TearDown]
		protected virtual void CleanupTest ()
		{
			TestContext.Out.WriteLine ($"[TESTLOG] Test {TestName} Complete");
			TestContext.Out.WriteLine ($"[TESTLOG] Test {TestName} Outcome={TestContext.CurrentContext.Result.Outcome.Status}");
			TestContext.Out.Flush ();
			string outputDir = null;
			if (!TestOutputDirectories.TryGetValue (TestContext.CurrentContext.Test.ID, out outputDir))
				return;
			if (System.Diagnostics.Debugger.IsAttached || string.IsNullOrEmpty (outputDir))
					return;
			// find the "root" directory just below "temp" and clean from there because
			// some tests create multiple subdirectories
			var output = Path.GetFullPath (outputDir);
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

