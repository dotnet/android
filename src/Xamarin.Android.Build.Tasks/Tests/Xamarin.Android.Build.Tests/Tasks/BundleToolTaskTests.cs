using System.IO;
using NUnit.Framework;
using Xamarin.Android.Tasks;

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

		[TestCase (null, "_ALL_", false)]
		[TestCase (new [] { "base" }, "base", false)]
		[TestCase (new [] { "base", "feature" }, "\"base,feature\"", false)]
		[TestCase (null, "_ALL_", true)]
		[TestCase (new [] { "base", "feature" }, "\"base,feature\"", true)]
		public void InstallApkSetCommand (string [] modules, string expectedModules, bool isTestOnly)
		{
			var task = new InstallApkSet {
				ApkSet = "foo.apks",
				AdbToolPath = Path.Combine ("path", "with spaces"),
				AdbToolExe = "adb",
				AdbTarget = "-s emulator-5554",
				JarPath = "bundletool.jar",
				Modules = modules,
				IsTestOnly = isTestOnly,
			};
			string adb = Path.Combine (task.AdbToolPath, task.AdbToolExe);
			string cmd = task.GetCommandLineBuilder ().ToString ();
			string allowTestOnly = isTestOnly ? " --allow-test-only" : "";
			Assert.AreEqual ($"-jar bundletool.jar install-apks --apks foo.apks --adb \"{adb}\" --device-id emulator-5554 --allow-downgrade{allowTestOnly} --modules {expectedModules}", cmd);
		}
	}
}
