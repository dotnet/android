using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Text;
using NUnit.Framework;
using Xamarin.ProjectTools;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Category ("UsesDevice")]
	public class MonoAndroidExportTest : DeviceTest
	{
		[TestCase (AndroidRuntime.CoreCLR)]
		[TestCase (AndroidRuntime.NativeAOT)]
		public void JavaOnlyExportsSurviveFullTrimming (AndroidRuntime runtime)
		{
			if (IgnoreUnsupportedConfiguration (runtime, release: true)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject (packageName: PackageUtils.MakePackageName (runtime, "javaonlyexports")) {
				IsRelease = true,
				References = {
					new BuildItem.Reference ("Mono.Android.Export"),
				},
			};
			proj.SetRuntime (runtime);
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");
			proj.SetRuntimeIdentifiers (new [] { DeviceAbi });
			proj.SetDefaultTargetDevice ();
			proj.SetProperty ("TrimMode", "full");
			proj.SetProperty ("PublishReadyToRun", "false");
			proj.Sources.Add (new BuildItem.Source ("JavaOnlyExports.cs") {
				TextContent = () => """
					using Android.Runtime;
					using Java.Interop;

					namespace UnnamedProject;

					[Register ("example.JavaOnlyPeer")]
					public class JavaOnlyPeer : Java.Lang.Object
					{
						readonly int value;
						public JavaOnlyPeer () { value = 42; }
						[Export (".ctor", SuperArgumentsString = "")]
						public JavaOnlyPeer (int value) { this.value = value; }
						[Export ("getValue")]
						public int GetValue () => value;
						[Export ("echo")]
						public string Echo (string text) => text + "!";
					}

					[Register ("example.JavaOnlyFieldPeer")]
					public class JavaOnlyFieldPeer : Java.Lang.Object
					{
						public JavaOnlyFieldPeer () { }
						protected JavaOnlyFieldPeer (System.IntPtr handle, JniHandleOwnership ownership) : base (handle, ownership) { }
						[ExportField ("VALUE")]
						public static int InitializeValue () => 43;
						[ExportField ("instanceValue")]
						public int InitializeInstanceValue () => 44;
					}
					""",
			});
			var marker = "JAVA_ONLY_EXPORTS_PASSED_" + Guid.NewGuid ().ToString ("N");
			proj.AndroidJavaSources.Add (new AndroidItem.AndroidJavaSource ("example/ExportCaller.java") {
				Encoding = Encoding.ASCII,
				Metadata = { { "Bind", "False" } },
				TextContent = () => $$"""
					package example;
					public class ExportCaller {
					    public static void run() {
					        JavaOnlyPeer peer = new JavaOnlyPeer();
					        if (peer.getValue() != 42 || new JavaOnlyPeer(41).getValue() != 41 ||
					            !"Java!".equals(peer.echo("Java")) || JavaOnlyFieldPeer.VALUE != 43 ||
					            new JavaOnlyFieldPeer().instanceValue != 44) {
					            throw new AssertionError("Java-only exports returned incorrect values");
					        }
					        android.util.Log.i("JavaOnlyExports", "{{marker}}");
					    }
					}
					""",
			});
			proj.MainActivity = proj.DefaultMainActivity.Replace ("//${AFTER_ONCREATE}", """
				var klass = Android.Runtime.JNIEnv.FindClass ("example/ExportCaller");
				try {
					var method = Android.Runtime.JNIEnv.GetStaticMethodID (klass, "run", "()V");
					Android.Runtime.JNIEnv.CallStaticVoidMethod (klass, method);
				} finally {
					Android.Runtime.JNIEnv.DeleteGlobalRef (klass);
				}
				""");

			using var builder = CreateApkBuilder ();
			bool installed = false;
			try {
				installed = builder.Install (proj);
				Assert.IsTrue (installed, "The fully trimmed Java-only export app should install.");
				Assert.IsTrue (MonitorAdbLogcat (line => line.Contains (marker, StringComparison.Ordinal),
					Path.Combine (Root, builder.ProjectDirectory, "java-only-exports-logcat.log"), ActivityStartTimeoutInSeconds,
					onMonitoringStarted: () => StartActivityAndAssert (proj)),
					"Handwritten Java must instantiate managed peers and invoke exports after full trimming.");
			} finally {
				if (installed) {
					RunAdbCommand ($"uninstall {proj.PackageName}");
				}
			}
		}

		[Test]
		public void MonoAndroidExportReferencedAppStarts (
			[Values] bool embedAssemblies,
			[Values] bool isRelease,
			[Values (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
		{
			if (runtime == AndroidRuntime.NativeAOT) {
				Assert.Ignore ("NativeAOT does not support Mono.Android.Export");
			}
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject (packageName: PackageUtils.MakePackageName (runtime)) {
				IsRelease = isRelease,
				References = {
					new BuildItem.Reference ("Mono.Android.Export"),
				},
			};
			proj.SetRuntime (runtime);
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
			proj.SetRuntimeIdentifiers (new[] { DeviceAbi });
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
			[Values (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
		{
			if (runtime == AndroidRuntime.NativeAOT) {
				Assert.Ignore ("NativeAOT does not support Mono.Android.Export");
			}
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject (packageName: PackageUtils.MakePackageName (runtime)) {
				IsRelease = isRelease,
				References = {
					new BuildItem.Reference ("Mono.Android.Export"),
				},
			};
			proj.SetRuntime (runtime);
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

			// Force GC to collect any unrooted delegates
			for (int i = 0; i < 10; i++) {
				GC.Collect ();
				GC.WaitForPendingFinalizers ();
			}

			// Invoke the [Export] method through JNI (Java -> native delegate -> C#)
			// This path crashes with SIGABRT if the delegate was garbage collected
			IntPtr klass = JNIEnv.GetObjectClass (foo.Handle);
			IntPtr methodId = JNIEnv.GetMethodID (klass, ""Exported"", ""()V"");
			JNIEnv.CallVoidMethod (foo.Handle, methodId);

			Console.WriteLine (""# ExportCallbackSurvivedGC"");
		}
	}
}";
			proj.SetRuntimeIdentifiers (new[] { DeviceAbi });
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
