using System;
using System.Linq;
using NUnit.Framework;
using Xamarin.ProjectTools;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Xamarin.Android.Build.Tests
{
	public class ManifestTest : BaseTest
	{
		readonly string TargetSdkManifest = @"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" android:versionCode=""1"" android:versionName=""1.0"" package=""Bug12935.Bug12935"">
	<uses-sdk android:targetSdkVersion=""{0}""/>
	<application android:label=""Bug12935"">
	</application>
</manifest>";

		readonly string ElementOrderManifest = @"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" android:versionCode=""1"" android:versionName=""1.0"" package=""CheckElementReOrdering.CheckElementReOrdering"">
	<uses-sdk android:targetSdkVersion=""21""/>
	<application android:label=""CheckElementReOrdering"">
	</application>
	<permission android:name=""com.xamarin.test.TEST"" android:label=""Test Permission"" />
	<permissionTree android:name=""com.xamarin.test"" />
	<permissionGroup android:name=""group1"" />
	<uses-feature android:name=""android.hardware.camera"" />
	<supports-gl-texture android:name=""GL_OES_compressed_ETC1_RGB8_texture"" />
	<uses-permission android:name=""android.permission.CAMERA"" />
</manifest>";

		readonly string ScreenOrientationActivity = @"
using System;

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;

namespace Bug12935
{
	[Activity (Label = ""Bug12935"",
		MainLauncher = true,
		Icon = ""@drawable/icon"",
		ScreenOrientation = ScreenOrientation.SensorPortrait
	)]
	public class MainActivity : Activity
	{
		int count = 1;

		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);
		}
	}
}
";

		[Test]
		public void Bug12935 ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
			};
			proj.MainActivity = ScreenOrientationActivity;
			var directory = "temp/Bug12935";
			using (var builder = CreateApkBuilder (directory)) {

				proj.TargetFrameworkVersion = "v4.2";
				proj.AndroidManifest = string.Format (TargetSdkManifest, "17");
				Assert.IsTrue (builder.Build (proj), "Build for TargetFrameworkVersion 17 should have succeeded");
				var manifestFile = Path.Combine (Root, builder.ProjectDirectory, proj.IntermediateOutputPath, "android", "AndroidManifest.xml");

				XDocument doc = XDocument.Load (manifestFile);
				var ns = doc.Root.GetNamespaceOfPrefix ("android");
				var screenOrientationXName = XName.Get ("screenOrientation", ns.NamespaceName);
				var targetSdkXName = XName.Get ("targetSdkVersion", ns.NamespaceName);

				var usesSdk = doc.XPathSelectElement ("/manifest/uses-sdk");
				Assert.IsNotNull (usesSdk, "Failed to read the uses-sdk element");
				var targetSdk = usesSdk.Attribute (targetSdkXName);
				Assert.AreEqual ("17", targetSdk.Value, "targetSdkVersion should have been 17");

				var activityElement = doc.XPathSelectElement ("/manifest/application/activity");
				Assert.IsNotNull (activityElement, "Failed to read the activity element");
				var screenOrientation = activityElement.Attribute (screenOrientationXName);
				Assert.IsNotNull (screenOrientation, "activity element did not contain a android:screenOrientation attribute");
				Assert.AreEqual ("sensorPortrait", screenOrientation.Value, "screenOrientation should have been sensorPortrait");

				builder.Cleanup ();
				proj.TargetFrameworkVersion = "v4.1";
				proj.AndroidManifest = string.Format (TargetSdkManifest, "16");
				Assert.IsTrue (builder.Build (proj), "Build for TargetFrameworkVersion 16 should have succeeded");

				doc = XDocument.Load (manifestFile);
				usesSdk = doc.XPathSelectElement ("/manifest/uses-sdk");
				Assert.IsNotNull (usesSdk, "Failed to read the uses-sdk element");
				targetSdk = usesSdk.Attribute (targetSdkXName);
				Assert.AreEqual ("16", targetSdk.Value, "targetSdkVersion should have been 16");
				activityElement = doc.XPathSelectElement ("/manifest/application/activity");
				Assert.IsNotNull (activityElement, "Failed to read the activity element");
				screenOrientation = activityElement.Attribute (screenOrientationXName);
				Assert.AreEqual ("sensorPortrait", screenOrientation.Value, "screenOrientation for targetSdkVersion 16 should have been sensorPortrait");

				builder.Cleanup ();
				builder.ThrowOnBuildFailure = false;
				proj.TargetFrameworkVersion = "v4.0.3";
				proj.AndroidManifest = string.Format (TargetSdkManifest, "15");
				Assert.IsFalse (builder.Build (proj), "Build for TargetFrameworkVersion 15 should have failed");
				StringAssert.Contains ("APT0000: ", builder.LastBuildOutput);
				StringAssert.Contains ("1 Error(s)", builder.LastBuildOutput);
			}
		}

		[Test]
		public void CheckElementReOrdering ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
			};
			proj.MainActivity = ScreenOrientationActivity;
			var directory = "temp/CheckElementReOrdering";
			using (var builder = CreateApkBuilder (directory)) {
				proj.AndroidManifest = ElementOrderManifest;
				Assert.IsTrue (builder.Build (proj), "Build should have succeeded");
				var manifestFile = Path.Combine (Root, builder.ProjectDirectory, proj.IntermediateOutputPath, "android", "AndroidManifest.xml");
				XDocument doc = XDocument.Load (manifestFile);
				var ns = doc.Root.GetNamespaceOfPrefix ("android");
				var manifest = doc.Element ("manifest");
				Assert.IsNotNull (manifest, "manifest element should not be null.");
				var app = manifest.Element ("application");
				Assert.IsNotNull (app, "application element should not be null.");
				Assert.AreEqual (0, app.ElementsAfterSelf ().Count (),
					"There should be no elements after the application element");
			}
		}

		[Test]
		public void IntentFilterData ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
			};
			string declHead = "public class MainActivity";
			string intentFilter = @"[IntentFilter (new string [] {""action1""},
				DataPath = ""foo"",
				DataPathPattern = ""foo*"",
				DataPathPrefix = ""foo"",
				Label = ""testTarget""
				)]
				";
			proj.MainActivity = proj.DefaultMainActivity.Replace (declHead, intentFilter + declHead);
			using (var b = CreateApkBuilder ("temp/IntentFilterData", true, false)) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				var manifest = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android", "AndroidManifest.xml");
				var doc = XDocument.Load (manifest);
				var nsResolver = new XmlNamespaceManager (new NameTable ());
				nsResolver.AddNamespace ("android", "http://schemas.android.com/apk/res/android");
				Assert.AreEqual (3, doc.XPathSelectElements ("//intent-filter[@android:label='testTarget']/data", nsResolver).Count (), "intent-filter/data count mismatch.");
			}
		}

		[Test]
		public void IntentFilterDataLists ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
			};
			string declHead = "public class MainActivity";
			string intentFilter = @"[IntentFilter (new string [] {""action1""},
				DataPaths = new string [] {""foo"", ""bar""},
				DataPathPatterns = new string [] {""foo*"", ""bar*""},
				DataPathPrefixes = new string [] {""foo"", ""bar""},
				DataHosts = new string [] {""foo.com"", ""bar.com""},
				DataPorts = new string [] {""10000"", ""20000""},
				DataSchemes = new string [] {""http"", ""ftp""},
				DataMimeTypes = new string [] {""text/html"", ""text/xml""},
				Label = ""testTarget""
				)]
				";
			proj.MainActivity = proj.DefaultMainActivity.Replace (declHead, intentFilter + declHead);
			using (var b = CreateApkBuilder ("temp/IntentFilterDataLists", true, false)) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				var manifest = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android", "AndroidManifest.xml");
				var doc = XDocument.Load (manifest);
				var nsResolver = new XmlNamespaceManager (new NameTable ());
				nsResolver.AddNamespace ("android", "http://schemas.android.com/apk/res/android");
				Assert.AreEqual (14, doc.XPathSelectElements ("//intent-filter[@android:label='testTarget']/data", nsResolver).Count (), "intent-filter/data count mismatch.");
			}
		}

		[Test]
		public void IntentFilterMultipleItems ()
		{
			// somehow the tests above passed but this example failed.
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
			};
			proj.MainActivity = proj.DefaultMainActivity.Replace (
				"public class MainActivity ",
				@"[IntentFilter (new string []{""foo""}, DataSchemes=new string []{""http"", ""https""})] public class MainActivity ");
			var b = CreateApkBuilder ("temp/IntentFilterMultipleItems");
			Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
			var appsrc = File.ReadAllText (Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android", "AndroidManifest.xml"));
			Assert.IsTrue (appsrc.Contains ("<data android:scheme=\"http\""), "schemes:http");
			Assert.IsTrue (appsrc.Contains ("<data android:scheme=\"https\""), "schemes:https");
			b.Dispose ();
		}

		[Test]
		public void LayoutAttributeElement ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				TargetFrameworkVersion = "v7.0",
				IsRelease = true,
			};
			string declHead = "public class MainActivity";
			string layout = @"[Layout (DefaultWidth=""500dp"", DefaultHeight=""600dp"", Gravity=""center"", MinWidth=""300dp"", MinHeight=""400dp"")]";
			proj.MainActivity = proj.DefaultMainActivity.Replace (declHead, layout + declHead);
			using (var b = CreateApkBuilder ("temp/LayoutAttributeElement", true, false)) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				var manifest = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android", "AndroidManifest.xml");
				var doc = XDocument.Load (manifest);
				var nsResolver = new XmlNamespaceManager (new NameTable ());
				nsResolver.AddNamespace ("android", "http://schemas.android.com/apk/res/android");
				var le = doc.XPathSelectElement ("//layout") as XElement;
				Assert.IsNotNull (le, "no layout element found");
				Assert.IsTrue (doc.XPathSelectElements ("//layout[@android:defaultWidth='500dp' and @android:defaultHeight='600dp' and @android:gravity='center' and @android:minWidth='300dp' and @android:minHeight='400dp']", nsResolver).Any (),
							   "'layout' element is not generated as expected.");
			}
		}

		[Test]
		public void DirectBootAwareAttribute ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				TargetFrameworkVersion = "v7.0",
				IsRelease = true,
			};
			string attrHead = "[Activity (";
			string attr = @"[Activity (DirectBootAware=true, ";
			proj.MainActivity = proj.DefaultMainActivity.Replace (attrHead, attr);
			proj.OtherBuildItems.Add (new BuildItem (BuildActions.Compile, "MyService.cs") {
				TextContent = () => "using Android.App; [Service (DirectBootAware = true)] public class MyService : Service { public override Android.OS.IBinder OnBind (Android.Content.Intent intent) { return null; } }"
			});
			proj.OtherBuildItems.Add (new BuildItem (BuildActions.Compile, "MyApplication.cs") {
				TextContent = () => "using Android.App; [Application (DirectBootAware = true)] public class MyApplication : Application {}"
			});
			using (var b = CreateApkBuilder ("temp/DirectBootAwareAttribute", true, false)) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				var manifest = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android", "AndroidManifest.xml");
				var doc = XDocument.Load (manifest);
				var nsResolver = new XmlNamespaceManager (new NameTable ());
				nsResolver.AddNamespace ("android", "http://schemas.android.com/apk/res/android");
				var le = doc.XPathSelectElement ("//activity") as XElement;
				Assert.IsNotNull (le, "no activity element found");
				Assert.IsTrue (doc.XPathSelectElements ("//activity[@android:directBootAware='true']", nsResolver).Any (),
						   "'activity' element is not generated as expected.");
			}
		}

		[Test]
		public void ManifestPlaceholders ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
			};
			proj.AndroidManifest = proj.AndroidManifest.Replace ("application android:label=\"${PROJECT_NAME}\"", "application android:label=\"${ph1}\" x='${ph2}' ");
			proj.SetProperty ("AndroidManifestPlaceholders", "ph2=a=b\\c;ph1=val1");
			using (var builder = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name), false, false)) {
				builder.Build (proj);
				var manifest = builder.Output.GetIntermediaryAsText (Root, Path.Combine ("android", "AndroidManifest.xml"));
				Assert.IsTrue (manifest.Contains ("application android:label=\"val1\""), "#1");
				Assert.IsTrue (manifest.Contains (" x=\"a=b\\c\"".Replace ('\\', Path.DirectorySeparatorChar)), "#2");
			}
		}

		[Test]
		public void ManifestPlaceHolders2 ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
			};
			var s = proj.AndroidManifest.Replace ("<application android:label=\"${PROJECT_NAME}\"", "<application android:label=\"${FOOBARNAME}\"");
			Assert.AreNotEqual (proj.AndroidManifest, s, "#0");
			proj.SetProperty ("AndroidManifestPlaceholders", "FOOBARNAME=AAAAAAAA");
			proj.AndroidManifest = s;
			using (var builder = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name), false, false)) {
				Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
				var manifest = builder.Output.GetIntermediaryAsText (Root, "android/AndroidManifest.xml");
				Assert.IsTrue (manifest.Contains ("AAAAAAAA"), "#1");
			}
		}

		static object[] DebuggerAttributeCases = new object[] {
			// DebugType, isRelease, extpected
			new object[] { "", true, false, },
			new object[] { "", false, true, },
			new object[] { "None", true, false, },
			new object[] { "None", false, false, },
			new object[] { "PdbOnly", true, false, },
			new object[] { "PdbOnly", false, true, },
			new object[] { "Full", true, false, },
			new object[] { "Full", false, true, },
			new object[] { "Portable", true, false, },
			new object[] { "Portable", false, true, },
		};

		[Test]
		[TestCaseSource ("DebuggerAttributeCases")]
		public void DebuggerAttribute (string debugType, bool isRelease, bool expected)
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
			};
			proj.SetProperty (isRelease ? proj.ReleaseProperties : proj.DebugProperties, "DebugType", debugType);
			using (var builder = CreateApkBuilder (Path.Combine ("temp", $"DebuggerAttribute_{debugType}_{isRelease}_{expected}"), false, false)) {
				Assert.IsTrue (builder.Build (proj), "Build should have succeeded");
				var manifest = builder.Output.GetIntermediaryAsText (Root, Path.Combine ("android", "AndroidManifest.xml"));
				Assert.AreEqual (expected, manifest.Contains ("android:debuggable=\"true\""), $"Manifest  {(expected ? "should" : "should not")} contain the andorid:debuggable attribute");
			}
		}
	}
}
