using System;
using NUnit.Framework;
using System.IO;
using System.Text;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[Category ("Node-3")]
	[Parallelizable (ParallelScope.Children)]
	public class AssetPackTests : BaseTest
	{
		[Test]
		[Category ("SmokeTests")]
		public void BuildLibraryWithAssetPack ([Values (true, false)] bool isRelease)
		{
			var path = Path.Combine ("temp", TestName);
			var lib = new XamarinAndroidLibraryProject {
				IsRelease = isRelease,
				OtherBuildItems = {
					new AndroidItem.AndroidAsset ("Assets\\asset1.txt") {
						TextContent = () => "Asset1",
						Encoding = Encoding.ASCII,
						MetadataValues="AssetPack=assetpack1",
					},
				}
			};
			using (var builder = CreateDllBuilder (Path.Combine (path, lib.ProjectName))) {
				builder.ThrowOnBuildFailure = false;
				Assert.IsFalse (builder.Build (lib), $"{lib.ProjectName} should fail.");
				StringAssertEx.Contains ("error XA0138:", builder.LastBuildOutput,
					"Build Output did not contain error XA0138'.");
			}
		}

		[Test]
		[Category ("SmokeTests")]
		public void BuildApplicationWithAssetPackOutsideProjectDirectory ([Values (true, false)] bool isRelease)
		{
			var path = Path.Combine ("temp", TestName);
			var app = new XamarinAndroidApplicationProject {
				ProjectName = "MyApp",
				IsRelease = isRelease,
				OtherBuildItems = {
					new AndroidItem.AndroidAsset ("..\\Assets\\asset1.txt") {
						TextContent = () => "Asset1",
						Encoding = Encoding.ASCII,
						MetadataValues="AssetPack=assetpack1;Link=Assets\\asset1.txt",
					},
					new AndroidItem.AndroidAsset ("..\\Assets\\asset2.txt") {
						TextContent = () => "Asset2",
						Encoding = Encoding.ASCII,
						MetadataValues="AssetPack=assetpack1;Link=Assets\\asset2.txt",
					},
					new AndroidItem.AndroidAsset ("..\\Assets\\SubDirectory\\asset3.txt") {
						TextContent = () => "Asset2",
						Encoding = Encoding.ASCII,
						MetadataValues="AssetPack=assetpack1;Link=Assets\\SubDirectory\\asset3.txt",
					},
				}
			};
			app.SetProperty ("AndroidPackageFormat", "aab");
			using (var appBuilder = CreateApkBuilder (Path.Combine (path, app.ProjectName))) {
				Assert.IsTrue (appBuilder.Build (app), $"{app.ProjectName} should succeed");
				// Check the final aab has the required feature files in it.
				var aab = Path.Combine (Root, appBuilder.ProjectDirectory,
					app.OutputPath, $"{app.PackageName}.aab");
				using (var zip = ZipHelper.OpenZip (aab)) {
					Assert.IsFalse (zip.ContainsEntry ("base/assets/asset1.txt"), "aab should not contain base/assets/asset1.txt");
					Assert.IsFalse (zip.ContainsEntry ("base/assets/asset2.txt"), "aab should not contain base/assets/asset2.txt");
					Assert.IsFalse (zip.ContainsEntry ("base/assets/SubDirectory/asset3.txt"), "aab should not contain base/assets/SubDirectory/asset3.txt");
					Assert.IsTrue (zip.ContainsEntry ("assetpack1/assets/asset1.txt"), "aab should contain assetpack1/assets/asset1.txt");
					Assert.IsTrue (zip.ContainsEntry ("assetpack1/assets/asset2.txt"), "aab should contain assetpack1/assets/asset2.txt");
					Assert.IsTrue (zip.ContainsEntry ("assetpack1/assets/SubDirectory/asset3.txt"), "aab should contain assetpack1/assets/SubDirectory/asset3.txt");
					Assert.IsTrue (zip.ContainsEntry ("assetpack1/assets.pb"), "aab should contain assetpack1/assets.pb");
					Assert.IsFalse (zip.ContainsEntry ("assetpack1/resources.pb"), "aab should not contain assetpack1/resources.pb");
				}
			}
		}

		[Test]
		[Category ("SmokeTests")]
		public void BuildApplicationWithAssetPackOverrides ([Values (true, false)] bool isRelease)
		{
			var path = Path.Combine ("temp", TestName);
			var app = new XamarinAndroidApplicationProject {
				ProjectName = "MyApp",
				IsRelease = isRelease,
				OtherBuildItems = {
					new AndroidItem.AndroidAsset ("Assets\\asset1.txt") {
						TextContent = () => "Asset1",
						Encoding = Encoding.ASCII,
						MetadataValues="AssetPack=assetpack1",
					},
					new AndroidItem.AndroidAsset ("Assets\\asset2.txt") {
						TextContent = () => "Asset2",
						Encoding = Encoding.ASCII,
						MetadataValues="AssetPack=base",
					},
				}
			};
			app.SetProperty ("AndroidPackageFormat", "aab");
			using (var appBuilder = CreateApkBuilder (Path.Combine (path, app.ProjectName))) {
				Assert.IsTrue (appBuilder.Build (app), $"{app.ProjectName} should succeed");
				// Check the final aab has the required feature files in it.
				var aab = Path.Combine (Root, appBuilder.ProjectDirectory,
					app.OutputPath, $"{app.PackageName}.aab");
				using (var zip = ZipHelper.OpenZip (aab)) {
					Assert.IsFalse (zip.ContainsEntry ("base/assets/asset1.txt"), "aab should not contain base/assets/asset1.txt");
					Assert.IsTrue (zip.ContainsEntry ("base/assets/asset2.txt"), "aab should contain base/assets/asset2.txt");
					Assert.IsTrue (zip.ContainsEntry ("assetpack1/assets/asset1.txt"), "aab should contain assetpack1/assets/asset1.txt");
					Assert.IsFalse (zip.ContainsEntry ("assetpack1/assets/asset2.txt"), "aab should not contain assetpack1/assets/asset2.txt");
					Assert.IsTrue (zip.ContainsEntry ("assetpack1/assets.pb"), "aab should contain assetpack1/assets.pb");
					Assert.IsFalse (zip.ContainsEntry ("assetpack1/resources.pb"), "aab should not contain assetpack1/resources.pb");
				}
			}
		}

		[Test]
		[Category ("SmokeTests")]
		public void BuildApplicationWithAssetPack ([Values (true, false)] bool isRelease) {
			var path = Path.Combine ("temp", TestName);
			var asset3 = new AndroidItem.AndroidAsset ("Assets\\asset3.txt") {
				TextContent = () => "Asset3",
				Encoding = Encoding.ASCII,
				MetadataValues="AssetPack=assetpack1",
			};
			var app = new XamarinAndroidApplicationProject {
				ProjectName = "MyApp",
				IsRelease = isRelease,
				OtherBuildItems = {
					new AndroidItem.AndroidAsset ("Assets\\asset1.txt") {
						TextContent = () => "Asset1",
						Encoding = Encoding.ASCII,
					},
					new AndroidItem.AndroidAsset ("Assets\\asset2.txt") {
						TextContent = () => "Asset2",
						Encoding = Encoding.ASCII,
						MetadataValues="AssetPack=assetpack1;DeliveryType=InstallTime",
					},
					asset3,
					new AndroidItem.AndroidAsset ("Assets\\asset4.txt") {
						TextContent = () => "Asset4",
						Encoding = Encoding.ASCII,
						MetadataValues="AssetPack=assetpack2;DeliveryType=OnDemand",
					},
					new AndroidItem.AndroidAsset ("Assets\\asset5.txt") {
						TextContent = () => "Asset5",
						Encoding = Encoding.ASCII,
						MetadataValues="AssetPack=assetpack3;DeliveryType=FastFollow",
					},
				}
			};
			app.SetProperty ("AndroidPackageFormat", "aab");
			using (var appBuilder = CreateApkBuilder (Path.Combine (path, app.ProjectName))) {
				Assert.IsTrue (appBuilder.Build (app), $"{app.ProjectName} should succeed");
				// Check the final aab has the required feature files in it.
				var aab = Path.Combine (Root, appBuilder.ProjectDirectory,
					app.OutputPath, $"{app.PackageName}.aab");
				var asset3File = Path.Combine (Root, path, app.ProjectName,
					app.IntermediateOutputPath, "assetpacks", "assetpack1", "assets", "asset3.txt");
				using (var zip = ZipHelper.OpenZip (aab)) {
					Assert.IsTrue (zip.ContainsEntry ("base/assets/asset1.txt"), "aab should contain base/assets/asset1.txt");
					Assert.IsFalse (zip.ContainsEntry ("base/assets/asset2.txt"), "aab should not contain base/assets/asset2.txt");
					Assert.IsFalse (zip.ContainsEntry ("base/assets/asset3.txt"), "aab should not contain base/assets/asset3.txt");
					Assert.IsFalse (zip.ContainsEntry ("base/assets/asset4.txt"), "aab should not contain base/assets/asset4.txt");
					Assert.IsTrue (zip.ContainsEntry ("assetpack1/assets/asset2.txt"), "aab should contain assetpack1/assets/asset2.txt");
					Assert.IsTrue (zip.ContainsEntry ("assetpack1/assets/asset3.txt"), "aab should contain assetpack1/assets/asset3.txt");
					Assert.IsTrue (zip.ContainsEntry ("assetpack2/assets/asset4.txt"), "aab should contain assetpack2/assets/asset4.txt");
					Assert.IsTrue (zip.ContainsEntry ("assetpack3/assets/asset5.txt"), "aab should contain assetpack3/assets/asset5.txt");
					Assert.IsTrue (zip.ContainsEntry ("assetpack1/assets.pb"), "aab should contain assetpack1/assets.pb");
					Assert.IsFalse (zip.ContainsEntry ("assetpack1/resources.pb"), "aab should not contain assetpack1/resources.pb");
				}
				Assert.IsTrue (appBuilder.Build (app, doNotCleanupOnUpdate: true, saveProject: false), $"{app.ProjectName} should succeed");
				appBuilder.Output.AssertTargetIsSkipped ("_CreateAssetPackManifests");
				appBuilder.Output.AssertTargetIsSkipped ("_BuildAssetPacks");
				appBuilder.Output.AssertTargetIsSkipped ("_GenerateAndroidAssetsDir");
				FileAssert.Exists (asset3File, $"file {asset3File} should exist.");
				asset3.TextContent = () => "Asset3 Updated";
				asset3.Timestamp = DateTime.UtcNow.AddSeconds(1);
				Assert.IsTrue (appBuilder.Build (app, doNotCleanupOnUpdate: true, saveProject: false), $"{app.ProjectName} should succeed");
				appBuilder.Output.AssertTargetIsNotSkipped ("_CreateAssetPackManifests");
				appBuilder.Output.AssertTargetIsNotSkipped ("_BuildAssetPacks");
				appBuilder.Output.AssertTargetIsNotSkipped ("_GenerateAndroidAssetsDir");
				FileAssert.Exists (asset3File, $"file {asset3File} should exist.");
				Assert.AreEqual (asset3.TextContent (), File.ReadAllText (asset3File), $"Contents of {asset3File} should have been updated.");
				app.OtherBuildItems.Remove (asset3);
				Assert.IsTrue (appBuilder.Build (app, doNotCleanupOnUpdate: true), $"{app.ProjectName} should succeed");
				FileAssert.DoesNotExist (asset3File, $"file {asset3File} should not exist.");
				using (var zip = ZipHelper.OpenZip (aab)) {
					Assert.IsTrue (zip.ContainsEntry ("base/assets/asset1.txt"), "aab should contain base/assets/asset1.txt");
					Assert.IsFalse (zip.ContainsEntry ("base/assets/asset2.txt"), "aab should not contain base/assets/asset2.txt");
					Assert.IsFalse (zip.ContainsEntry ("base/assets/asset3.txt"), "aab should not contain base/assets/asset3.txt");
					Assert.IsFalse (zip.ContainsEntry ("base/assets/asset4.txt"), "aab should not contain base/assets/asset4.txt");
					Assert.IsTrue (zip.ContainsEntry ("assetpack1/assets/asset2.txt"), "aab should contain assetpack1/assets/asset2.txt");
					Assert.IsFalse (zip.ContainsEntry ("assetpack1/assets/asset3.txt"), "aab should not contain assetpack1/assets/asset3.txt");
					Assert.IsTrue (zip.ContainsEntry ("assetpack2/assets/asset4.txt"), "aab should contain assetpack2/assets/asset4.txt");
					Assert.IsTrue (zip.ContainsEntry ("assetpack3/assets/asset5.txt"), "aab should contain assetpack3/assets/asset5.txt");
					Assert.IsTrue (zip.ContainsEntry ("assetpack1/assets.pb"), "aab should contain assetpack1/assets.pb");
					Assert.IsFalse (zip.ContainsEntry ("assetpack1/resources.pb"), "aab should not contain assetpack1/resources.pb");
				}
				appBuilder.Output.AssertTargetIsNotSkipped ("_CreateAssetPackManifests");
				appBuilder.Output.AssertTargetIsNotSkipped ("_BuildAssetPacks");
				appBuilder.Output.AssertTargetIsNotSkipped ("_GenerateAndroidAssetsDir");
			}
		}
	}
}
