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
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Category ("UsesDevice")]
	public class MonoAndroidExportTest : DeviceTest
	{
		[Test]
		public void MonoAndroidExportReferencedAppStarts (
			[Values] bool embedAssemblies,
			[Values] bool isRelease,
			[Values] AndroidRuntime runtime)
		{
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			AssertCommercialBuild ();
			var proj = new XamarinAndroidApplicationProject (packageName: PackageUtils.MakePackageName (runtime)) {
				IsRelease = isRelease,
				References = {
					new BuildItem.Reference ("Mono.Android.Export"),
				},
			};
			proj.SetRuntime (runtime);
			if (runtime == AndroidRuntime.CoreCLR || runtime == AndroidRuntime.NativeAOT) {
				proj.SetProperty ("_AndroidTypeMapImplementation", "trimmable");
			}
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
			proj.SetAndroidSupportedAbis (DeviceAbi);
			proj.SetProperty ("EmbedAssembliesIntoApk", embedAssemblies.ToString ());
			proj.SetDefaultTargetDevice ();
			using (var b = CreateApkBuilder ()) {
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
					Path.Combine (Root, b.ProjectDirectory, "logcat.log"), InstallAndRunTests.ActivityStartTimeoutInSeconds), "Activity should have started.");
				string expectedLogcatOutput = "ContainsExportedMethods: constructed! Handle=";
				Assert.IsTrue (MonitorAdbLogcat ((line) => {
					return line.Contains (expectedLogcatOutput);
				}, Path.Combine (Root, b.ProjectDirectory, "startup-logcat.log"), 45), $"Output did not contain {expectedLogcatOutput}!");
				Assert.True (b.Uninstall (proj), "Project should have uninstalled.");
			}
		}

		[Test]
		public void ExportedMembersSurviveGarbageCollection (
			[Values] bool isRelease,
			[Values] AndroidRuntime runtime)
		{
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			AssertCommercialBuild ();
			var proj = new XamarinAndroidApplicationProject (packageName: PackageUtils.MakePackageName (runtime)) {
				IsRelease = isRelease,
				References = {
					new BuildItem.Reference ("Mono.Android.Export"),
				},
			};
			proj.SetRuntime (runtime);
			if (runtime == AndroidRuntime.CoreCLR || runtime == AndroidRuntime.NativeAOT) {
				proj.SetProperty ("_AndroidTypeMapImplementation", "trimmable");
			}
			proj.Sources.Add (new BuildItem.Source ("ContainsExportedMethods.cs") {
				TextContent = () => @"using System;
using Java.Interop;

namespace UnnamedProject {
	class ContainsExportedMethods : Java.Lang.Object {
		[Export]
		public void Exported ()
		{
			Console.WriteLine (""# ExportedCallbackInvoked"");
		}
	}
}
",
			});
			proj.MainActivity = @"using System;
using Android.App;
using Android.OS;
using Android.Runtime;

namespace UnnamedProject
{
	[Activity (Label = ""UnnamedProject"", MainLauncher = true, Icon = ""@drawable/icon"")]
	public class MainActivity : Activity {
		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);

			var foo = new ContainsExportedMethods ();

			// Force GC to verify the registered callback does not rely on transient state.
			for (int i = 0; i < 10; i++) {
				GC.Collect ();
				GC.WaitForPendingFinalizers ();
			}

			// Invoke the [Export] method through JNI to validate the generated callback path.
			IntPtr klass = JNIEnv.GetObjectClass (foo.Handle);
			IntPtr methodId = JNIEnv.GetMethodID (klass, ""Exported"", ""()V"");
			JNIEnv.CallVoidMethod (foo.Handle, methodId);

			Console.WriteLine (""# ExportCallbackSurvivedGC"");
		}
	}
}";
			proj.SetAndroidSupportedAbis (DeviceAbi);
			proj.SetDefaultTargetDevice ();
			using (var b = CreateApkBuilder ()) {
				b.LatestTargetFrameworkVersion (out string apiLevel);
				proj.SupportedOSPlatformVersion = "24.0";
				proj.AndroidManifest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" android:versionCode=""1"" android:versionName=""1.0"" package=""{proj.PackageName}"">
	<uses-sdk android:targetSdkVersion=""{apiLevel}"" />
	<application android:label=""${{PROJECT_NAME}}"">
	</application >
</manifest>";
				Assert.True (b.Install (proj), "Project should have installed.");
				RunProjectAndAssert (proj, b, doNotCleanupOnUpdate: true);
				Assert.True (WaitForActivityToStart (proj.PackageName, "MainActivity",
					Path.Combine (Root, b.ProjectDirectory, "logcat.log"), InstallAndRunTests.ActivityStartTimeoutInSeconds), "Activity should have started.");
				string expectedLogcatOutput = "ExportCallbackSurvivedGC";
				Assert.IsTrue (MonitorAdbLogcat ((line) => {
					return line.Contains (expectedLogcatOutput);
				}, Path.Combine (Root, b.ProjectDirectory, "startup-logcat.log"), 45), $"Output did not contain {expectedLogcatOutput}!");
				Assert.True (b.Uninstall (proj), "Project should have uninstalled.");
			}
		}
	}
}
