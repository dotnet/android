using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using NUnit.Framework;
using Xamarin.ProjectTools;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Category ("UsesDevice")]
	public class DebuggingTest : DeviceTest
	{
		const int DEBUGGER_MAX_CONNECTIONS = 100;
		const int DEBUGGER_CONNECTION_TIMEOUT = 3000;

		[TearDown]
		public void ClearDebugProperties ()
		{
			ClearDebugProperty ();
		}

		void SetTargetFrameworkAndManifest(XamarinAndroidApplicationProject proj, Builder builder, int? apiLevelOverride)
		{
			builder.LatestTargetFrameworkVersion (out string apiLevel);
			proj.SupportedOSPlatformVersion = "24";
			proj.AndroidManifest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" android:versionCode=""1"" android:versionName=""1.0"" package=""{proj.PackageName}"">
	<uses-sdk android:targetSdkVersion=""{apiLevelOverride?.ToString () ?? apiLevel}"" />
	<application android:label=""${{PROJECT_NAME}}"">
	</application >
</manifest>";
		}

		int FindTextInFile (string file, string text)
		{
			int lineNumber = 1;
			foreach (var line in File.ReadAllLines (file)) {
				if (line.Contains (text)) {
					return lineNumber;
				}
				lineNumber++;
			}
			Console.WriteLine ($"Could not find '{text}' in '{file}'");
			return -1;
		}

		[Test]
		public void ApplicationRunsWithoutDebugger ([Values] bool isRelease, [Values] bool extractNativeLibs, [Values] bool useEmbeddedDex, [Values (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
		{
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			// TODO: NativeAOT fails with the following exception:
			//
			// FATAL UNHANDLED EXCEPTION: System.InvalidCastException: Unable to convert instance of type 'AndroidX.AppCompat.Widget.AppCompatImageButton' to type 'AndroidX.AppCompat.Widget.Toolbar'.
			//    at Java.Interop.JavaObjectExtensions._JavaCast[TResult](IJavaObject) + 0x190
			//    at Android.Runtime.Extensions.JavaCast[TResult](IJavaObject) + 0x18
			//    at Xamarin.Forms.Platform.Android.FormsAppCompatActivity.OnCreate(Bundle, ActivationFlags) + 0x5bc
			//    at UnnamedProject.MainActivity.OnCreate(Bundle savedInstanceState) + 0x4c
			//    at Android.App.Activity.n_OnCreate_Landroid_os_Bundle_(IntPtr jnienv, IntPtr native__this, IntPtr native_savedInstanceState) + 0x7c
			if (runtime == AndroidRuntime.NativeAOT) {
				Assert.Ignore ("NativeAOT currently crashes with an exception.");
			}

			SwitchUser ();

			var proj = new XamarinFormsAndroidApplicationProject () {
				IsRelease = isRelease,
			};
			proj.SetRuntime (runtime);
			if (isRelease) {
				proj.SetRuntimeIdentifiers (new[] { DeviceAbi });
			}
			proj.SetDefaultTargetDevice ();
			if (isRelease) {
				// bundle tool does NOT support embeddedDex files it seems.
				useEmbeddedDex = false;
			}
			using (var b = CreateApkBuilder ()) {
				SetTargetFrameworkAndManifest (proj, b, null);
				proj.AndroidManifest = proj.AndroidManifest.Replace ("<application ", $"<application android:extractNativeLibs=\"{extractNativeLibs.ToString ().ToLowerInvariant ()}\" android:useEmbeddedDex=\"{useEmbeddedDex.ToString ().ToLowerInvariant ()}\" ");
				Assert.True (b.Install (proj), "Project should have installed.");
				var manifest = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android", "AndroidManifest.xml");
				AssertExtractNativeLibs (manifest, extractNativeLibs);
				RunProjectAndAssert (proj, b);
				Assert.True (WaitForActivityToStart (proj.PackageName, "MainActivity",
					Path.Combine (Root, b.ProjectDirectory, "logcat.log"), ActivityStartTimeoutInSeconds), "Activity should have started.");
				b.BuildLogFile = "uninstall.log";
				Assert.True (b.Uninstall (proj), "Project should have uninstalled.");
			}
		}

		[Test]
		public void ClassLibraryMainLauncherRuns ([Values] bool preloadAssemblies, [Values (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
		{
			bool isRelease = runtime == AndroidRuntime.NativeAOT;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			// TODO: NativeAOT currently dies with a Java android.os.DeadObjectException exception (GC issue?):
			//
			// Exception thrown during dispatchAppVisibility Window{74689fa u0 com.xamarin.classlibrarymainlauncherruns/com.xamarin.classlibrarymainlauncherruns.MainActivity EXITING}
			// android.os.DeadObjectException
			//         at android.os.BinderProxy.transactNative(Native Method)
			//         at android.os.BinderProxy.transact(BinderProxy.java:592)
			//         at android.view.IWindow$Stub$Proxy.dispatchAppVisibility(IWindow.java:538)
			//         at com.android.server.wm.WindowState.sendAppVisibilityToClients(WindowState.java:3183)
			//         at com.android.server.wm.WindowContainer.sendAppVisibilityToClients(WindowContainer.java:1233)
			//         at com.android.server.wm.WindowToken.setClientVisible(WindowToken.java:394)
			//         at com.android.server.wm.ActivityRecord.commitVisibility(ActivityRecord.java:5546)
			//         at com.android.server.wm.Transition.finishTransition(Transition.java:1485)
			//         at com.android.server.wm.TransitionController.finishTransition(TransitionController.java:1048)
			//         at com.android.server.wm.WindowOrganizerController.finishTransition(WindowOrganizerController.java:514)
			//         at android.window.IWindowOrganizerController$Stub.onTransact(IWindowOrganizerController.java:270)
			//         at com.android.server.wm.WindowOrganizerController.onTransact(WindowOrganizerController.java:230)
			//         at android.os.Binder.execTransactInternal(Binder.java:1446)
			//         at android.os.Binder.execTransact(Binder.java:1385)
			if (runtime == AndroidRuntime.NativeAOT) {
				Assert.Ignore ("NativeAOT currently dies at startup with a Java android.os.DeadObjectException exception");
			}

			SwitchUser ();

			var path = Path.Combine ("temp", TestName);

			var app = new XamarinAndroidApplicationProject {
				IsRelease = isRelease,
				ProjectName = "MyApp",
			};
			app.SetRuntime (runtime);
			app.SetDefaultTargetDevice ();
			app.SetProperty ("AndroidEnablePreloadAssemblies", preloadAssemblies.ToString ());

			var lib = new XamarinAndroidLibraryProject {
				ProjectName = "MyLibrary"
			};
			lib.Sources.Add (new BuildItem.Source ("MainActivity.cs") {
				TextContent = () => lib.ProcessSourceTemplate (app.DefaultMainActivity).Replace ("${JAVA_PACKAGENAME}", app.JavaPackageName),
			});
			lib.AndroidResources.Clear ();
			foreach (var resource in app.AndroidResources) {
				lib.AndroidResources.Add (resource);
			}
			var reference = $"..\\{lib.ProjectName}\\{lib.ProjectName}.csproj";
			app.References.Add (new BuildItem.ProjectReference (reference, lib.ProjectName, lib.ProjectGuid));

			// Remove the default MainActivity.cs & AndroidResources
			app.AndroidResources.Clear ();
			app.AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\layout\\foo.xml") {
				TextContent = () =>
@"<?xml version=""1.0"" encoding=""utf-8""?>
<LinearLayout
  xmlns:android=""http://schemas.android.com/apk/res/android""
  android:layout_width=""fill_parent""
  android:layout_height=""wrap_content""
/>"
			});
			app.Sources.Remove (app.GetItem ("MainActivity.cs"));

			using (var libBuilder = CreateDllBuilder (Path.Combine (path, lib.ProjectName)))
			using (var appBuilder = CreateApkBuilder (Path.Combine (path, app.ProjectName))) {
				SetTargetFrameworkAndManifest (app, appBuilder, null);
				Assert.IsTrue (libBuilder.Build (lib), "library build should have succeeded.");
				Assert.True (appBuilder.Install (app), "app should have installed.");
				RunProjectAndAssert (app, appBuilder);
				Assert.True (WaitForActivityToStart (app.PackageName, "MainActivity",
					Path.Combine (Root, appBuilder.ProjectDirectory, "logcat.log"), ActivityStartTimeoutInSeconds), "Activity should have started.");
			}
		}

	}
}
