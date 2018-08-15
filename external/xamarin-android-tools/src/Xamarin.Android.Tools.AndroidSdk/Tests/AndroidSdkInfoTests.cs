using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

using NUnit.Framework;

namespace Xamarin.Android.Tools.Tests
{
	[TestFixture]
	public class AndroidSdkInfoTests
	{
		string      UnixConfigDirOverridePath;
		string      PreferredJdksOverridePath;

		static  readonly    string  GetMacOSMicrosoftJdkPathsOverrideName   = $"GetMacOSMicrosoftJdkPaths jdks override! {typeof (JdkInfo).AssemblyQualifiedName}";
		static  readonly    string  GetUnixConfigDirOverrideName            = $"UnixConfigPath directory override! {typeof (AndroidSdkInfo).AssemblyQualifiedName}";

		[OneTimeSetUp]
		public void FixtureSetUp ()
		{
			UnixConfigDirOverridePath   = Path.GetTempFileName ();
			File.Delete (UnixConfigDirOverridePath);
			Directory.CreateDirectory (UnixConfigDirOverridePath);
			AppDomain.CurrentDomain.SetData (GetUnixConfigDirOverrideName, UnixConfigDirOverridePath);

			PreferredJdksOverridePath   = Path.GetTempFileName ();
			File.Delete (PreferredJdksOverridePath);
			Directory.CreateDirectory (PreferredJdksOverridePath);
			AppDomain.CurrentDomain.SetData (GetMacOSMicrosoftJdkPathsOverrideName, PreferredJdksOverridePath);
		}

		[OneTimeTearDown]
		public void FixtureTearDown ()
		{
			AppDomain.CurrentDomain.SetData (GetMacOSMicrosoftJdkPathsOverrideName, null);
			AppDomain.CurrentDomain.SetData (GetUnixConfigDirOverrideName, null);
			Directory.Delete (UnixConfigDirOverridePath, recursive: true);
			Directory.Delete (PreferredJdksOverridePath, recursive: true);
		}

		[Test]
		public void Constructor_Paths ()
		{
			CreateSdks (out string root, out string jdk, out string ndk, out string sdk);

			var logs    = new StringWriter ();
			Action<TraceLevel, string> logger = (level, message) => {
				logs.WriteLine ($"[{level}] {message}");
			};

			try {
				var info    = new AndroidSdkInfo (logger, androidSdkPath: sdk, androidNdkPath: ndk, javaSdkPath: jdk);

				Assert.AreEqual (ndk, info.AndroidNdkPath,  "AndroidNdkPath not preserved!");
				Assert.AreEqual (sdk, info.AndroidSdkPath,  "AndroidSdkPath not preserved!");
				Assert.AreEqual (jdk, info.JavaSdkPath,     "JavaSdkPath not preserved!");
			}
			finally {
				Directory.Delete (root, recursive: true);
			}
		}

		[Test]
		public void Constructor_SetValuesFromPath ()
		{
			CreateSdks (out string root, out string jdk, out string ndk, out string sdk);
			JdkInfoTests.CreateFauxJdk (jdk, releaseVersion: "1.8.0", releaseBuildNumber: "42", javaVersion: "100.100.100_100");

			Action<TraceLevel, string> logger = (level, message) => {
				Console.WriteLine ($"[{level}] {message}");
			};
			var oldPath = Environment.GetEnvironmentVariable ("PATH");
			try {
				var paths   = new List<string> () {
					Path.Combine (jdk, "bin"),
					ndk,
					Path.Combine (sdk, "platform-tools"),
				};
				paths.AddRange (oldPath.Split (new[]{Path.PathSeparator}, StringSplitOptions.RemoveEmptyEntries));
				Environment.SetEnvironmentVariable ("PATH", string.Join (Path.PathSeparator.ToString (), paths));

				var info    = new AndroidSdkInfo (logger);

				Assert.AreEqual (ndk, info.AndroidNdkPath,  "AndroidNdkPath not set from $PATH!");
				Assert.AreEqual (sdk, info.AndroidSdkPath,  "AndroidSdkPath not set from $PATH!");
				Assert.AreEqual (jdk, info.JavaSdkPath,     "JavaSdkPath not set from $PATH!");
			}
			finally {
				Environment.SetEnvironmentVariable ("PATH", oldPath);
				Directory.Delete (root, recursive: true);
			}
		}

		static  bool    IsWindows   => OS.IsWindows;

		static void CreateSdks (out string root, out string jdk, out string ndk, out string sdk)
		{
			root    = Path.GetTempFileName ();
			File.Delete (root);
			Directory.CreateDirectory (root);

			ndk     = Path.Combine (root, "ndk");
			sdk     = Path.Combine (root, "sdk");
			jdk     = Path.Combine (root, "jdk");

			Directory.CreateDirectory (sdk);
			Directory.CreateDirectory (ndk);
			Directory.CreateDirectory (jdk);

			CreateFauxAndroidSdkDirectory (sdk, "26.0.0");
			CreateFauxAndroidNdkDirectory (ndk);
			CreateFauxJavaSdkDirectory (jdk, "1.8.0", out var _, out var _);
		}

		static void CreateFauxAndroidSdkDirectory (string androidSdkDirectory, string buildToolsVersion, ApiInfo [] apiLevels = null)
		{
			var androidSdkToolsPath             = Path.Combine (androidSdkDirectory, "tools");
			var androidSdkBinPath               = Path.Combine (androidSdkToolsPath, "bin");
			var androidSdkPlatformToolsPath     = Path.Combine (androidSdkDirectory, "platform-tools");
			var androidSdkPlatformsPath         = Path.Combine (androidSdkDirectory, "platforms");
			var androidSdkBuildToolsPath        = Path.Combine (androidSdkDirectory, "build-tools", buildToolsVersion);

			Directory.CreateDirectory (androidSdkDirectory);
			Directory.CreateDirectory (androidSdkToolsPath);
			Directory.CreateDirectory (androidSdkBinPath);
			Directory.CreateDirectory (androidSdkPlatformToolsPath);
			Directory.CreateDirectory (androidSdkPlatformsPath);
			Directory.CreateDirectory (androidSdkBuildToolsPath);

			File.WriteAllText (Path.Combine (androidSdkPlatformToolsPath,   IsWindows ? "adb.exe" : "adb"),             "");
			File.WriteAllText (Path.Combine (androidSdkBuildToolsPath,      IsWindows ? "zipalign.exe" : "zipalign"),   "");
			File.WriteAllText (Path.Combine (androidSdkBuildToolsPath,      IsWindows ? "aapt.exe" : "aapt"),           "");
			File.WriteAllText (Path.Combine (androidSdkToolsPath,           IsWindows ? "lint.bat" : "lint"),           "");

			List<ApiInfo> defaults = new List<ApiInfo> ();
			for (int i = 10; i < 26; i++) {
				defaults.Add (new ApiInfo () {
					Id = i.ToString (),
				});
			}
			foreach (var level in ((IEnumerable<ApiInfo>) apiLevels) ?? defaults) {
				var dir = Path.Combine (androidSdkPlatformsPath, $"android-{level.Id}");
				Directory.CreateDirectory(dir);
				File.WriteAllText (Path.Combine (dir, "android.jar"), "");
			}
		}

		struct ApiInfo {
			public  string      Id;
		}

		static void CreateFauxAndroidNdkDirectory (string androidNdkDirectory)
		{
			File.WriteAllText (Path.Combine (androidNdkDirectory, "ndk-stack"),     "");
			File.WriteAllText (Path.Combine (androidNdkDirectory, "ndk-stack.cmd"), "");

			string prebuiltHostName = "";
			if (OS.IsWindows)
				prebuiltHostName    = "windows-x86_64";
			else if (OS.IsMac)
				prebuiltHostName    = "darwin-x86_64";
			else
				prebuiltHostName    = "linux-x86_64";


			var toolchainsDir   = Path.Combine (androidNdkDirectory, "toolchains");
			var armToolchainDir = Path.Combine (toolchainsDir, "arm-linux-androideabi-4.9");
			var armPrebuiltDir  = Path.Combine (armToolchainDir, "prebuilt", prebuiltHostName);

			Directory.CreateDirectory (armPrebuiltDir);
		}

		static string CreateFauxJavaSdkDirectory (string javaPath, string javaVersion, out string javaExe, out string javacExe)
		{
			javaExe             = IsWindows ? "Java.cmd" : "java.bash";
			javacExe            = IsWindows ? "Javac.cmd" : "javac.bash";
			var jarSigner       = IsWindows ? "jarsigner.exe" : "jarsigner";
			var javaBinPath     = Path.Combine (javaPath, "bin");

			Directory.CreateDirectory (javaBinPath);

			CreateFauxJavaExe (Path.Combine (javaBinPath, javaExe), javaVersion);
			CreateFauxJavacExe (Path.Combine (javaBinPath, javacExe), javaVersion);

			File.WriteAllText (Path.Combine (javaBinPath, jarSigner), "");
			return javaPath;
		}

		static void CreateFauxJavaExe (string javaExeFullPath, string version)
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
			File.WriteAllText (javaExeFullPath, sb.ToString ());
			if (!IsWindows) {
				Exec ("chmod", $"u+x \"{javaExeFullPath}\"");
			}
		}

		static void CreateFauxJavacExe (string javacExeFullPath, string version)
		{
			var sb  = new StringBuilder ();
			if (IsWindows) {
				sb.AppendLine ("@echo off");
				sb.AppendLine ($"echo javac {version}");
			} else {
				sb.AppendLine ("#!/bin/bash");
				sb.AppendLine ($"echo \"javac {version}\"");
			}
			File.WriteAllText (javacExeFullPath, sb.ToString ());
			if (!IsWindows) {
				Exec ("chmod", $"u+x \"{javacExeFullPath}\"");
			}
		}

		static void Exec (string exe, string args)
		{
			var psi     = new ProcessStartInfo {
				FileName                = exe,
				Arguments               = args,
				UseShellExecute         = false,
				RedirectStandardInput   = false,
				RedirectStandardOutput  = false,
				RedirectStandardError   = false,
				CreateNoWindow          = true,
				WindowStyle             = ProcessWindowStyle.Hidden,

			};
			var proc    = Process.Start (psi);
			if (!proc.WaitForExit ((int) TimeSpan.FromSeconds(30).TotalMilliseconds)) {
				proc.Kill ();
				proc.WaitForExit ();
			}
		}

		[Test]
		public void DetectAndSetPreferredJavaSdkPathToLatest ()
		{
			Action<TraceLevel, string> logger = (level, message) => {
				Console.WriteLine ($"[{level}] {message}");
			};

			var backupConfig    = UnixConfigPath + "." + Path.GetRandomFileName ();
			try {
				if (OS.IsWindows) {
					Assert.Throws<NotImplementedException>(() => AndroidSdkInfo.DetectAndSetPreferredJavaSdkPathToLatest (logger));
					return;
				}
				Assert.Throws<NotSupportedException>(() => AndroidSdkInfo.DetectAndSetPreferredJavaSdkPathToLatest (logger));
				var newJdkPath  = Path.Combine (PreferredJdksOverridePath, "microsoft_dist_openjdk_1.8.999.9");
				JdkInfoTests.CreateFauxJdk (newJdkPath, releaseVersion: "1.8.999", releaseBuildNumber: "9", javaVersion: "1.8.999-9");

				if (File.Exists (UnixConfigPath))
					File.Move (UnixConfigPath, backupConfig);

				AndroidSdkInfo.DetectAndSetPreferredJavaSdkPathToLatest (logger);
				AssertJdkPath (newJdkPath);
			}
			finally {
				if (File.Exists (backupConfig)) {
					File.Delete (UnixConfigPath);
					File.Move (backupConfig, UnixConfigPath);
				}
			}
		}

		void AssertJdkPath (string expectedJdkPath)
		{
			var config_file     = XDocument.Load (UnixConfigPath);
			var javaEl          = config_file.Root.Element ("java-sdk");
			var actualJdkPath   = (string) javaEl.Attribute ("path");

			Assert.AreEqual (expectedJdkPath, actualJdkPath);
		}

		string UnixConfigPath {
			get {
				return Path.Combine (UnixConfigDirOverridePath, "monodroid-config.xml");
			}
		}
	}
}
