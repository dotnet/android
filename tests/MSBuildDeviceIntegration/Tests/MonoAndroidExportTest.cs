using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using Mono.Debugging.Client;
using Mono.Debugging.Soft;
using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Category ("UsesDevice")]
	public class MonoAndroidExportTest : DeviceTest
	{
#pragma warning disable 414
		static object [] MonoAndroidExportTestCases = new object [] {
			new object[] {
				/* embedAssemblies */    true,
				/* fastDevType */        "Assemblies",
				/* isRelease */          false,
			},
			new object[] {
				/* embedAssemblies */    false,
				/* fastDevType */        "Assemblies",
				/* isRelease */          false,
			},
			new object[] {
				/* embedAssemblies */    true,
				/* fastDevType */        "Assemblies:Dexes",
				/* isRelease */          false,
			},
			new object[] {
				/* embedAssemblies */    false,
				/* fastDevType */        "Assemblies:Dexes",
				/* isRelease */          false,
			},
			new object[] {
				/* embedAssemblies */    true,
				/* fastDevType */        "",
				/* isRelease */          true,
			},
		};
#pragma warning restore 414

		[Test]
		[TestCaseSource (nameof (MonoAndroidExportTestCases))]
		public void MonoAndroidExportReferencedAppStarts (bool embedAssemblies, string fastDevType, bool isRelease)
		{
			AssertCommercialBuild ();
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
				References = {
					new BuildItem.Reference ("Mono.Android.Export"),
				},
			};
			if (!string.IsNullOrEmpty (fastDevType))
				proj.AndroidFastDeploymentType = fastDevType;
			proj.Sources.Add (new BuildItem.Source ("ContainsExportedMethods.cs") {
				TextContent = () => @"using System;
using Java.Interop;

namespace UnnamedProject {
	class ContainsExportedMethods : Java.Lang.Object {

		public bool Constructed;

		public int Count;

		public ContainsExportedMethods ()
		{
			Console.WriteLine (""# ContainsExportedMethods: constructed! Handle=0x{0}"", Handle.ToString (""x""));
			Constructed = true;
		}

		[Export]
		public void Exported ()
		{
			Count++;
		}
	}
}
",
			});
			proj.MainActivity = @"using System;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;

namespace UnnamedProject
{
	[Activity (Label = ""UnnamedProject"", MainLauncher = true, Icon = ""@drawable/icon"")]
	public class MainActivity : Activity {
			protected override void OnCreate (Bundle bundle)
			{
				base.OnCreate (bundle);
				var foo = new ContainsExportedMethods ();
				foo.Exported ();
			}
		}
	}";
			proj.SetAndroidSupportedAbis ("armeabi-v7a", "x86", "x86_64");
			proj.SetProperty ("EmbedAssembliesIntoApk", embedAssemblies.ToString ());
			proj.SetDefaultTargetDevice ();
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				b.LatestTargetFrameworkVersion (out string apiLevel);
				proj.SupportedOSPlatformVersion = "24.0";
				proj.AndroidManifest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" android:versionCode=""1"" android:versionName=""1.0"" package=""${proj.PackageName}"">
	<uses-sdk android:targetSdkVersion=""{apiLevel}"" />
	<application android:label=""${{PROJECT_NAME}}"">
	</application >
</manifest>";
				Assert.True (b.Install (proj), "Project should have installed.");
				RunProjectAndAssert (proj, b, doNotCleanupOnUpdate: true);
				Assert.True (WaitForActivityToStart (proj.PackageName, "MainActivity",
					Path.Combine (Root, b.ProjectDirectory, "logcat.log"), 30), "Activity should have started.");
				string expectedLogcatOutput = "ContainsExportedMethods: constructed! Handle=";
				Assert.IsTrue (MonitorAdbLogcat ((line) => {
					return line.Contains (expectedLogcatOutput);
				}, Path.Combine (Root, b.ProjectDirectory, "startup-logcat.log"), 45), $"Output did not contain {expectedLogcatOutput}!");
				Assert.True (b.Uninstall (proj), "Project should have uninstalled.");
			}
		}
	}
}
