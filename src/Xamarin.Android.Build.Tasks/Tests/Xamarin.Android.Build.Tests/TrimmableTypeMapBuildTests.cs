using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests {
	[TestFixture]
	[Category ("Node-2")]
	public class TrimmableTypeMapBuildTests : BaseTest {

		[Test]
		public void Build_WithTrimmableTypeMap_Succeeds ([Values] bool isRelease, [Values (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
		{
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = isRelease,
			};
			proj.SetRuntime (runtime);
			proj.SetProperty ("_AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");

			var intermediateDir = builder.Output.GetIntermediaryPath ("typemap");
			DirectoryAssert.Exists (intermediateDir);
		}

		[Test]
		public void Build_WithTrimmableTypeMap_IncrementalBuild ([Values] bool isRelease, [Values (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
		{
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = isRelease,
			};
			proj.SetRuntime (runtime);
			proj.SetProperty ("_AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "First build should have succeeded.");

			var intermediateDir = builder.Output.GetIntermediaryPath ("typemap");
			DirectoryAssert.Exists (intermediateDir);

			Assert.IsTrue (builder.Build (proj), "Second build should have succeeded.");

			Assert.IsTrue (
				builder.Output.IsTargetSkipped ("_GenerateJavaStubs"),
				"_GenerateJavaStubs should be skipped on incremental build.");
		}

		[Test]
		public void Build_WithTrimmableTypeMap_DoesNotHitCopyIfChangedMismatch ([Values (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (runtime);
			proj.SetProperty ("_AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");

			Assert.IsFalse (
				StringAssertEx.ContainsText (builder.LastBuildOutput, "source and destination count mismatch"),
				$"{builder.BuildLogFile} should not fail with XACIC7004.");
			Assert.IsFalse (
				StringAssertEx.ContainsText (builder.LastBuildOutput, "Internal error: architecture"),
				$"{builder.BuildLogFile} should keep trimmable typemap assemblies aligned across ABIs.");
		}

		[Test]
		public void Build_WithTrimmableTypeMap_AssemblyStoreMappingsStayInRange ()
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetProperty ("_AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");

			var environmentFiles = Directory.GetFiles (builder.Output.GetIntermediaryPath ("android"), "environment.*.ll");
			Assert.IsNotEmpty (environmentFiles, "Expected generated environment.<abi>.ll files.");

			foreach (var environmentFile in environmentFiles) {
				var abi = Path.GetFileNameWithoutExtension (environmentFile).Substring ("environment.".Length);
				var manifestFile = builder.Output.GetIntermediaryPath (Path.Combine ("app_shared_libraries", abi, "assembly-store.so.manifest"));

				if (!File.Exists (manifestFile)) {
					continue;
				}

				var environmentText = File.ReadAllText (environmentFile);
				var runtimeDataMatch = Regex.Match (environmentText, @"assembly_store_bundled_assemblies.*\[(\d+)\s+x");
				Assert.IsTrue (runtimeDataMatch.Success, $"{environmentFile} should declare assembly_store_bundled_assemblies.");

				var runtimeDataCount = int.Parse (runtimeDataMatch.Groups [1].Value);
				var maxMappingIndex = File.ReadLines (manifestFile)
					.Select (line => Regex.Match (line, @"\bmi:(\d+)\b"))
					.Where (match => match.Success)
					.Select (match => int.Parse (match.Groups [1].Value))
					.Max ();

				Assert.That (
					runtimeDataCount,
					Is.GreaterThan (maxMappingIndex),
					$"{Path.GetFileName (environmentFile)} should allocate enough runtime slots for {Path.GetFileName (manifestFile)}.");
			}
		}

		[Test]
		public void TrimmableTypeMap_PreserveList_IsPackagedInSdk ()
		{
			var path = Path.Combine (TestEnvironment.DotNetPreviewAndroidSdkDirectory, "PreserveLists", "Trimmable.CoreCLR.xml");

			FileAssert.Exists (path, $"{path} should exist in the SDK pack.");
		}
	}
}
