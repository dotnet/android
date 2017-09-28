using System;
using NUnit.Framework;
using Xamarin.ProjectTools;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using System.Text;
using System.Xml.Linq;

namespace Xamarin.Android.Build.Tests
{
	[Parallelizable (ParallelScope.Children)]
	public class BuildAssetsTest : BaseTest
	{
		[Test]
		public void CheckAssetsAreIncludedInAPK ()
		{
			var projectPath = string.Format ("temp/CheckAssetsAreIncludedInAPK");
			var libproj = new XamarinAndroidLibraryProject () {
				ProjectName = "Library1",
				IsRelease = true,
				OtherBuildItems = {
					new AndroidItem.AndroidAsset ("Assets\\asset1.txt") {
						TextContent = () => "Asset1",
						Encoding = Encoding.ASCII,
					},
					new AndroidItem.AndroidAsset ("Assets\\subfolder\\asset2.txt") {
						TextContent = () => "Asset2",
						Encoding = Encoding.ASCII,
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
					new AndroidItem.AndroidAsset ("Assets\\subfolder\\asset4.txt") {
						TextContent = () => "Asset4",
						Encoding = Encoding.ASCII,
					},
				}
			};
			proj.References.Add (new BuildItem ("ProjectReference", "..\\Library1\\Library1.csproj"));
			using (var libb = CreateDllBuilder (Path.Combine (projectPath, libproj.ProjectName))) {
				Assert.IsTrue (libb.Build (libproj), "{0} should have built successfully.", libproj.ProjectName);
				using (var b = CreateApkBuilder (Path.Combine (projectPath, proj.ProjectName))) {
					Assert.IsTrue (b.Build (proj), "{0} should have built successfully.", proj.ProjectName);
					using (var apk = ZipHelper.OpenZip (Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android", "bin", "packaged_resources"))) {
						foreach (var a in libproj.OtherBuildItems.Where (x => x is AndroidItem.AndroidAsset)) {
							var item = a.Include ().ToLower ().Replace ("\\", "/");
							var data = ZipHelper.ReadFileFromZip (apk, item);
							Assert.IsNotNull (data, "{0} should be in the apk.", item);
							Assert.AreEqual (a.TextContent (), Encoding.ASCII.GetString (data), "The Contents of {0} should be \"{1}\"", item, a.TextContent ());
						}
						foreach (var a in proj.OtherBuildItems.Where (x => x is AndroidItem.AndroidAsset)) {
							var item = a.Include ().ToLower ().Replace ("\\", "/");
							var data = ZipHelper.ReadFileFromZip (apk, item);
							Assert.IsNotNull (data, "{0} should be in the apk.", item);
							Assert.AreEqual (a.TextContent (), Encoding.ASCII.GetString (data), "The Contents of {0} should be \"{1}\"", item, a.TextContent ());
						}
					}
					Directory.Delete (Path.Combine (Root, projectPath), recursive: true);
				}
			}
		}
	}
}
