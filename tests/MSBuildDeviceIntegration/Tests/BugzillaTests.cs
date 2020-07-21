using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[SingleThreaded]
	[Category ("UsesDevices")]
	public class BugzillaTests : DeviceTest
	{
		static ProjectBuilder builder;
		static XamarinAndroidApplicationProject proj;

		[TearDown]
		public void Teardown ()
		{
			if (HasDevices && proj != null)
				RunAdbCommand ($"uninstall {proj.PackageName}");

			if (TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Passed
				&& builder != null && Directory.Exists (builder.ProjectDirectory))
				Directory.Delete (builder.ProjectDirectory, recursive: true);
		}

		[Test]
		public void GlobalLayoutEvent_ShouldRegisterAndFire_OnActivityLaunch ([Values (false, true)] bool isRelease)
		{
			AssertHasDevices ();

			string expectedLogcatOutput = "Bug 29730: GlobalLayout event handler called!";

			proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
			};
			if (isRelease || !CommercialBuildAvailable) {
				proj.SetAndroidSupportedAbis ("armeabi-v7a", "arm64-v8a", "x86");
			} else {
				proj.MinSdkVersion = "23";
				proj.TargetSdkVersion = null;
			}
			proj.MainActivity = proj.DefaultMainActivity.Replace ("//${AFTER_ONCREATE}",
$@"button.ViewTreeObserver.GlobalLayout += Button_ViewTreeObserver_GlobalLayout;
		}}
		void Button_ViewTreeObserver_GlobalLayout (object sender, EventArgs e)
		{{
			Android.Util.Log.Debug (""BugzillaTests"", ""{expectedLogcatOutput}"");
");
			builder = CreateApkBuilder (Path.Combine ("temp", $"Bug29730-{isRelease}"));
			Assert.IsTrue (builder.Install (proj), "Install should have succeeded.");
			ClearAdbLogcat ();
			AdbStartActivity ($"{proj.PackageName}/{proj.JavaPackageName}.MainActivity");
			Assert.IsTrue (MonitorAdbLogcat ((line) => {
				return line.Contains (expectedLogcatOutput);
			}, Path.Combine (Root, builder.ProjectDirectory, "startup-logcat.log"), 45), $"Output did not contain {expectedLogcatOutput}!");
		}

	}
}
