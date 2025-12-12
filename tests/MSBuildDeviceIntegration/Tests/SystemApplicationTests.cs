using System;
using NUnit.Framework;
using Xamarin.ProjectTools;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using System.Text;
using System.Xml.Linq;
using System.Collections.Generic;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Category ("UsesDevice")]
	public class SystemApplicationTests : DeviceTest
	{
		// All Tests here require the emulator to be started with -writable-system
		[Test, Category ("SystemApplication")]
		public void SystemApplicationCanInstall ([Values] AndroidRuntime runtime)
		{
			const bool isRelease = false;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}
			AssertCommercialBuild ();

			var proj = new XamarinAndroidApplicationProject (packageName: PackageUtils.MakePackageName (runtime)) {
				IsRelease = false,
				EmbedAssembliesIntoApk = false,
			};
			proj.SetRuntime (runtime);
			proj.OtherBuildItems.Add (new BuildItem ("None", "platform.pk8") {
				WebContent = "https://github.com/aosp-mirror/platform_build/raw/master/target/product/security/platform.pk8"
			});
			proj.OtherBuildItems.Add (new BuildItem ("None", "platform.x509.pem") {
				WebContent = "https://github.com/aosp-mirror/platform_build/raw/master/target/product/security/platform.x509.pem"
			});
			proj.AndroidManifest = proj.AndroidManifest.Replace ("<manifest ", "<manifest android:sharedUserId=\"android.uid.system\" ");
			proj.SetAndroidSupportedAbis (DeviceAbi);


			proj.SetDefaultTargetDevice ();
			using (var b = CreateApkBuilder ()) {
				proj.SetProperty ("AndroidSigningPlatformKey", Path.Combine (Root, b.ProjectDirectory, "platform.pk8"));
				proj.SetProperty ("AndroidSigningPlatformCert", Path.Combine (Root, b.ProjectDirectory, "platform.x509.pem"));
				Assert.True (b.Install (proj), "Project should have installed.");
			}
		}
	}
}
