using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xamarin.ProjectTools;
using Xamarin.Tools.Zip;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class BundleToolTests : BaseTest
	{
		XamarinAndroidApplicationProject project;
		ProjectBuilder builder;
		string intermediate;
		string bin;

		[OneTimeSetUp]
		public void OneTimeSetUp ()
		{
			project = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			//NOTE: this is here to enable adb shell run-as
			project.AndroidManifest = project.AndroidManifest.Replace ("<application ", "<application android:debuggable=\"true\" ");
			project.SetProperty (project.ReleaseProperties, "AndroidPackageFormat", "aab");

			builder = CreateApkBuilder (Path.Combine ("temp", TestName));
			Assert.IsTrue (builder.Build (project), "Build should have succeeded.");

			var projectDir = Path.Combine (Root, builder.ProjectDirectory);
			intermediate = Path.Combine (projectDir, project.IntermediateOutputPath);
			bin = Path.Combine (projectDir, project.OutputPath);
		}

		[OneTimeTearDown]
		public void OneTimeTearDown ()
		{
			builder.Dispose ();
		}

		string [] ListArchiveContents (string archive)
		{
			var entries = new List<string> ();
			using (var zip = ZipArchive.Open (archive, FileMode.Open)) {
				foreach (var entry in zip) {
					entries.Add (entry.FullName);
				}
			}
			entries.Sort ();
			return entries.ToArray ();
		}

		[Test]
		public void BaseZip ()
		{
			var baseZip = Path.Combine (intermediate, "android", "bin", "base.zip");
			var contents = ListArchiveContents (baseZip);
			CollectionAssert.AreEqual (contents, new [] {
				"dex/classes.dex",
				"lib/arm64-v8a/libmono-btls-shared.so",
				"lib/arm64-v8a/libmonodroid.so",
				"lib/arm64-v8a/libmono-native.so",
				"lib/arm64-v8a/libmonosgen-2.0.so",
				"lib/armeabi-v7a/libmono-btls-shared.so",
				"lib/armeabi-v7a/libmonodroid.so",
				"lib/armeabi-v7a/libmono-native.so",
				"lib/armeabi-v7a/libmonosgen-2.0.so",
				"manifest/AndroidManifest.xml",
				"res/drawable-hdpi-v4/icon.png",
				"res/drawable-mdpi-v4/icon.png",
				"res/drawable-xhdpi-v4/icon.png",
				"res/drawable-xxhdpi-v4/icon.png",
				"res/drawable-xxxhdpi-v4/icon.png",
				"res/layout/main.xml",
				"resources.pb",
				"root/assemblies/Java.Interop.dll",
				"root/assemblies/Mono.Android.dll",
				"root/assemblies/mscorlib.dll",
				"root/assemblies/System.Core.dll",
				"root/assemblies/System.dll",
				"root/assemblies/System.Runtime.Serialization.dll",
				"root/assemblies/UnnamedProject.dll",
				"root/NOTICE",
				"root/typemap.jm",
				"root/typemap.mj"
			});
		}

		[Test]
		public void AppBundle ()
		{
			var aab = Path.Combine (intermediate, "android", "bin", "UnnamedProject.UnnamedProject.aab");
			FileAssert.Exists (aab);
			var contents = ListArchiveContents (aab);
			CollectionAssert.AreEqual (contents, new [] {
				"base/dex/classes.dex",
				"base/lib/arm64-v8a/libmono-btls-shared.so",
				"base/lib/arm64-v8a/libmonodroid.so",
				"base/lib/arm64-v8a/libmono-native.so",
				"base/lib/arm64-v8a/libmonosgen-2.0.so",
				"base/lib/armeabi-v7a/libmono-btls-shared.so",
				"base/lib/armeabi-v7a/libmonodroid.so",
				"base/lib/armeabi-v7a/libmono-native.so",
				"base/lib/armeabi-v7a/libmonosgen-2.0.so",
				"base/manifest/AndroidManifest.xml",
				"base/native.pb",
				"base/res/drawable-hdpi-v4/icon.png",
				"base/res/drawable-mdpi-v4/icon.png",
				"base/res/drawable-xhdpi-v4/icon.png",
				"base/res/drawable-xxhdpi-v4/icon.png",
				"base/res/drawable-xxxhdpi-v4/icon.png",
				"base/res/layout/main.xml",
				"base/resources.pb",
				"base/root/assemblies/Java.Interop.dll",
				"base/root/assemblies/Mono.Android.dll",
				"base/root/assemblies/mscorlib.dll",
				"base/root/assemblies/System.Core.dll",
				"base/root/assemblies/System.dll",
				"base/root/assemblies/System.Runtime.Serialization.dll",
				"base/root/assemblies/UnnamedProject.dll",
				"base/root/NOTICE",
				"base/root/typemap.jm",
				"base/root/typemap.mj",
				"BundleConfig.pb"
			});
		}

		[Test]
		public void AppBundleSigned ()
		{
			var aab = Path.Combine (bin, "UnnamedProject.UnnamedProject-Signed.aab");
			FileAssert.Exists (aab);
			var contents = ListArchiveContents (aab);
			Assert.IsTrue (StringAssertEx.ContainsText (contents, "META-INF/MANIFEST.MF"), $"{aab} is not signed!");
		}

		[Test]
		public void ApkSet ()
		{
			if (!HasDevices)
				Assert.Ignore ("Skipping Installation. No devices available.");

			Assert.IsTrue (builder.RunTarget (project, "Install"), "App should have installed.");

			var aab = Path.Combine (intermediate, "android", "bin", "UnnamedProject.UnnamedProject.apks");
			FileAssert.Exists (aab);
			// Expecting: splits/base-arm64_v8a.apk, splits/base-master.apk, splits/base-xxxhdpi.apk
			var contents = ListArchiveContents (aab).Where (a => a.EndsWith (".apk", StringComparison.OrdinalIgnoreCase)).ToArray ();
			Assert.AreEqual (3, contents.Length, "Expecting three APKs!");
		}
	}
}
