using System.IO;
using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class WearTests : BaseTest
	{
		[Test]
		public void ResolveLibraryImportsWithReadonlyFiles ()
		{
			//NOTE: doesn't need to be a full Android Wear app
			var proj = new XamarinAndroidApplicationProject {
				PackageReferences = {
					KnownPackages.AndroidWear_2_2_0,
					KnownPackages.Android_Arch_Core_Common_26_1_0,
					KnownPackages.Android_Arch_Lifecycle_Common_26_1_0,
					KnownPackages.Android_Arch_Lifecycle_Runtime_26_1_0,
					KnownPackages.SupportCompat_27_0_2_1,
					KnownPackages.SupportCoreUI_27_0_2_1,
					KnownPackages.SupportPercent_27_0_2_1,
					KnownPackages.SupportV7RecyclerView_27_0_2_1,
				},
			};
			using (var b = CreateApkBuilder ()) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
			}
		}

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
				EmbedAssembliesIntoApk = true,
			};
			var wear = new XamarinAndroidWearApplicationProject {
				EmbedAssembliesIntoApk = true,
			};
			app.References.Add (new BuildItem.ProjectReference ($"..\\{wear.ProjectName}\\{wear.ProjectName}.csproj", wear.ProjectName, wear.ProjectGuid) {
				MetadataValues = "IsAppExtension=True"
			});

			using (var wearBuilder = CreateDllBuilder (Path.Combine (path, wear.ProjectName)))
			using (var appBuilder = CreateApkBuilder (Path.Combine (path, app.ProjectName))) {
				Assert.IsTrue (wearBuilder.Build (wear), "first wear build should have succeeded.");
				// In .NET 5+, just check for a build error
				if (Builder.UseDotNet) {
					appBuilder.ThrowOnBuildFailure = false;
					Assert.IsFalse (appBuilder.Build (app), "'dotnet' app build should have failed.");
					StringAssertEx.Contains ($"error XA4312", appBuilder.LastBuildOutput, "Error should be XA4312");
					return;
				}
				Assert.IsTrue (appBuilder.Build (app), "first app build should have succeeded.");
				StringAssertEx.Contains ($"warning XA4312", appBuilder.LastBuildOutput, "Warning should be XA4312");
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

		[Test]
		public void WearProjectJavaBuildFailure ()
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
				EnableDefaultItems = true,
				PackageReferences = {
					new Package { Id = "Xamarin.AndroidX.Wear", Version = "1.2.0.5" },
					new Package { Id = "Xamarin.Android.Wear", Version = "2.2.0" },
					new Package { Id = "Xamarin.AndroidX.PercentLayout", Version = "1.0.0.14" },
					new Package { Id = "Xamarin.AndroidX.Legacy.Support.Core.UI", Version = "1.0.0.14" },
				},
				SupportedOSPlatformVersion = "23",
			};
			var builder = CreateApkBuilder ();
			builder.ThrowOnBuildFailure = false;
			Assert.IsFalse (builder.Build (proj), $"{proj.ProjectName} should fail.");
			var text = $"java.lang.RuntimeException";
			Assert.IsTrue (StringAssertEx.ContainsText (builder.LastBuildOutput, text), $"Output did not contain '{text}'");
			text = $"is defined multiple times";
			Assert.IsTrue (StringAssertEx.ContainsText (builder.LastBuildOutput, text), $"Output did not contain '{text}'");
			text = $"is from 'androidx.core.core.aar'";
			Assert.IsTrue (StringAssertEx.ContainsText (builder.LastBuildOutput, text), $"Output did not contain '{text}'");
		}
	}
}
