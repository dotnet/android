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
		const string NdkVersion = "21.0.6113669";

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
		public void Ndk_MultipleNdkVersionsInSdk ()
		{
			// Must match like-named constants in AndroidSdkBase
			const int MinimumCompatibleNDKMajorVersion = 16;
			const int MaximumCompatibleNDKMajorVersion = 25;

			CreateSdks(out string root, out string jdk, out string ndk, out string sdk);

			Action<TraceLevel, string> logger = (level, message) => {
				Console.WriteLine($"[{level}] {message}");
			};

			var ndkVersions = new List<string> {
				"16.1.4479499",
				"17.2.4988734",
				"18.1.5063045",
				"19.2.5345600",
				"20.0.5594570",
				"20.1.5948944",
				"21.0.6113669",
				"21.1.6352462",
				"21.2.6472646",
				"21.3.6528147",
				"22.0.7026061",
				"22.1.7171670",
				"23.1.7779620",
				"24.0.8215888",
				"25.0.8775105",
				"26.0.3735928559",   // 0xdeadbeef
			};
			string expectedVersion = "25.0.8775105";
			string expectedNdkPath = Path.Combine (sdk, "ndk", expectedVersion);

			try {
				MakeNdkDir (Path.Combine (sdk, "ndk-bundle"), NdkVersion);

				foreach (string ndkVer in ndkVersions) {
					MakeNdkDir (Path.Combine (sdk, "ndk", ndkVer), ndkVer);
				}

				var info = new AndroidSdkInfo (logger, androidSdkPath: sdk, androidNdkPath: null, javaSdkPath: jdk);

				Assert.AreEqual (expectedNdkPath, info.AndroidNdkPath, "AndroidNdkPath not found inside sdk!");

				string ndkVersion = Path.GetFileName (info.AndroidNdkPath);
				if (!Version.TryParse (ndkVersion, out Version ver)) {
					Assert.Fail ($"Unable to parse '{ndkVersion}' as a valid version.");
				}

				Assert.True (ver.Major >= MinimumCompatibleNDKMajorVersion, $"NDK version must be at least {MinimumCompatibleNDKMajorVersion}");
				Assert.True (ver.Major <= MaximumCompatibleNDKMajorVersion, $"NDK version must be at most {MinimumCompatibleNDKMajorVersion}");
			} finally {
				Directory.Delete (root, recursive: true);
			}
		}

		[Test]
		public void Ndk_PathInSdk()
		{
			CreateSdks(out string root, out string jdk, out string ndk, out string sdk);

			Action<TraceLevel, string> logger = (level, message) => {
				Console.WriteLine($"[{level}] {message}");
			};

			try
			{
				var extension = OS.IsWindows ? ".cmd" : "";
				var ndkPath = Path.Combine(sdk, "ndk-bundle");
				Directory.CreateDirectory(ndkPath);
				File.WriteAllText(Path.Combine (ndkPath, "source.properties"), $"Pkg.Revision = {NdkVersion}");
				Directory.CreateDirectory(Path.Combine(ndkPath, "toolchains"));
				File.WriteAllText(Path.Combine(ndkPath, $"ndk-stack{extension}"), "");

				var info = new AndroidSdkInfo(logger, androidSdkPath: sdk, androidNdkPath: null, javaSdkPath: jdk);

				Assert.AreEqual(ndkPath, info.AndroidNdkPath, "AndroidNdkPath not found inside sdk!");
			}
			finally
			{
				Directory.Delete(root, recursive: true);
			}
		}

		[Test]
		public void Ndk_Path_InvalidChars ()
		{
			CreateSdks (out string root, out string jdk, out string ndk, out string sdk);

			Action<TraceLevel, string> logger = (level, message) => {
				Console.WriteLine ($"[{level}] {message}");
				if (level == TraceLevel.Error)
					Assert.Fail (message);
			};

			var oldPath = Environment.GetEnvironmentVariable ("PATH");
			try {
				Environment.SetEnvironmentVariable ("PATH", "\"C:\\IHAVEQUOTES\\\"");
				// Check that this doesn't throw
				new AndroidSdkInfo (logger, androidSdkPath: sdk, androidNdkPath: null, javaSdkPath: jdk);
			} finally {
				Environment.SetEnvironmentVariable ("PATH", oldPath);
				Directory.Delete (root, recursive: true);
			}
		}

		[Test]
		public void Ndk_PathExt_InvalidChars ()
		{
			CreateSdks (out string root, out string jdk, out string ndk, out string sdk);

			Action<TraceLevel, string> logger = (level, message) => {
				Console.WriteLine ($"[{level}] {message}");
				if (level == TraceLevel.Error)
					Assert.Fail (message);
			};

			var oldPathExt = Environment.GetEnvironmentVariable ("PATHEXT");
			try {
				Environment.SetEnvironmentVariable ("PATHEXT", string.Join (Path.PathSeparator.ToString (), "\"", ".EXE", ".BAT"));
				// Check that this doesn't throw
				new AndroidSdkInfo (logger, androidSdkPath: sdk, androidNdkPath: null, javaSdkPath: jdk);
			} finally {
				Environment.SetEnvironmentVariable ("PATHEXT", oldPathExt);
				Directory.Delete (root, recursive: true);
			}
		}

		[Test]
		public void Ndk_AndroidSdkDoesNotExist ()
		{
			CreateSdks (out string root, out string jdk, out string ndk, out string sdk);

			Action<TraceLevel, string> logger = (level, message) => {
				Console.WriteLine ($"[{level}] {message}");
				if (level == TraceLevel.Error)
					Assert.Fail (message);
			};

			var oldAndroidHome = Environment.GetEnvironmentVariable ("ANDROID_HOME");
			try {
				Environment.SetEnvironmentVariable ("ANDROID_HOME", "/i/dont/exist");
				// Check that this doesn't throw
				new AndroidSdkInfo (logger, androidSdkPath: sdk, androidNdkPath: null, javaSdkPath: jdk);
			} finally {
				Environment.SetEnvironmentVariable ("ANDROID_HOME", oldAndroidHome);
				Directory.Delete (root, recursive: true);
			}
		}

		[Test]
		public void Constructor_SetValuesFromPath ()
		{
			if (OS.IsWindows)
				Assert.Ignore ("Windows does not look for values in %PATH%");

			CreateSdks (out string root, out string jdk, out string ndk, out string sdk);
			JdkInfoTests.CreateFauxJdk (jdk, releaseVersion: "1.8.0", releaseBuildNumber: "42", javaVersion: "100.100.100_100");

			Action<TraceLevel, string> logger = (level, message) => {
				Console.WriteLine ($"[{level}] {message}");
			};
			var oldPath = Environment.GetEnvironmentVariable ("PATH");
			var oldJavaHome = Environment.GetEnvironmentVariable ("JAVA_HOME");
			var oldAndroidHome = Environment.GetEnvironmentVariable ("ANDROID_HOME");
			var oldAndroidSdkRoot = Environment.GetEnvironmentVariable ("ANDROID_SDK_ROOT");
			try {
				var paths   = new List<string> () {
					Path.Combine (jdk, "bin"),
					ndk,
					Path.Combine (sdk, "platform-tools"),
				};
				paths.AddRange (oldPath.Split (new[]{Path.PathSeparator}, StringSplitOptions.RemoveEmptyEntries));
				Environment.SetEnvironmentVariable ("PATH", string.Join (Path.PathSeparator.ToString (), paths));
				if (!string.IsNullOrEmpty (oldJavaHome)) {
					Environment.SetEnvironmentVariable ("JAVA_HOME", string.Empty);
				}
				if (!string.IsNullOrEmpty (oldAndroidHome)) {
					Environment.SetEnvironmentVariable ("ANDROID_HOME", string.Empty);
				}
				if (!string.IsNullOrEmpty (oldAndroidSdkRoot)) {
					Environment.SetEnvironmentVariable ("ANDROID_SDK_ROOT", string.Empty);
				}

				var info    = new AndroidSdkInfo (logger);

				Assert.AreEqual (ndk, info.AndroidNdkPath,  "AndroidNdkPath not set from $PATH!");
				Assert.AreEqual (sdk, info.AndroidSdkPath,  "AndroidSdkPath not set from $PATH!");
				Assert.AreEqual (jdk, info.JavaSdkPath,     "JavaSdkPath not set from $PATH!");
			}
			finally {
				Environment.SetEnvironmentVariable ("PATH", oldPath);
				if (!string.IsNullOrEmpty (oldJavaHome)) {
					Environment.SetEnvironmentVariable ("JAVA_HOME", oldJavaHome);
				}
				if (!string.IsNullOrEmpty (oldAndroidHome)) {
					Environment.SetEnvironmentVariable ("ANDROID_HOME", oldAndroidHome);
				}
				if (!string.IsNullOrEmpty (oldAndroidSdkRoot)) {
					Environment.SetEnvironmentVariable ("ANDROID_SDK_ROOT", oldAndroidSdkRoot);
				}
				Directory.Delete (root, recursive: true);
			}
		}

		[Test]
		public void JdkDirectory_JavaHome ([Values ("JI_JAVA_HOME", "JAVA_HOME")] string envVar)
		{
			if (envVar.Equals ("JAVA_HOME", StringComparison.OrdinalIgnoreCase)) {
				Assert.Ignore ("This test will only work locally if you rename/remove your Open JDK directory.");
				return;
			}

			CreateSdks (out string root, out string jdk, out string ndk, out string sdk);
			JdkInfoTests.CreateFauxJdk (jdk, releaseVersion: "1.8.999", releaseBuildNumber: "9", javaVersion: "1.8.999-9");

			var logs = new StringWriter ();
			Action<TraceLevel, string> logger = (level, message) => {
				logs.WriteLine ($"[{level}] {message}");
			};

			string java_home = null;
			try {
				// We only set via JAVA_HOME
				java_home = Environment.GetEnvironmentVariable (envVar, EnvironmentVariableTarget.Process);
				Environment.SetEnvironmentVariable (envVar, jdk, EnvironmentVariableTarget.Process);
				var info = new AndroidSdkInfo (logger, androidSdkPath: sdk, androidNdkPath: ndk, javaSdkPath: "");

				Assert.AreEqual (ndk, info.AndroidNdkPath, "AndroidNdkPath not preserved!");
				Assert.AreEqual (sdk, info.AndroidSdkPath, "AndroidSdkPath not preserved!");
				Assert.AreEqual (jdk, info.JavaSdkPath, "JavaSdkPath not preserved!");
			} finally {
				Environment.SetEnvironmentVariable (envVar, java_home, EnvironmentVariableTarget.Process);
				Directory.Delete (root, recursive: true);
			}
		}

		[Test]
		public void Sdk_GetCommandLineToolsPaths ()
		{
			CreateSdks(out string root, out string jdk, out string ndk, out string sdk);

			var cmdlineTools        = Path.Combine (sdk, "cmdline-tools");
			var latestToolsVersion  = "latest";
			var toolsVersion        = "2.1";
			var higherToolsVersion  = "11.2";

			void recreateCmdlineToolsDirectory () {
				Directory.Delete (cmdlineTools, recursive: true);
				Directory.CreateDirectory (cmdlineTools);
			}

			try {
				var info = new AndroidSdkInfo (androidSdkPath: sdk);

				// Test cmdline-tools path
				recreateCmdlineToolsDirectory();
				CreateFauxAndroidSdkToolsDirectory (sdk, createToolsDir: true, toolsVersion: toolsVersion, createOldToolsDir: false);
				var toolsPaths = info.GetCommandLineToolsPaths ();

				Assert.AreEqual (toolsPaths.Count (), 1, "Incorrect number of elements");
				Assert.AreEqual (toolsPaths.First (), Path.Combine (sdk, "cmdline-tools", toolsVersion), "Incorrect command line tools path");

				// Test that cmdline-tools is preferred over tools
				recreateCmdlineToolsDirectory();
				CreateFauxAndroidSdkToolsDirectory (sdk, createToolsDir: true, toolsVersion: latestToolsVersion, createOldToolsDir: true);
				toolsPaths = info.GetCommandLineToolsPaths ();

				Assert.AreEqual (toolsPaths.Count (), 2, "Incorrect number of elements");
				Assert.AreEqual (toolsPaths.First (), Path.Combine (sdk, "cmdline-tools", latestToolsVersion), "Incorrect command line tools path");
				Assert.AreEqual (toolsPaths.Last (), Path.Combine (sdk, "tools"), "Incorrect tools path");

				// Test sorting
				recreateCmdlineToolsDirectory ();
				CreateFauxAndroidSdkToolsDirectory (sdk, createToolsDir: true,  toolsVersion: latestToolsVersion,   createOldToolsDir: false);
				CreateFauxAndroidSdkToolsDirectory (sdk, createToolsDir: true,  toolsVersion: toolsVersion,         createOldToolsDir: false);
				CreateFauxAndroidSdkToolsDirectory (sdk, createToolsDir: true,  toolsVersion: higherToolsVersion,   createOldToolsDir: true);
				toolsPaths = info.GetCommandLineToolsPaths ();

				var toolsPathsList = toolsPaths.ToList ();
				Assert.AreEqual (toolsPaths.Count (), 4, "Incorrect number of elements");
				bool isOrderCorrect = toolsPathsList [0].Equals (Path.Combine (sdk, "cmdline-tools", latestToolsVersion), StringComparison.Ordinal)
					&& toolsPathsList [1].Equals (Path.Combine (sdk, "cmdline-tools", higherToolsVersion), StringComparison.Ordinal)
					&& toolsPathsList [2].Equals (Path.Combine (sdk, "cmdline-tools", toolsVersion), StringComparison.Ordinal)
					&& toolsPathsList [3].Equals (Path.Combine (sdk, "tools"), StringComparison.Ordinal);

				Assert.IsTrue (isOrderCorrect, "Tools order is not descending");
			} finally {
				Directory.Delete (root, recursive: true);
			}
		}

		static  bool    IsWindows   => OS.IsWindows;

		static string CreateRoot ()
		{
			var root = Path.GetTempFileName ();
			File.Delete (root);
			Directory.CreateDirectory (root);
			return root;
		}

		static void CreateSdks (out string root, out string jdk, out string ndk, out string sdk)
		{
			root    = CreateRoot ();

			ndk     = Path.Combine (root, "ndk");
			sdk     = Path.Combine (root, "sdk");
			jdk     = Path.Combine (root, "jdk");

			Directory.CreateDirectory (sdk);
			Directory.CreateDirectory (ndk);
			Directory.CreateDirectory (jdk);

			CreateFauxAndroidSdkDirectory (sdk, "26.0.0");
			CreateFauxAndroidNdkDirectory (ndk, NdkVersion);
			CreateFauxJavaSdkDirectory (jdk, "1.8.0", out var _, out var _);
		}

		static void CreateFauxAndroidSdkToolsDirectory (string androidSdkDirectory, bool createToolsDir, string toolsVersion, bool createOldToolsDir)
		{
			if (createToolsDir) {
				string androidSdkToolsPath    = Path.Combine (androidSdkDirectory, "cmdline-tools", toolsVersion ?? "1.0");
				string androidSdkToolsBinPath = Path.Combine (androidSdkToolsPath, "bin");

				Directory.CreateDirectory (androidSdkToolsPath);
				Directory.CreateDirectory (androidSdkToolsBinPath);

				File.WriteAllText (Path.Combine (androidSdkToolsBinPath, IsWindows ? "lint.bat" : "lint"), "");
			}

			if (createOldToolsDir) {
				string androidSdkToolsPath    = Path.Combine (androidSdkDirectory, "tools");
				string androidSdkToolsBinPath = Path.Combine (androidSdkToolsPath, "bin");

				Directory.CreateDirectory (androidSdkToolsPath);
				Directory.CreateDirectory (androidSdkToolsBinPath);

				File.WriteAllText (Path.Combine (androidSdkToolsBinPath, IsWindows ? "lint.bat" : "lint"), "");
			}

		}

		static void CreateFauxAndroidSdkDirectory (
				string  androidSdkDirectory,
				string  buildToolsVersion,
				bool    createToolsDir = true,
				string  toolsVersion = null,
				bool    createOldToolsDir = false,
				ApiInfo[]   apiLevels = null)
		{
			CreateFauxAndroidSdkToolsDirectory (androidSdkDirectory, createToolsDir, toolsVersion, createOldToolsDir);

			var androidSdkPlatformToolsPath     = Path.Combine (androidSdkDirectory, "platform-tools");
			var androidSdkPlatformsPath         = Path.Combine (androidSdkDirectory, "platforms");
			var androidSdkBuildToolsPath        = Path.Combine (androidSdkDirectory, "build-tools", buildToolsVersion);

			Directory.CreateDirectory (androidSdkDirectory);
			Directory.CreateDirectory (androidSdkPlatformToolsPath);
			Directory.CreateDirectory (androidSdkPlatformsPath);
			Directory.CreateDirectory (androidSdkBuildToolsPath);

			File.WriteAllText (Path.Combine (androidSdkPlatformToolsPath,   IsWindows ? "adb.exe" : "adb"),             "");
			File.WriteAllText (Path.Combine (androidSdkBuildToolsPath,      IsWindows ? "zipalign.exe" : "zipalign"),   "");
			File.WriteAllText (Path.Combine (androidSdkBuildToolsPath,      IsWindows ? "aapt.exe" : "aapt"),           "");

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

		static void CreateFauxAndroidNdkDirectory (string androidNdkDirectory, string ndkVersion)
		{
			File.WriteAllText (Path.Combine (androidNdkDirectory, "source.properties"), $"Pkg.Revision = {ndkVersion}");
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

		[Test]
		public void GetBuildToolsPaths_StableVersionsFirst ()
		{
			CreateSdks (out string root, out string jdk, out string ndk, out string sdk);
			CreateFauxAndroidSdkDirectory (sdk, "27.0.0-rc4");

			var logs    = new StringWriter ();
			Action<TraceLevel, string> logger = (level, message) => {
				logs.WriteLine ($"[{level}] {message}");
			};

			try {
				var info    = new AndroidSdkInfo (logger, androidSdkPath: sdk, androidNdkPath: ndk, javaSdkPath: jdk);

				var buildToolsPaths = info.GetBuildToolsPaths ().ToList ();
				Assert.AreEqual (3, buildToolsPaths.Count);
				Assert.AreEqual (Path.Combine (sdk, "build-tools", "26.0.0"),       buildToolsPaths [0]);
				Assert.AreEqual (Path.Combine (sdk, "build-tools", "27.0.0-rc4"),   buildToolsPaths [1]);
				Assert.AreEqual (Path.Combine (sdk, "platform-tools"),              buildToolsPaths [2]);
			}
			finally {
				Directory.Delete (root, recursive: true);
			}
		}

		void MakeNdkDir (string rootPath, string version)
		{
			var extension = OS.IsWindows ? ".cmd" : String.Empty;
			Directory.CreateDirectory(rootPath);
			File.WriteAllText(Path.Combine (rootPath, "source.properties"), $"Pkg.Revision = {version}");
			Directory.CreateDirectory(Path.Combine(rootPath, "toolchains"));
			File.WriteAllText(Path.Combine(rootPath, $"ndk-stack{extension}"), String.Empty);
		}
	}
}
