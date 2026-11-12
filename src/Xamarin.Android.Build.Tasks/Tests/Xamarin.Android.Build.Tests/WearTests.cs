using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestClass]
	[DoNotParallelize]
	public class WearTests : BaseTest
	{
		[TestMethod]
		[CombinatorialData]
		public void BasicProject ([ValidRuntimeConfigurations] (bool isRelease, AndroidRuntime runtime) configuration)
		{
			var (isRelease, runtime) = configuration;
			var proj = new XamarinAndroidWearApplicationProject {
				IsRelease = isRelease,
			};
			proj.SetRuntime (runtime);
			using (var b = CreateApkBuilder ()) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
			}
		}

		[TestMethod]
		[CombinatorialData]
		public void BundledWearApp ([CombinatorialValues (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
		{
			bool isRelease = runtime == AndroidRuntime.NativeAOT;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			var path = Path.Combine ("temp", TestName);
			var app = new XamarinAndroidApplicationProject {
				IsRelease = isRelease,
				ProjectName = "MyApp",
				EmbedAssembliesIntoApk = true,
			};
			app.SetRuntime (runtime);

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

		[TestMethod]
		[CombinatorialData]
		public void WearProjectJavaBuildFailure ([CombinatorialValues (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
		{
			const bool isRelease = true;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = isRelease,
				EnableDefaultItems = true,
				PackageReferences = {
					KnownPackages.XamarinAndroidXWear,
					new Package { Id = "Xamarin.Android.Wear", Version = "2.2.0" },
					new Package { Id = "Xamarin.AndroidX.PercentLayout", Version = "1.0.0.14" },
					new Package { Id = "Xamarin.AndroidX.Legacy.Support.Core.UI", Version = "1.0.0.14" },
				},
				SupportedOSPlatformVersion = "24",
			};
			proj.SetRuntime (runtime);
			var builder = CreateApkBuilder ();
			builder.ThrowOnBuildFailure = false;
			Assert.IsFalse (builder.Build (proj), $"{proj.ProjectName} should fail.");
			Assert.IsTrue (StringAssertEx.ContainsText (builder.LastBuildOutput, "error XA1039"), "Should receive error XA1039");
		}
	}
}
