using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

using NUnit.Framework;

namespace Xamarin.Android.Tools.Tests
{
	[TestFixture]
	public class AndroidAppManifestTests
	{
		[Test]
		public void Load ()
		{
			var versions    = new AndroidVersions (new AndroidVersion [0]);
			Assert.Throws<ArgumentNullException> (() => AndroidAppManifest.Load ((string) null, versions));
			Assert.Throws<ArgumentNullException> (() => AndroidAppManifest.Load ("filename", null));
			Assert.Throws<ArgumentNullException> (() => AndroidAppManifest.Load ((XDocument) null, versions));
			Assert.Throws<ArgumentNullException> (() => AndroidAppManifest.Load (GetTestAppManifest (), null));

			Assert.Throws<ArgumentException> (() => AndroidAppManifest.Load (XDocument.Parse ("<invalid-root/>"), versions));
		}

		[Test]
		public void ParsePermissions ()
		{
			var versions    = new AndroidVersions (new AndroidVersion [0]);
			var manifest    = AndroidAppManifest.Load (GetTestAppManifest (), versions);
			var permissions = manifest.AndroidPermissions.ToArray ();
			Assert.AreEqual (3, permissions.Length, "#1");
			Assert.IsTrue (permissions.Contains ("INTERNET"), "#2");
			Assert.IsTrue (permissions.Contains ("READ_CONTACTS"), "#3");
			Assert.IsTrue (permissions.Contains ("WRITE_CONTACTS"), "#4");
		}

		static XDocument GetTestAppManifest ()
		{
			using (var xml = typeof (AndroidAppManifestTests).Assembly.GetManifestResourceStream ("manifest-simplewidget.xml")) {
				return XDocument.Load (xml);
			}
		}

		[Test]
		public void GetLaunchableActivityNames ()
		{
			var versions	= new AndroidVersions (Array.Empty<AndroidVersion>());
			var manifest    = AndroidAppManifest.Load (GetTestAppManifest (), versions);
			var launchers   = manifest.GetLaunchableActivityNames ().ToList ();
			Assert.AreEqual (1,                             launchers.Count);
			Assert.AreEqual (".HasMultipleIntentFilters",	launchers [0]);
		}

		[Test]
		public void SetNewPermissions ()
		{
			var versions = new AndroidVersions (new AndroidVersion [0]);
			var manifest = AndroidAppManifest.Load (GetTestAppManifest (), versions);
			manifest.SetAndroidPermissions (new [] { "FOO" });

			var sb = new StringBuilder ();
			using (var writer = XmlWriter.Create (sb)) {
				manifest.Write (writer);
			}

			manifest    = AndroidAppManifest.Load (XDocument.Parse (sb.ToString ()), versions);
			Assert.AreEqual (1,     manifest.AndroidPermissions.Count (), "#1");
			Assert.AreEqual ("FOO", manifest.AndroidPermissions.ElementAt (0));
		}

		[Test]
		public void CanonicalizePackageName ()
		{
			Assert.Throws<ArgumentNullException>(() => AndroidAppManifest.CanonicalizePackageName (null));
			Assert.Throws<ArgumentException>(() => AndroidAppManifest.CanonicalizePackageName (""));
			Assert.Throws<ArgumentException>(() => AndroidAppManifest.CanonicalizePackageName ("  "));

			Assert.AreEqual ("A.A",
					AndroidAppManifest.CanonicalizePackageName ("A"));
			Assert.AreEqual ("Foo.Bar",
					AndroidAppManifest.CanonicalizePackageName ("Foo.Bar"));
			Assert.AreEqual ("foo_bar.foo_bar",
					AndroidAppManifest.CanonicalizePackageName ("foo-bar"));
			Assert.AreEqual ("x1.x1",
					AndroidAppManifest.CanonicalizePackageName ("1"));
			Assert.AreEqual ("x_1.x_2",
					AndroidAppManifest.CanonicalizePackageName ("_1._2"));
			Assert.AreEqual ("mfa1.x0.x2_2",
					AndroidAppManifest.CanonicalizePackageName ("mfa1.0.2_2"));
			Assert.AreEqual ("My.Cool_Assembly",
					AndroidAppManifest.CanonicalizePackageName ("My.Cool Assembly"));
			Assert.AreEqual ("x7Cats.x7Cats",
					AndroidAppManifest.CanonicalizePackageName ("7Cats"));
		}

		[Test]
		public void CanParseNonNumericSdkVersion ()
		{
			var versions    = new AndroidVersions (new AndroidVersion [0]);
			var doc         = XDocument.Parse (@"
				<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" android:versionCode=""1"" android:versionName=""1.0"" package=""com.xamarin.Foo"">
					<uses-sdk android:minSdkVersion=""L"" android:targetSdkVersion=""L"" />
					<application android:label=""Foo"" android:icon=""@drawable/ic_icon"">
					</application>
				</manifest>");
			var manifest    = AndroidAppManifest.Load (doc, versions);

			var mininum     = manifest.MinSdkVersion;
			var target      = manifest.TargetSdkVersion;

			Assert.IsTrue (mininum.HasValue);
			Assert.IsTrue (target.HasValue);
			Assert.AreEqual (21, mininum.Value);
			Assert.AreEqual (21, target.Value);
		}

		[Test]
		public void EnsureMinAndTargetSdkVersionsAreReadIndependently ()
		{
			// Regression test for https://bugzilla.xamarin.com/show_bug.cgi?id=21296
			var versions    = new AndroidVersions (new AndroidVersion [0]);
			var doc         = XDocument.Parse (@"
				<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" android:versionCode=""1"" android:versionName=""1.0"" package=""com.xamarin.Foo"">
					<uses-sdk android:minSdkVersion=""8"" android:targetSdkVersion=""12"" />
					<application android:label=""Foo"" android:icon=""@drawable/ic_icon"">
					</application>
				</manifest>");
			var manifest    = AndroidAppManifest.Load (doc, versions);

			var mininum     = manifest.MinSdkVersion;
			var target      = manifest.TargetSdkVersion;

			Assert.IsTrue (mininum.HasValue);
			Assert.IsTrue (target.HasValue);
			Assert.AreEqual (8, mininum.Value);
			Assert.AreEqual (12, target.Value);
		}

		[Test]
		public void EnsureUsesPermissionElementOrder ()
		{
			var versions    = new AndroidVersions (new AndroidVersion [0]);
			var manifest    = AndroidAppManifest.Create ("com.xamarin.test", "Xamarin Test", versions);
			manifest.SetAndroidPermissions (new string[] { "FOO" });
			var sb = new StringBuilder ();
			using (var writer = XmlWriter.Create (sb)) {
				manifest.Write (writer);
			}

			var doc         = XDocument.Parse (sb.ToString ());
			var app         = doc.Element ("manifest").Element ("application");
			Assert.IsNotNull (app, "Application element should exist");
			Assert.IsFalse (app.ElementsAfterSelf ().Any (x => x.Name == "uses-permission"));
			Assert.IsTrue (app.ElementsBeforeSelf ().Any (x => x.Name == "uses-permission"));
		}

		[Test]
		public void CanGetAppTheme ()
		{
			var versions    = new AndroidVersions (new AndroidVersion [0]);
			var doc         = XDocument.Parse (@"
				<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" android:versionCode=""1"" android:versionName=""1.0"" package=""com.xamarin.Foo"">
					<uses-sdk android:minSdkVersion=""8"" android:targetSdkVersion=""12"" />
					<application android:label=""Foo"" android:icon=""@drawable/ic_icon"" android:theme=""@android:style/Theme.Material.Light"">
					</application>
				</manifest>");
			var manifest    = AndroidAppManifest.Load (doc, versions);

			Assert.AreEqual ("@android:style/Theme.Material.Light", manifest.ApplicationTheme);
		}
	}
}
