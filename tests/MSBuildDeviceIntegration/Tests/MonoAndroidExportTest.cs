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
	[NonParallelizable]
	[Category ("UsesDevice")]
	public class MonoAndroidExportTest : DeviceTest {
#pragma warning disable 414
		static object [] MonoAndroidExportTestCases = new object [] {
			new object[] {
				/* embedAssemblies */    true,
				/* fastDevType */        "Assemblies",
			},
			new object[] {
				/* embedAssemblies */    false,
				/* fastDevType */        "Assemblies",
			},
			new object[] {
				/* embedAssemblies */    true,
				/* fastDevType */        "Assemblies:Dexes",
			},
			new object[] {
				/* embedAssemblies */    false,
				/* fastDevType */        "Assemblies:Dexes",
			},
		};
#pragma warning restore 414

		[Test]
		[TestCaseSource (nameof (MonoAndroidExportTestCases))]
		public void MonoAndroidExportReferencedAppStarts (bool embedAssemblies, string fastDevType)
		{
			AssertCommercialBuild ();
			AssertHasDevices ();
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = false,
				AndroidFastDeploymentType = fastDevType,
				References = {
					new BuildItem.Reference ("Mono.Android.Export"),
				},
			};
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
			//TODO: x86_64 is a workaround in .NET 6 for: https://github.com/xamarin/monodroid/issues/1136
			proj.SetAndroidSupportedAbis ("armeabi-v7a", "x86", "x86_64");
			proj.SetProperty ("EmbedAssembliesIntoApk", embedAssemblies.ToString ());
			proj.SetDefaultTargetDevice ();
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				string apiLevel;
				proj.TargetFrameworkVersion = b.LatestTargetFrameworkVersion (out apiLevel);

				// TODO: We aren't sure how to support preview bindings in .NET6 yet.
				if (Builder.UseDotNet && apiLevel == "31") {
					apiLevel = "30";
					proj.TargetFrameworkVersion = "v11.0";
				}

				proj.AndroidManifest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" android:versionCode=""1"" android:versionName=""1.0"" package=""UnnamedProject.UnnamedProject"">
	<uses-sdk android:minSdkVersion=""24"" android:targetSdkVersion=""{apiLevel}"" />
	<application android:label=""${{PROJECT_NAME}}"">
	</application >
</manifest>";
				Assert.True (b.Install (proj), "Project should have installed.");
				ClearAdbLogcat ();
				b.BuildLogFile = "run.log";
				Assert.True (b.RunTarget (proj, "StartAndroidActivity", doNotCleanupOnUpdate: true), "Project should have run.");

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
