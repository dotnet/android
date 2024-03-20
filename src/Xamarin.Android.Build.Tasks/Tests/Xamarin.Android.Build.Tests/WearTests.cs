using System.IO;
using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
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
				appBuilder.ThrowOnBuildFailure = false;
				Assert.IsFalse (appBuilder.Build (app), "'dotnet' app build should have failed.");
				StringAssertEx.Contains ($"error XA4312", appBuilder.LastBuildOutput, "Error should be XA4312");
			}
		}

		[Test]
		public void WearProjectJavaBuildFailure ()
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
				EnableDefaultItems = true,
				PackageReferences = {
					KnownPackages.XamarinAndroidXWear,
					new Package { Id = "Xamarin.Android.Wear", Version = "2.2.0" },
					new Package { Id = "Xamarin.AndroidX.PercentLayout", Version = "1.0.0.14" },
					new Package { Id = "Xamarin.AndroidX.Legacy.Support.Core.UI", Version = "1.0.0.14" },
				},
				SupportedOSPlatformVersion = "23",
			};
			var builder = CreateApkBuilder ();
			builder.ThrowOnBuildFailure = false;
			Assert.IsFalse (builder.Build (proj), $"{proj.ProjectName} should fail.");
			Assert.IsTrue (StringAssertEx.ContainsText (builder.LastBuildOutput, "error XA1039"), "Should receive error XA1039");
		}
	}
}
