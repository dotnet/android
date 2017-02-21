﻿using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[Parallelizable (ParallelScope.Fixtures)]
	public class BuildTest : BaseTest
	{
		[Test]
		public void BuildReleaseApplication ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
			};
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name))) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
			}
		}

		[Test]
		public void BuildReleaseApplicationWithNugetPackages ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				Packages = {
					KnownPackages.AndroidSupportV4_21_0_3_0,
				},
			};
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name))) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				DirectoryAssert.Exists (Path.Combine (Root, "temp","packages", "Xamarin.Android.Support.v4.21.0.3.0"),
										"Nuget Package Xamarin.Android.Support.v4.21.0.3.0 should have been restored.");
			}
		}

		static object [] TlsProviderTestCases =
		{
			// androidTlsProvider, isRelease, extpected
			new object[] { "", true, false, },
			new object[] { "default", true, false, },
			new object[] { "legacy", true, false, },
			new object[] { "btls", true, true, }
		};


		[Test]
		[TestCaseSource ("TlsProviderTestCases")]
		public void BuildWithTlsProvider (string androidTlsProvider, bool isRelease, bool expected)
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
			};
			using (var b = CreateApkBuilder (Path.Combine ("temp", $"BuildWithTlsProvider_{androidTlsProvider}_{isRelease}_{expected}"))) {
				proj.SetProperty ("AndroidTlsProvider", androidTlsProvider);
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				var apk = Path.Combine(Root, b.ProjectDirectory,
					proj.IntermediateOutputPath,"android", "bin", "UnnamedProject.UnnamedProject.apk");
				using (var zipFile = ZipHelper.OpenZip (apk)) {
					if (expected) {
						Assert.IsNotNull (ZipHelper.ReadFileFromZip (zipFile,
						   "lib/armeabi-v7a/libmono-btls-shared.so"),
						   "lib/armeabi-v7a/libmono-btls-shared.so should exist in the apk.");
					}
					else {
						Assert.IsNull (ZipHelper.ReadFileFromZip (zipFile,
						   "lib/armeabi-v7a/libmono-btls-shared.so"),
						   "lib/armeabi-v7a/libmono-btls-shared.so should not exist in the apk.");
					}
				}
			}
		}

		[Test]
		public void BuildAfterAddingNuget ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				TargetFrameworkVersion = "7.1",
			};
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name))) {
				Assert.IsTrue (b.Build (proj), "first build should have succeeded.");
				proj.Packages.Add (KnownPackages.SupportV7CardView_24_2_1);
				b.Save (proj, doNotCleanupOnUpdate: true);
				Assert.IsTrue (b.Build (proj), "second build should have succeeded.");
				var doc = File.ReadAllText (Path.Combine (b.Root, b.ProjectDirectory, proj.IntermediateOutputPath, "resourcepaths.cache"));
				Assert.IsTrue (doc.Contains ("Xamarin.Android.Support.v7.CardView/24.2.1"), "CardView should be resolved as a reference.");
			}
		}

		[Test]
		public void CheckTargetFrameworkVersion ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
			};
			proj.SetProperty ("TargetFrameworkVersion", "v2.3");
			using (var builder = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name))) {
				Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
				StringAssert.Contains ($"TargetFrameworkVersion: v2.3", builder.LastBuildOutput, "TargetFrameworkVerson should be v2.3");
				Assert.IsTrue (builder.Build (proj, parameters: new [] { "TargetFrameworkVersion=v4.4" }), "Build should have succeeded.");
				StringAssert.Contains ($"TargetFrameworkVersion: v4.4", builder.LastBuildOutput, "TargetFrameworkVerson should be v4.4");

			}
		}
	}
}

