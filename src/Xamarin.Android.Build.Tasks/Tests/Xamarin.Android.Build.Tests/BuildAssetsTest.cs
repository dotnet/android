using System;
using NUnit.Framework;
using Xamarin.ProjectTools;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using System.Text;
using System.Xml.Linq;
using System.Collections.Generic;

namespace Xamarin.Android.Build.Tests
{
	[Parallelizable (ParallelScope.Children)]
	public class BuildAssetsTest : BaseTest
	{
		[Test]
		public void CheckPostCompileAssetsIncludedInAPK ()
		{
			var path = Path.Combine ("temp", TestName);
			var proj = new XamarinAndroidApplicationProject () {
				ProjectName = "App1",
				IsRelease = true,
				OtherBuildItems = {
					new AndroidItem.AndroidAsset ("Assets\\asset3.txt") {
						TextContent = () => "PlaceHolder",
						Encoding = Encoding.ASCII,
					},
					new AndroidItem.AndroidAsset ("Assets\\subfolder\\asset4.txt") {
						TextContent = () => "Asset4",
						Encoding = Encoding.ASCII,
					},
				},
				Imports = {
					new Import (() => "My.Test.target") {
						TextContent = () => @"<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
	<Target Name=""CustomTarget"" AfterTargets=""Compile"" >
		<WriteLinesToFile
			File=""Assets\\asset3.txt""
			Lines=""Asset3""
			Overwrite=""true""/>
	</Target>
</Project>"
					},
				},

			};
			using (var b = CreateApkBuilder (Path.Combine (path, proj.ProjectName))) {
				Assert.IsTrue (b.Build (proj), "{0} should have built successfully.", proj.ProjectName);
				using (var apk = ZipHelper.OpenZip (Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android", "bin", "packaged_resources"))) {
					var item = "assets/asset3.txt";
					var data = ZipHelper.ReadFileFromZip (apk, item);
					Assert.IsNotNull (data, "{0} should be in the apk.", item);
					var text = Encoding.ASCII.GetString (data).Trim ();
					Assert.AreEqual ("Asset3", text, $"The Contents of {item} should be \"Asset3\" but was {text}");
				}
				Directory.Delete (Path.Combine (Root, path), recursive: true);
			}

		}

		[Test]
		public void CheckAssetsAreIncludedInAPK ()
		{
			var projectPath = Path.Combine ("temp", TestName);
			var libproj = new XamarinAndroidLibraryProject () {
				ProjectName = "Library1",
				IsRelease = true,
				OtherBuildItems = {
					new AndroidItem.AndroidAsset ("Assets\\asset1.txt") {
						TextContent = () => "Asset1",
						Encoding = Encoding.ASCII,
					},
					new AndroidItem.AndroidAsset ("Assets\\subfolder\\") {

					},
					new AndroidItem.AndroidAsset ("Assets\\subfolder\\asset2.txt") {
						TextContent = () => "Asset2",
						Encoding = Encoding.ASCII,
					},
					new AndroidItem.AndroidAsset ("Assets\\subfolder\\asset5.txt") {
						TextContent = () => "Asset5",
						Encoding = Encoding.ASCII,
						Metadata = { { "LogicalName", Path.Combine (Path.GetPathRoot (Root), "Assets", "subfolder", "asset5.txt") } },
					},
				}
			};
			var proj = new XamarinAndroidApplicationProject () {
				ProjectName = "App1",
				IsRelease = true,
				OtherBuildItems = {
					new AndroidItem.AndroidAsset ("Assets\\asset3.txt") {
						TextContent = () => "Asset3",
						Encoding = Encoding.ASCII,
					},
					new AndroidItem.AndroidAsset ("Assets\\subfolder\\") {

					},
					new AndroidItem.AndroidAsset ("Assets\\subfolder\\asset4.txt") {
						TextContent = () => "Asset4",
						Encoding = Encoding.ASCII,
					},
					new AndroidItem.AndroidAsset ("Assets\\subfolder\\asset6.txt") {
						TextContent = () => "Asset6",
						Encoding = Encoding.ASCII,
						Metadata = { { "LogicalName", Path.Combine (Path.GetPathRoot (Root), "Assets", "subfolder", "asset6.txt") } },
					},
				}
			};
			proj.References.Add (new BuildItem ("ProjectReference", "..\\Library1\\Library1.csproj"));
			using (var libb = CreateDllBuilder (Path.Combine (projectPath, libproj.ProjectName))) {
				Assert.IsTrue (libb.Build (libproj), "{0} should have built successfully.", libproj.ProjectName);
				using (var b = CreateApkBuilder (Path.Combine (projectPath, proj.ProjectName))) {
					Assert.IsTrue (b.Build (proj), "{0} should have built successfully.", proj.ProjectName);
					using (var apk = ZipHelper.OpenZip (Path.Combine (Root, b.ProjectDirectory, proj.OutputPath, $"{proj.PackageName}-Signed.apk"))) {
						foreach (var a in libproj.OtherBuildItems.Where (x => x is AndroidItem.AndroidAsset)) {
							var item = a.Include ().ToLower ().Replace ("\\", "/");
							if (item.EndsWith ("/", StringComparison.Ordinal))
								continue;
							var data = ZipHelper.ReadFileFromZip (apk, item);
							Assert.IsNotNull (data, "{0} should be in the apk.", item);
							Assert.AreEqual (a.TextContent (), Encoding.ASCII.GetString (data), "The Contents of {0} should be \"{1}\"", item, a.TextContent ());
						}
						foreach (var a in proj.OtherBuildItems.Where (x => x is AndroidItem.AndroidAsset)) {
							var item = a.Include ().ToLower ().Replace ("\\", "/");
							if (item.EndsWith ("/", StringComparison.Ordinal))
								continue;
							var data = ZipHelper.ReadFileFromZip (apk, item);
							Assert.IsNotNull (data, "{0} should be in the apk.", item);
							Assert.AreEqual (a.TextContent (), Encoding.ASCII.GetString (data), "The Contents of {0} should be \"{1}\"", item, a.TextContent ());
						}
					}
					Directory.Delete (Path.Combine (Root, projectPath), recursive: true);
				}
			}
		}

		[Test]
		public void InvalidAssetDirectoryWithNonASCIIChars ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				ProjectName = "App1",
				IsRelease = true,
				OtherBuildItems = {
					new AndroidItem.AndroidAsset ("Assets\\asset1.txt") {
						TextContent = () => "Asset1",
						Encoding = Encoding.ASCII,
					},
				},
			};
			using (var b = CreateApkBuilder (Path.Combine ("temp", "InvalidAssetDirectoryWithNonASCIIChars_Ümläüt", proj.ProjectName))) {
				b.ThrowOnBuildFailure = false;
				Assert.AreEqual (!IsWindows, b.Build (proj), $"{proj.ProjectName} should {(IsWindows ? "not " : "")}have built successfully.");
				Assert.AreEqual (IsWindows, b.LastBuildOutput.ContainsText ("APT2265"), $"APT2265 should {(IsWindows ? "" : "not ")}have been raised.");
			}
		}

		[Test]
		public void FullPath ()
		{
			var assetPath = Path.GetFullPath (Path.Combine (Root, "temp", TestName, "Assets", "foo.txt"));
			Directory.CreateDirectory (Path.GetDirectoryName (assetPath));
			File.WriteAllText (assetPath, contents: "bar");
			var proj = new XamarinAndroidLibraryProject {
				OtherBuildItems = {
					new AndroidItem.AndroidAsset (assetPath) {
						Metadata = { { "LogicalName", Path.Combine ("Assets", "foo.txt") } },
					},
				},
			};
			using (var b = CreateDllBuilder (Path.Combine ("temp", TestName, "SubDir"))) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				var libraryProjectImports = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "__AndroidLibraryProjects__.zip");
				var aarPath = Path.Combine (Root, b.ProjectDirectory, proj.OutputPath, $"{proj.ProjectName}.aar");
				FileAssert.Exists (aarPath);
				using (var aar = ZipHelper.OpenZip (aarPath)) {
					aar.AssertEntryContents (aarPath, "assets/foo.txt", contents: "bar");
				}
				FileAssert.DoesNotExist (libraryProjectImports);
			}
		}

		[Test]
		[Category ("SmokeTests")]
		[TestCase("MonoAndroidAssetsPrefix")]
		[TestCase("MonoAndroidResourcePrefix")]
		public void FullPrefixRaisesError(string prefix)
		{
			var proj = new XamarinAndroidLibraryProject {
				OtherBuildItems = {
					new AndroidItem.AndroidAsset ("Assets\\asset1.txt") {
						TextContent = () => "bar",
					},
				},
			};
			var app = new XamarinAndroidApplicationProject {
				References = {
					new BuildItem ("ProjectReference", "..\\Library1\\Library1.csproj"),
				},
				OtherBuildItems = {
					new AndroidItem.AndroidAsset ("Assets\\asset2.txt") {
						TextContent = () => "foo",
					},
				},
			};
			using var b = CreateDllBuilder (Path.Combine ("temp", TestName, "Library"));
			using var appb = CreateApkBuilder (Path.Combine ("temp", TestName, "App"));
			appb.ThrowOnBuildFailure = false;
			b.ThrowOnBuildFailure = false;
			Directory.CreateDirectory (Path.Combine (Root, "temp", TestName, "App"));
			var envar = new Dictionary<string, string> {
				{ prefix, Path.Combine (Root, "temp", TestName, "App") },
			};
			Assert.IsFalse (b.Build (proj, environmentVariables: envar), "Build should have failed.");
			Assert.IsTrue (b.LastBuildOutput.ContainsText ("error XA1041"), "XA1041 should have been raised.");
			Assert.IsFalse (appb.Build (app, environmentVariables: envar), "Build should have failed.");
			Assert.IsTrue (appb.LastBuildOutput.ContainsText ("error XA1041"), "XA1041 should have been raised.");
		}
	}
}
