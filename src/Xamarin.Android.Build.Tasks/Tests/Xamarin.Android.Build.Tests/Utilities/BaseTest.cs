using System;
using System.IO;
using System.Collections.Generic;
using Xamarin.ProjectTools;
using NUnit.Framework;
using System.Linq;

namespace Xamarin.Android.Build.Tests
{
	public class BaseTest
	{
		static BaseTest ()
		{
			try {
				var adbTarget = Environment.GetEnvironmentVariable ("ADB_TARGET");
				HasDevices = string.Compare (RunAdbCommand ($"{adbTarget} shell getprop ro.build.version.sdk"),
						"error: no devices/emulators found" , StringComparison.InvariantCultureIgnoreCase) != 0;
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

		protected static string RunAdbCommand (string command, bool ignoreErrors = true)
		{
			string ext = Environment.OSVersion.Platform != PlatformID.Unix ? ".exe" : "";
			var home = Environment.GetFolderPath (Environment.SpecialFolder.Personal);
			var sdkPath = Environment.GetEnvironmentVariable ("ANDROID_SDK_PATH");
			if (string.IsNullOrEmpty (sdkPath))
				sdkPath = $"{home}/android-toolchain/sdk";
			string adb = Path.Combine (sdkPath, "platform-tools", "adb" + ext);
			var proc = System.Diagnostics.Process.Start (new System.Diagnostics.ProcessStartInfo (adb, command) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false });
			proc.WaitForExit ();
			var result = proc.StandardOutput.ReadToEnd ().Trim () + proc.StandardError.ReadToEnd ().Trim ();
			return result;
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

		[TearDown]
		protected virtual void CleanupTest ()
		{
			if (TestContext.CurrentContext.Test.Properties ["Output"] == null)
					return;
			// find the "root" directory just below "temp" and clean from there because
			// some tests create multiple subdirectories
			var output = Path.GetFullPath (((string [])TestContext.CurrentContext.Test.Properties ["Output"]) [0]);
			while (!Directory.GetParent (output).Name.EndsWith ("temp", StringComparison.OrdinalIgnoreCase)) {
					output = Directory.GetParent (output).FullName;
			}
			if (!Directory.Exists (output))
				return;
			if (TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Passed) {
				FileSystemUtils.SetDirectoryWriteable (output);
				Directory.Delete (output, recursive: true);
			} else {
				foreach (var file in Directory.GetFiles (Path.Combine (output), "build.log", SearchOption.AllDirectories)) {
					TestContext.Out.WriteLine ("*************************************************************************");
					TestContext.Out.WriteLine (file);
					TestContext.Out.WriteLine ();
					TestContext.Out.WriteLine (File.ReadAllText (file));
					TestContext.Out.WriteLine ("*************************************************************************");
					TestContext.Out.Flush ();
				}
			}
		}
	}
}

