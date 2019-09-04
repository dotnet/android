using System.IO;
using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[NonParallelizable] // <GetAdditionalResourcesFromAssemblies/> is failing in parallel builds
	public class WearTests : BaseTest
	{
		[Test]
		public void BasicProject ([Values (true, false)] bool isRelease)
		{
			var proj = new XamarinAndroidWearApplicationProject {
				IsRelease = isRelease,
			};
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
			}
		}

		[Test]
		public void BundledWearApp ()
		{
			var target = "_UpdateAndroidResgen";
			var path = Path.Combine ("temp", TestName);
			var app = new XamarinAndroidApplicationProject {
				ProjectName = "MyApp",
			};
			var wear = new XamarinAndroidWearApplicationProject ();
			app.References.Add (new BuildItem.ProjectReference ($"..\\{wear.ProjectName}\\{wear.ProjectName}.csproj", wear.ProjectName, wear.ProjectGuid) {
				MetadataValues = "IsAppExtension=True"
			});

			// Set these to be the same values
			app.SetProperty (app.DebugProperties, KnownProperties.AndroidUseSharedRuntime, "False");
			app.SetProperty (app.DebugProperties, "EmbedAssembliesIntoApk", "True");
			wear.SetProperty (wear.DebugProperties, KnownProperties.AndroidUseSharedRuntime, "False");
			wear.SetProperty (wear.DebugProperties, "EmbedAssembliesIntoApk", "True");

			using (var wearBuilder = CreateDllBuilder (Path.Combine (path, wear.ProjectName)))
			using (var appBuilder = CreateApkBuilder (Path.Combine (path, app.ProjectName))) {
				Assert.IsTrue (wearBuilder.Build (wear), "first wear build should have succeeded.");
				Assert.IsTrue (appBuilder.Build (app), "first app build should have succeeded.");
				// Build with no changes
				Assert.IsTrue (wearBuilder.Build (wear, doNotCleanupOnUpdate: true), "second wear build should have succeeded.");
				Assert.IsTrue (wearBuilder.Output.IsTargetSkipped (target), $"`{target}` in wear build should be skipped!");
				Assert.IsTrue (appBuilder.Build (app, doNotCleanupOnUpdate: true), "second app build should have succeeded.");
				Assert.IsTrue (appBuilder.LastBuildOutput.ContainsOccurances ($"Skipping target \"{target}\"", 2), $"`{target}` in app build should be skipped!");
				// Check the APK for the special Android Wear files
				var files = new [] {
					"res/raw/wearable_app.apk",
					"res/xml/wearable_app_desc.xml"
				};
				var apk = Path.Combine (Root, appBuilder.ProjectDirectory, app.OutputPath, $"{app.PackageName}.apk");
				FileAssert.Exists (apk);
				using (var zipFile = ZipHelper.OpenZip (apk)) {
					foreach (var file in files) {
						Assert.IsTrue (zipFile.ContainsEntry (file, caseSensitive: true), $"{file} should be in the apk!");
					}
				}
			}
		}
	}
}
