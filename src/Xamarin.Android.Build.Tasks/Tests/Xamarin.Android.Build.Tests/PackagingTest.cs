﻿using System;
using System.IO;
using NUnit.Framework;
using Xamarin.ProjectTools;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Xamarin.Android.Build.Tests
{
	[Parallelizable (ParallelScope.Fixtures)]
	public class PackagingTest : BaseTest
	{
#pragma warning disable 414
		static object [] ManagedSymbolsArchiveSource = new object [] {
			//           isRelease, monoSymbolArchive, archiveShouldExists,
			new object[] { false    , false              , false },
			new object[] { true     , true               , true },
			new object[] { true     , false              , false },
		};
#pragma warning restore 414

		[Test]
		[TestCaseSource (nameof(ManagedSymbolsArchiveSource))]
		public void CheckManagedSymbolsArchive (bool isRelease, bool monoSymbolArchive, bool archiveShouldExists)
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
			};
			proj.SetProperty (proj.ReleaseProperties, "MonoSymbolArchive", monoSymbolArchive);
			proj.SetProperty (proj.ReleaseProperties, KnownProperties.AndroidCreatePackagePerAbi, "true");
			proj.SetProperty (proj.ReleaseProperties, KnownProperties.AndroidSupportedAbis, "armeabi-v7a;x86");
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name))) {
				b.Verbosity = Microsoft.Build.Framework.LoggerVerbosity.Diagnostic;
				b.ThrowOnBuildFailure = false;
				Assert.IsTrue (b.Build (proj), "first build failed");
				var outputPath = Path.Combine (Root, b.ProjectDirectory, proj.OutputPath);
				var archivePath = Path.Combine (outputPath, proj.PackageName + ".apk.mSYM");
				Assert.AreEqual (archiveShouldExists, Directory.Exists (archivePath),
					string.Format ("The msym archive {0} exist.", archiveShouldExists ? "should" : "should not"));
			}
		}

		[Test]
		public void CheckBuildIdIsUnique ()
		{
			Dictionary<string, string> buildIds = new Dictionary<string, string> ();
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
			};
			proj.SetProperty (proj.ReleaseProperties, "MonoSymbolArchive", "True");
			proj.SetProperty (proj.ReleaseProperties, "DebugSymbols", "true");
			proj.SetProperty (proj.ReleaseProperties, "DebugType", "PdbOnly");
			proj.SetProperty (proj.ReleaseProperties, KnownProperties.AndroidCreatePackagePerAbi, "true");
			proj.SetProperty (proj.ReleaseProperties, KnownProperties.AndroidSupportedAbis, "armeabi-v7a;x86");
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name))) {
				b.Verbosity = Microsoft.Build.Framework.LoggerVerbosity.Diagnostic;
				b.ThrowOnBuildFailure = false;
				Assert.IsTrue (b.Build (proj), "first build failed");
				var outputPath = Path.Combine (Root, b.ProjectDirectory, proj.OutputPath);
				var archivePath = Path.Combine (outputPath, proj.PackageName + ".apk.mSYM");
				var allFilesInArchive = Directory.GetFiles (archivePath, "*", SearchOption.AllDirectories);
				Assert.IsTrue (allFilesInArchive.Any (x => Path.GetFileName (x) == string.Format ("{0}.dll", proj.ProjectName)), "{0}.dll should exist in {1}",
					proj.ProjectName, archivePath);
				Assert.IsTrue (allFilesInArchive.Any (x => Path.GetFileName (x) == string.Format ("{0}.dll.mdb", proj.ProjectName)), "{0}.mdb should exist in {1}",
					proj.ProjectName, archivePath);
				foreach (var abi in new string [] { "armeabi-v7a", "x86" }) {
					using (var apk = ZipHelper.OpenZip (Path.Combine (outputPath, proj.PackageName + "-" + abi + ".apk"))) {
						var data = ZipHelper.ReadFileFromZip (apk, "environment");
						var env = Encoding.ASCII.GetString (data);
						var lines = env.Split (new char [] { '\n' });
						Assert.IsTrue (lines.Any (x => x.Contains ("XAMARIN_BUILD_ID")),
							"The environment should contain a XAMARIN_BUIL_ID");
						var buildID = lines.First (x => x.StartsWith ("XAMARIN_BUILD_ID", StringComparison.InvariantCultureIgnoreCase));
						buildIds.Add (abi, buildID);
					}
				}
				Assert.IsFalse (buildIds.Values.Any (x => buildIds.Values.Any (v => v != x)),
					"All the XAMARIN_BUILD_ID values should be the same");

				var msymDirectory = Path.Combine (Root, b.ProjectDirectory, proj.OutputPath, proj.PackageName + ".apk.mSYM");
				Assert.IsTrue (File.Exists (Path.Combine (msymDirectory, "manifest.xml")), "manifest.xml should exist in", msymDirectory);
				var doc = XDocument.Load (Path.Combine (msymDirectory, "manifest.xml"));

				Assert.IsTrue (doc.Element ("mono-debug")
					.Elements ()
					.Any (x => x.Name == "app-id" && x.Value == proj.PackageName), "app-id is has an incorrect value.");
				var buildId = buildIds.First ().Value;
				Assert.IsTrue (doc.Element ("mono-debug")
					.Elements ()
					.Any (x => x.Name == "build-id" && x.Value == buildId.Replace ("XAMARIN_BUILD_ID=", "")), "build-id is has an incorrect value.");
			}
		}

		[Test]
		public void CheckIncludedAssemblies ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
			};
			proj.References.Add (new BuildItem.Reference ("Mono.Data.Sqlite.dll"));
			var expectedFiles = new string [] {
				"Java.Interop.dll",
				"Mono.Android.dll",
				"mscorlib.dll",
				"System.Collections.Concurrent.dll",
				"System.Collections.dll",
				"System.Core.dll",
				"System.Diagnostics.Debug.dll",
				"System.dll",
				"System.Linq.dll",
				"System.Reflection.dll",
				"System.Reflection.Extensions.dll",
				"System.Runtime.dll",
				"System.Runtime.Extensions.dll",
				"System.Runtime.InteropServices.dll",
				"System.Runtime.Serialization.dll",
				"System.Threading.dll",
				"UnnamedProject.dll",
				"Mono.Data.Sqlite.dll",
				"Mono.Data.Sqlite.dll.config",
			};
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name))) {
				b.Verbosity = Microsoft.Build.Framework.LoggerVerbosity.Diagnostic;
				b.ThrowOnBuildFailure = false;
				Assert.IsTrue (b.Build (proj), "build failed");
				var apk = Path.Combine (Root, b.ProjectDirectory,
						proj.IntermediateOutputPath, "android", "bin", "UnnamedProject.UnnamedProject.apk");
				using (var zip = ZipHelper.OpenZip (apk)) {
					var existingFiles = zip.Where (a => a.FullName.StartsWith ("assemblies/", StringComparison.InvariantCultureIgnoreCase));
					var missingFiles = expectedFiles.Where (x => !zip.ContainsEntry ("assmelbies/" + Path.GetFileName (x)));
					Assert.IsTrue (missingFiles.Any (),
					string.Format ("The following Expected files are missing. {0}",
						string.Join (Environment.NewLine, missingFiles)));
					var additionalFiles = existingFiles.Where (x => !expectedFiles.Contains (Path.GetFileName (x.FullName)));
					Assert.IsTrue (!additionalFiles.Any (),
						string.Format ("Unexpected Files found! {0}",
						string.Join (Environment.NewLine, additionalFiles.Select (x => x.FullName))));
				}
			}
		}

		[Test]
		public void CheckClassesDexIsIncluded ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
			};
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name))) {
				b.Verbosity = Microsoft.Build.Framework.LoggerVerbosity.Diagnostic;
				b.ThrowOnBuildFailure = false;
				Assert.IsTrue (b.Build (proj), "build failed");
				var apk = Path.Combine (Root, b.ProjectDirectory,
						proj.IntermediateOutputPath, "android", "bin", "UnnamedProject.UnnamedProject.apk");
				using (var zip = ZipHelper.OpenZip (apk)) {
					Assert.IsTrue (zip.ContainsEntry ("classes.dex"), "Apk should contain classes.dex");
				}
			}
		}
	}
}
