using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class BundleToolTaskTests
	{
		[Test]
		public void BuildApkSetCommand ()
		{
			var task = new BuildApkSet {
				AppBundle = "foo.aab",
				Output = "foo.apks",
				KeyStore = "foo.keystore",
				KeyAlias = "alias",
				KeyPass = "keypass",
				StorePass = "storepass",
				Aapt2ToolPath = Path.Combine ("aapt", "with spaces"),
				Aapt2ToolExe = "aapt2",
				AdbToolPath = Path.Combine ("adb", "with spaces"),
				AdbToolExe = "adb",
				AdbTarget = "-s emulator-5554",
				JarPath = "bundletool.jar",
			};
			string aapt2 = Path.Combine (task.Aapt2ToolPath, task.Aapt2ToolExe);
			string adb = Path.Combine (task.AdbToolPath, task.AdbToolExe);
			string cmd = task.GetCommandLineBuilder ().ToString ();
			Assert.AreEqual ($"-jar bundletool.jar build-apks --connected-device --mode default --adb \"{adb}\" --device-id emulator-5554 --bundle foo.aab --output foo.apks --aapt2 \"{aapt2}\" --ks foo.keystore --ks-key-alias alias --key-pass pass:keypass --ks-pass pass:storepass", cmd);
		}

		[Test]
		public void InstallApkSetCommandPreservesJavaOptions ()
		{
			var task = new InstallApkSet {
				ApkSet = "foo.apks",
				AdbToolPath = Path.Combine ("path", "with spaces"),
				AdbToolExe = "adb",
				AdbTarget = "-s emulator-5554",
				JarPath = "bundletool.jar",
				JavaOptions = "-Dexample=\"cost$5\"",
			};
			string adb = Path.Combine (task.AdbToolPath, task.AdbToolExe);
			string cmd = task.GetCommandLineBuilder ().ToString ();
			Assert.AreEqual ($"-Dexample=\"cost$5\" -jar bundletool.jar install-apks --apks foo.apks --adb \"{adb}\" --device-id emulator-5554 --allow-downgrade --modules _ALL_", cmd);
		}

		[Test]
		public void InstallApkSetReportsQuotedBundletoolErrors ()
		{
			var errors = new List<BuildErrorEventArgs> ();
			var task = new TestableInstallApkSet {
				ApkSet = "application.apks",
				BuildEngine = new MockBuildEngine (TextWriter.Null, errors),
			};

			task.AddOutput ("com.android.tools.build.bundletool.model.exceptions.CommandExecutionException: Device 'emulator-5554' not found.");
			task.ProcessErrors ();

			Assert.AreEqual (1, errors.Count);
			Assert.AreEqual ("BT0000", errors [0].Code);
			Assert.AreEqual ("application.apks", errors [0].File);
			StringAssert.Contains ("Device 'emulator-5554' not found.", errors [0].Message);
		}

		[Test]
		public void InstallApkSetIgnoresQuotedNonmatchingOutput ()
		{
			var errors = new List<BuildErrorEventArgs> ();
			var task = new TestableInstallApkSet {
				ApkSet = "application.apks",
				BuildEngine = new MockBuildEngine (TextWriter.Null, errors),
			};

			Assert.IsTrue (task.ProcessOutputLine ("File 'application.apks' was not found."));
			Assert.IsEmpty (errors);
		}

		sealed class TestableInstallApkSet : InstallApkSet
		{
			public void AddOutput (string line) => LogEventsFromTextOutput (line, MessageImportance.Normal);

			public void ProcessErrors () => HandleTaskExecutionErrors ();

			public bool ProcessOutputLine (string line) => ProcessOutput (line, new AssemblyIdentityMap ());
		}
	}
}
