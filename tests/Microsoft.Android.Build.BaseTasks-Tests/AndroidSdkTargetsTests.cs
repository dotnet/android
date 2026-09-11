using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NUnit.Framework;
using Xamarin.Android.Tools.BootstrapTasks;

namespace Microsoft.Android.Build.BaseTasks.Tests
{
	[TestFixture]
	public class AndroidSdkTargetsTests
	{
		string tempDirectory;

		[SetUp]
		public void SetUp ()
		{
			tempDirectory = Path.Combine (Path.GetTempPath (), $"{nameof (AndroidSdkTargetsTests)}-{Guid.NewGuid ():N}");
			Directory.CreateDirectory (tempDirectory);
		}

		[TearDown]
		public void TearDown ()
		{
			if (Directory.Exists (tempDirectory))
				Directory.Delete (tempDirectory, recursive: true);
		}

		[Test]
		public void ExtractAndroidSdkPackageAtomicallyReplacesDestinationAndRemainsIncremental ()
		{
			var packageRoot = Path.Combine (tempDirectory, "package");
			var archiveRoot = Path.Combine (packageRoot, "android-test");
			var cacheDirectory = Path.Combine (tempDirectory, "cache");
			var sdkDirectory = Path.Combine (tempDirectory, "sdk");
			var destination = Path.Combine (sdkDirectory, "platforms", "android-test");
			Directory.CreateDirectory (archiveRoot);
			Directory.CreateDirectory (cacheDirectory);
			Directory.CreateDirectory (Path.Combine (destination, "data", "res"));
			File.WriteAllText (Path.Combine (archiveRoot, "source.properties"), "Pkg.Revision=1");
			File.WriteAllText (Path.Combine (archiveRoot, "android.jar"), "new");
			File.WriteAllText (Path.Combine (destination, "data", "res", "old.xml"), "old");
			ZipFile.CreateFromDirectory (packageRoot, Path.Combine (cacheDirectory, "fake-platform.zip"));

			RunMsbuild (cacheDirectory, sdkDirectory, destination + Path.DirectorySeparatorChar, expectSuccess: true);

			FileAssert.Exists (Path.Combine (destination, "android.jar"));
			FileAssert.DoesNotExist (Path.Combine (destination, "data", "res", "old.xml"));
			FileAssert.Exists (Path.Combine (destination, ".extracted-fake-platform.zip-TESTHASH"));
			Assert.IsFalse (Directory.GetDirectories (Path.GetDirectoryName (destination), "android-test.old-*").Any (), "Old package backups should be removed.");
			Assert.IsFalse (Directory.GetDirectories (Path.GetDirectoryName (destination), "android-test.staging-*").Any (), "Staging directories should be removed.");

			var marker = Path.Combine (destination, "incremental-marker");
			var staleBackup = destination + ".old-stale";
			File.WriteAllText (marker, "");
			Directory.CreateDirectory (staleBackup);
			File.WriteAllText (Path.Combine (staleBackup, "stale.txt"), "");
			RunMsbuild (cacheDirectory, sdkDirectory, destination + Path.DirectorySeparatorChar, expectSuccess: true);
			FileAssert.Exists (marker);
			Assert.IsFalse (Directory.Exists (staleBackup), "Incremental builds should clean stale package backups.");
		}

		[Test]
		public void ExtractAndroidSdkPackageCleansStagingAfterExtractionFailure ()
		{
			var cacheDirectory = Path.Combine (tempDirectory, "cache");
			var sdkDirectory = Path.Combine (tempDirectory, "sdk");
			var destination = Path.Combine (sdkDirectory, "platforms", "android-test");
			Directory.CreateDirectory (cacheDirectory);
			File.WriteAllText (Path.Combine (cacheDirectory, "fake-platform.zip"), "not a zip");

			RunMsbuild (cacheDirectory, sdkDirectory, destination, expectSuccess: false);

			var destinationParent = Path.GetDirectoryName (destination);
			if (Directory.Exists (destinationParent))
				Assert.IsFalse (Directory.GetDirectories (destinationParent, "android-test.staging-*").Any (), "Failed extraction should clean its staging directory.");
		}

		void RunMsbuild (string cacheDirectory, string sdkDirectory, string destination, bool expectSuccess)
		{
			var repositoryRoot = Path.GetFullPath (Path.Combine (TestContext.CurrentContext.TestDirectory, "..", ".."));
			var project = Path.Combine (repositoryRoot, "tests", "Microsoft.Android.Build.BaseTasks-Tests", "Resources", "AndroidSdkTargetsTest.proj");
			var hostOS = OperatingSystem.IsWindows () ? "Windows" : OperatingSystem.IsMacOS () ? "Darwin" : "Linux";
			var startInfo = new ProcessStartInfo ("dotnet") {
				RedirectStandardError = true,
				RedirectStandardOutput = true,
				UseShellExecute = false,
			};
			startInfo.ArgumentList.Add ("msbuild");
			startInfo.ArgumentList.Add (project);
			startInfo.ArgumentList.Add ("-nologo");
			startInfo.ArgumentList.Add ("-t:_InstallAndroidSdkComponents");
			startInfo.ArgumentList.Add ("-v:minimal");
			startInfo.ArgumentList.Add ($"-p:TestHostOS={hostOS}");
			startInfo.ArgumentList.Add ($"-p:TestSdkDirectory={sdkDirectory}");
			startInfo.ArgumentList.Add ($"-p:TestNdkDirectory={Path.Combine (tempDirectory, "ndk")}");
			startInfo.ArgumentList.Add ($"-p:TestCacheDirectory={cacheDirectory}");
			startInfo.ArgumentList.Add ($"-p:TestDestination={destination}");
			startInfo.ArgumentList.Add ($"-p:TestBootstrapTasksAssembly={typeof (ReplaceDirectory).Assembly.Location}");

			using (var process = Process.Start (startInfo)) {
				Assert.IsNotNull (process, "Could not start dotnet msbuild.");
				if (process == null)
					return;
				var standardOutputTask = process.StandardOutput.ReadToEndAsync ();
				var standardErrorTask = process.StandardError.ReadToEndAsync ();
				process.WaitForExit ();
				var standardOutput = standardOutputTask.GetAwaiter ().GetResult ();
				var standardError = standardErrorTask.GetAwaiter ().GetResult ();
				var expectedExitCode = expectSuccess ? 0 : 1;
				Assert.AreEqual (expectedExitCode, process.ExitCode, standardOutput + Environment.NewLine + standardError);
			}
		}
	}
}
