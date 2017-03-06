using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Framework;
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

		[Test]
		public void BuildBasicApplicationCheckPdb ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			using (var b = CreateApkBuilder ("temp/BuildBasicApplicationCheckPdb", false, false)) {
				b.Verbosity = LoggerVerbosity.Diagnostic;
				var reference = new BuildItem.Reference ("PdbTestLibrary.dll") {
					WebContent = "https://dl.dropboxusercontent.com/u/18881050/Xamarin/PdbTestLibrary.dll"
				};
				proj.References.Add (reference);
				var pdb = new BuildItem.NoActionResource ("PdbTestLibrary.pdb") {
					WebContent = "https://dl.dropboxusercontent.com/u/18881050/Xamarin/PdbTestLibrary.pdb"
				};
				proj.References.Add (pdb);
				var netStandardRef = new BuildItem.Reference ("NetStandard16.dll") {
					WebContent = "https://dl.dropboxusercontent.com/u/18881050/Xamarin/NetStandard16.dll"
				};
				proj.References.Add (netStandardRef);
				var netStandardpdb = new BuildItem.NoActionResource ("NetStandard16.pdb") {
					WebContent = "https://dl.dropboxusercontent.com/u/18881050/Xamarin/NetStandard16.pdb"
				};
				proj.References.Add (netStandardpdb);
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				var pdbToMdbPath = Path.Combine (Root, b.ProjectDirectory, "PdbTestLibrary.dll.mdb");
				Assert.IsTrue (
					File.Exists (pdbToMdbPath),
					"PdbTestLibrary.dll.mdb must be generated next to the .pdb");
				Assert.IsTrue (
					File.Exists (Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android", "assets", "UnnamedProject.dll.mdb")),
					"UnnamedProject.dll.mdb must be copied to the Intermediate directory");
				Assert.IsFalse (
					File.Exists (Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android", "assets", "PdbTestLibrary.pdb")),
					"PdbTestLibrary.pdb must not be copied to Intermediate directory");
				Assert.IsTrue (
					File.Exists (Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android", "assets", "PdbTestLibrary.dll.mdb")),
					"PdbTestLibrary.dll.mdb must be copied to Intermediate directory");
				FileAssert.AreNotEqual (pdbToMdbPath,
					Path.Combine (Root, b.ProjectDirectory, "PdbTestLibrary.pdb"),
					"The .pdb should NOT match the .mdb");
				Assert.IsTrue (
					File.Exists (Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android", "assets", "NetStandard16.pdb")),
					"NetStandard16.pdb must be copied to Intermediate directory");
				var apk = Path.Combine (Root, b.ProjectDirectory,
					proj.IntermediateOutputPath, "android", "bin", "UnnamedProject.UnnamedProject.apk");
				using (var zipFile = ZipHelper.OpenZip (apk)) {
					Assert.IsNotNull (ZipHelper.ReadFileFromZip (zipFile,
							"assemblies/NetStandard16.pdb"),
							"assemblies/NetStandard16.pdb should exist in the apk.");
					Assert.IsNotNull (ZipHelper.ReadFileFromZip (zipFile,
							"assemblies/PdbTestLibrary.dll.mdb"),
							"assemblies/PdbTestLibrary.dll.mdb should exist in the apk.");
					Assert.IsNull (ZipHelper.ReadFileFromZip (zipFile,
							"assemblies/PdbTestLibrary.pdb"),
							"assemblies/PdbTestLibrary.pdb should not exist in the apk.");
				}
				Assert.IsTrue (b.Build (proj, doNotCleanupOnUpdate: true), "second build failed");
				Assert.IsTrue (
					b.LastBuildOutput.Contains ("Skipping target \"_CopyMdbFiles\" because"),
					"the _CopyMdbFiles target should be skipped");
				var lastTime = File.GetLastAccessTimeUtc (pdbToMdbPath);
				pdb.Timestamp = DateTime.UtcNow;
				Assert.IsTrue (b.Build (proj, doNotCleanupOnUpdate: true), "third build failed");
				Assert.IsFalse (
					b.LastBuildOutput.Contains ("Skipping target \"_CopyMdbFiles\" because"),
					"the _CopyMdbFiles target should not be skipped");
				Assert.Less (lastTime,
					File.GetLastAccessTimeUtc (pdbToMdbPath),
					"{0} should have been updated", pdbToMdbPath);
			}
		}

		[Test]
		public void BuildInDesignTimeMode ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
			};
			using (var builder = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name), false ,false)) {
				builder.Verbosity = LoggerVerbosity.Diagnostic;
				builder.Target = "UpdateAndroidResources";
				builder.Build (proj, parameters: new string[] { "DesignTimeBuild=true" });
				Assert.IsFalse (builder.Output.IsTargetSkipped ("_CreatePropertiesCache"), "target \"_CreatePropertiesCache\" should have been run.");
				Assert.IsFalse (builder.Output.IsTargetSkipped ("_ResolveLibraryProjectImports"), "target \"_ResolveLibraryProjectImports\' should have been run.");
				builder.Build (proj, parameters: new string[] { "DesignTimeBuild=true" });
				Assert.IsFalse (builder.Output.IsTargetSkipped ("_CreatePropertiesCache"), "target \"_CreatePropertiesCache\" should have been run.");
				Assert.IsTrue (builder.Output.IsTargetSkipped ("_ResolveLibraryProjectImports"), "target \"_ResolveLibraryProjectImports\' should have been skipped.");
			}
		}
	}
}

