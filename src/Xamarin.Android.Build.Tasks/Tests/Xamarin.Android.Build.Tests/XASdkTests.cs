using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;
using Xamarin.Android.Tools;
using Xamarin.ProjectTools;
using System.IO.Compression;
using Microsoft.Android.Build.Tasks;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	/// <summary>
	/// Fixture containing tests which use custom dotnet commands (e.g. `dotnet new`, `dotnet pack`),
	///  and in most cases don't use the common ProjectBuilder infrastructure.
	/// </summary>
	[TestFixture]
	[NonParallelizable] // On MacOS, parallel /restore causes issues
	public class XASdkTests : BaseTest
	{
		[Test]
		[TestCase (false)]
		[TestCase (true)]
		public void NestedBuildReusesResolvedAndroidTooling (bool multithreaded)
		{
			var projectName = $"ResolvedTooling{multithreaded}";
			var testPath = Path.Combine (Root, "temp", TestName, "O'Brien", projectName);
			TestOutputDirectories [TestContext.CurrentContext.Test.ID] = testPath;
			if (Directory.Exists (testPath))
				Directory.Delete (testPath, recursive: true);
			var dotnet = new DotNetCLI (Path.Combine (testPath, $"{projectName}.csproj")) {
				Verbosity = "diagnostic",
			};
			Assert.IsTrue (dotnet.New ("android", noRestore: true), "Creating the Android project should succeed.");
			var parameters = new [] {
				"EmbedAssembliesIntoApk=true",
				$"LintToolPath=\"{Path.Combine (testPath, "O'Brien", "lint")}\"",
			};
			var arguments = multithreaded ? new [] { "-m:2", "-mt" } : new [] { "-m:1" };
			dotnet.EnvironmentVariables ["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
			dotnet.EnvironmentVariables ["MSBUILDDISABLENODEREUSE"] = "1";
			Assert.IsTrue (dotnet.Build (parameters: parameters, msbuildArguments: arguments), "The multi-RID Android build should succeed.");
			AssertResolvedTaskCounts ();
			Assert.IsTrue (dotnet.Build (parameters: parameters, msbuildArguments: arguments), "The incremental multi-RID Android build should succeed.");
			AssertResolvedTaskCounts ();

			Assert.IsTrue (dotnet.Build (target: "_ResolveMonoAndroidSdks", parameters: parameters, msbuildArguments: arguments),
				"Standalone tooling resolution should still succeed.");
			var standalone = GetTaskCounts ();
			Assert.AreEqual (1, standalone ["ResolveSdks"]);
			Assert.AreEqual (1, standalone ["ResolveAndroidTooling"]);
			Assert.AreEqual (1, standalone ["ValidateJavaVersion"]);
			Assert.IsFalse (dotnet.Build (parameters: parameters.Concat (["LatestSupportedJavaVersion=1.0"]).ToArray (), msbuildArguments: arguments),
				"A changed Java limit must still be validated.");
			StringAssertEx.Contains ("error XA0030", dotnet.LastBuildOutput);

			void AssertResolvedTaskCounts ()
			{
				var counts = GetTaskCounts ();
				Assert.AreEqual (1, counts ["ResolveSdks"], "Only the outer build should discover SDKs.");
				Assert.AreEqual (1, counts ["ResolveAndroidTooling"], "Only the outer build should resolve Android tooling.");
				Assert.AreEqual (1, counts ["ValidateJavaVersion"], "Only the outer build should validate Java.");
				Assert.GreaterOrEqual (counts ["RuntimeIdentifierToAbi"], 3, "Both RID-specific target bodies must still execute.");
			}

			Dictionary<string, int> GetTaskCounts ()
			{
				var counts = new Dictionary<string, int> {
					["ResolveSdks"] = 0,
					["ResolveAndroidTooling"] = 0,
					["ValidateJavaVersion"] = 0,
					["RuntimeIdentifierToAbi"] = 0,
				};
				foreach (var task in counts.Keys.ToArray ()) {
					counts [task] = dotnet.LastBuildOutput.Count (line => line.Contains ($"Task \"{task}\" (TaskId:", StringComparison.Ordinal));
				}
				return counts;
			}
		}

		[Test]
		[TestCase (AndroidRuntime.CoreCLR)]
		[TestCase (AndroidRuntime.NativeAOT)]
		public void PublishReusesResolvedToolingInSpecializedInnerBuilds (AndroidRuntime runtime)
		{
			if (IgnoreUnsupportedConfiguration (runtime, release: true))
				return;

			var project = new XamarinAndroidApplicationProject { IsRelease = true };
			project.SetRuntime (runtime);
			project.SetProperty ("RuntimeIdentifier", "android-arm64");
			project.SetProperty ("AndroidLinkTool", "r8");
			project.SetProperty ("AndroidR8ObfuscationMode", "runtime-remapping");
			project.SetProperty ("AndroidEnableAssemblyCompression", "true");
			project.SetProperty ("LintToolPath", Path.Combine (Root, "temp", TestName, "O'Brien;tag=1"));
			var imports = project.Imports.Single (import => import.Project () == "Directory.Build.targets");
			imports.TextContent = () => """
				<Project>
				  <Target Name="_TestInnerToolingPaths" AfterTargets="_ResolveMonoAndroidSdks"
				      Condition=" '$(_OuterIntermediateOutputPath)' != '' ">
				    <Error Condition=" '$(_AndroidUseResolvedSdk)' != 'true' or '$(_AndroidUseResolvedTooling)' != 'true' "
				        Text="Escaped tooling paths must not disable reuse." />
				    <Error Condition=" '$(_AndroidOuterLintToolPath)' != '$(LintToolPath)' or '$(_AndroidOuterToolingInputLintToolPath)' != '$(LintToolPath)' "
				        Text="The lint path changed during forwarding." />
				    <Error Condition=" '$(tag)' != '' " Text="The path was split into a separate property." />
				  </Target>
				</Project>
				""";
			using var builder = CreateApkBuilder ();
			builder.Target = "Publish";
			builder.Verbosity = Microsoft.Build.Framework.LoggerVerbosity.Diagnostic;
			Assert.IsTrue (builder.Build (project));
			Assert.AreEqual (1, CountTask (builder.LastBuildOutput, "ResolveSdks"));
			Assert.AreEqual (1, CountTask (builder.LastBuildOutput, "ResolveAndroidTooling"));
			Assert.AreEqual (1, CountTask (builder.LastBuildOutput, "ValidateJavaVersion"));
			Assert.GreaterOrEqual (CountTask (builder.LastBuildOutput, "RuntimeIdentifierToAbi"), 2);
			builder.Output.AssertTargetIsNotSkipped ("_ResolveImplementationAssembliesForTypeMap");
			builder.Output.AssertTargetIsNotSkipped (runtime == AndroidRuntime.CoreCLR ?
				"_AndroidResolveAssemblyCompressionInputsForRuntimeIdentifier" : "_AndroidLinkNativeAotForRuntimeIdentifier");
		}

		[Test]
		[TestCase ("DesignTimeBuild=true", 1)]
		[TestCase ("_SkipNdkResolution=true", 2)]
		[TestCase ("_AndroidAllowMissingSdkTooling=true", 2)]
		[TestCase ("Configuration=Release", 2)]
		[TestCase ("AndroidSdkBuildToolsVersion=0.0.0", 2)]
		[TestCase ("LatestSupportedJavaVersion=99.0", 2)]
		public void NestedBuildToolingFallsBackForChangedInputs (string innerProperties, int javaCalls)
		{
			var project = new XamarinAndroidApplicationProject ();
			project.SetProperty ("EmbedAssembliesIntoApk", "true");
			var imports = project.Imports.Single (import => import.Project () == "Directory.Build.targets");
			imports.TextContent = () => """
				<Project>
				  <Target Name="_TestNestedTooling" DependsOnTargets="_ResolveMonoAndroidSdks">
				    <MSBuild Projects="$(MSBuildProjectFullPath)"
				        Targets="_TestInnerTooling"
				        Properties="$(_AndroidResolvedToolingProperties);RuntimeIdentifier=android-arm64;_ComputeFilesToPublishForRuntimeIdentifiers=true;_OuterIntermediateOutputPath=$(IntermediateOutputPath);_AndroidNdkDirectory=$([MSBuild]::Escape('$(_AndroidNdkDirectory)'));_TestPinnedNdkDirectory=$([MSBuild]::Escape('$(_AndroidNdkDirectory)'));$(_TestInnerProperties)" />
				  </Target>
				  <Target Name="_TestInnerTooling" DependsOnTargets="_ResolveMonoAndroidSdks">
				    <Error Condition=" '$(_AndroidUseResolvedSdk)' == 'true' and '$(_AndroidUseResolvedTooling)' == 'true' "
				        Text="Changed inner inputs must not reuse the entire tooling snapshot." />
				    <Error Condition=" '$(_SkipNdkResolution)' != 'true' and '$(_AndroidNdkDirectory)' != '$(_TestPinnedNdkDirectory)' "
				        Text="Inner builds must keep the outer build's resolved NDK." />
				  </Target>
				</Project>
				""";
			using var builder = CreateApkBuilder ();
			builder.Target = "_TestNestedTooling";
			builder.Verbosity = Microsoft.Build.Framework.LoggerVerbosity.Diagnostic;
			Assert.IsTrue (builder.Build (project, parameters: [$"_TestInnerProperties={innerProperties}"]));
			Assert.AreEqual (javaCalls, CountTask (builder.LastBuildOutput, "ValidateJavaVersion"));
			Assert.AreEqual (2, CountTask (builder.LastBuildOutput, "ResolveAndroidTooling"));
			Assert.AreEqual (innerProperties.StartsWith ("AndroidSdkBuildToolsVersion", StringComparison.Ordinal) ? 1 : 2,
				CountTask (builder.LastBuildOutput, "ResolveSdks"));
		}

		[Test]
		public void NestedBuildPreservesEscapedNdkPath ()
		{
			var path = Path.Combine ("temp", TestName);
			var ndkPath = CreateFauxAndroidNdkDirectory (Path.Combine (path, "O'Brien;ndk=1"));
			File.WriteAllText (Path.Combine (ndkPath, "source.properties"), "Pkg.Revision = 21.0.6113669");
			var project = new XamarinAndroidApplicationProject ();
			project.SetProperty ("AndroidNdkDirectory", ndkPath);
			var imports = project.Imports.Single (import => import.Project () == "Directory.Build.targets");
			imports.TextContent = () => """
				<Project>
				  <Target Name="_TestEscapedNdk" DependsOnTargets="_ResolveMonoAndroidSdks">
				    <MSBuild Projects="$(MSBuildProjectFullPath)"
				        Targets="_TestInnerEscapedNdk"
				        Properties="$(_AndroidResolvedToolingProperties);RuntimeIdentifier=android-arm64;_ComputeFilesToPublishForRuntimeIdentifiers=true;_OuterIntermediateOutputPath=$(IntermediateOutputPath);_AndroidNdkDirectory=$([MSBuild]::Escape('$(_AndroidNdkDirectory)'))" />
				  </Target>
				  <Target Name="_TestInnerEscapedNdk" DependsOnTargets="_ResolveMonoAndroidSdks">
				    <PropertyGroup>
				      <_TestExpectedNdkDirectory>$([MSBuild]::EnsureTrailingSlash('$(_AndroidOuterNdkDirectory)'))</_TestExpectedNdkDirectory>
				    </PropertyGroup>
				    <Error Condition=" '$(_AndroidUseResolvedSdk)' != 'true' or '$(_AndroidUseResolvedTooling)' != 'true' "
				        Text="Escaped NDK paths must not disable reuse." />
				    <Error Condition=" '$(_AndroidNdkDirectory)' != '$(_TestExpectedNdkDirectory)' "
				        Text="The NDK path changed during forwarding." />
				  </Target>
				</Project>
				""";
			using var builder = CreateApkBuilder (Path.Combine (path, "App"));
			builder.Target = "_TestEscapedNdk";
			builder.Verbosity = Microsoft.Build.Framework.LoggerVerbosity.Diagnostic;
			Assert.IsTrue (builder.Build (project, parameters: [$"AndroidNdkDirectory=\"{ndkPath}\""]));
			Assert.AreEqual (1, CountTask (builder.LastBuildOutput, "ResolveSdks"));
			Assert.AreEqual (1, CountTask (builder.LastBuildOutput, "ResolveAndroidTooling"));
			Assert.AreEqual (1, CountTask (builder.LastBuildOutput, "ValidateJavaVersion"));
		}

		[Test]
		public void NestedToolingSnapshotDoesNotSkipProjectReferenceResolution ()
		{
			var directory = Path.Combine ("temp", TestName);
			var library = new XamarinAndroidLibraryProject { ProjectName = "Library" };
			library.SetProperty ("RuntimeIdentifiers", "android-arm64");
			var project = new XamarinAndroidApplicationProject { ProjectName = "Application" };
			project.AddReference (library);
			project.SetProperty ("EmbedAssembliesIntoApk", "true");
			var imports = project.Imports.Single (import => import.Project () == "Directory.Build.targets");
			imports.TextContent = () => """
				<Project>
				  <Target Name="_TestReferenceTooling" DependsOnTargets="_ResolveMonoAndroidSdks">
				    <MSBuild Projects="@(ProjectReference)"
				        Targets="_ResolveMonoAndroidSdks"
				        Properties="$(_AndroidResolvedToolingProperties);RuntimeIdentifier=android-arm64;_ComputeFilesToPublishForRuntimeIdentifiers=true;_OuterIntermediateOutputPath=$(IntermediateOutputPath)" />
				  </Target>
				</Project>
				""";
			using var libraryBuilder = CreateDllBuilder (Path.Combine (directory, library.ProjectName));
			libraryBuilder.Save (library);
			using var builder = CreateApkBuilder (Path.Combine (directory, project.ProjectName));
			builder.Target = "_TestReferenceTooling";
			builder.Verbosity = Microsoft.Build.Framework.LoggerVerbosity.Diagnostic;
			Assert.IsTrue (builder.Build (project));
			Assert.AreEqual (2, CountTask (builder.LastBuildOutput, "ResolveSdks"));
			Assert.AreEqual (2, CountTask (builder.LastBuildOutput, "ResolveAndroidTooling"));
			Assert.AreEqual (2, CountTask (builder.LastBuildOutput, "ValidateJavaVersion"));
		}

		static int CountTask (IEnumerable<string> output, string task) =>
			output.Count (line => line.Contains ($"Task \"{task}\" (TaskId:", StringComparison.Ordinal));

		[Test]
		public void DotNetWorkloadSearchUsesEnglishDescription ()
		{
			const string description = ".NET SDK Workload for building Android applications.";
			var testPath = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (testPath);
			TestOutputDirectories [TestContext.CurrentContext.Test.ID] = testPath;

			var manifestRoot = TestEnvironment.UseLocalBuildOutput ?
				TestEnvironment.WorkloadManifestOverridePath :
				Path.Combine (TestEnvironment.DotNetPreviewDirectory, "sdk-manifests");
			Assert.IsTrue (Directory.Exists (manifestRoot), $"The workload manifest root '{manifestRoot}' should exist.");
			var androidManifestDirectory = $"{Path.DirectorySeparatorChar}microsoft.net.sdk.android{Path.DirectorySeparatorChar}";
			var englishCatalog = Directory.EnumerateFiles (manifestRoot, "WorkloadManifest.en.json", SearchOption.AllDirectories)
				.FirstOrDefault (path => path.IndexOf (androidManifestDirectory, StringComparison.OrdinalIgnoreCase) >= 0);
			Assert.IsNotNull (englishCatalog, $"An English Android workload localization catalog should be installed under '{manifestRoot}'.");
			StringAssert.Contains (description, File.ReadAllText (englishCatalog));

			var dotnet = new DotNetCLI (Path.Combine (testPath, "unused.csproj"));
			dotnet.EnvironmentVariables ["DOTNET_CLI_UI_LANGUAGE"] = "en";

			Assert.IsTrue (dotnet.WorkloadSearch ("android"), "`dotnet workload search android` should succeed");
			StringAssert.Contains (
				description,
				File.ReadAllText (dotnet.ProcessLogFile),
				"`dotnet workload search android` should use the English workload description");
		}

		[Test]
		public void DotNetNewFramework (
			[Values ("android", "androidlib", "android-bindinglib", "androidwear", "androidtest")] string template,
			[Values (null, "net11.0-android", "net10.0-android")] string framework)
		{
			var templateName = TestName.Replace ("-", "");
			var templatePath = Path.Combine (Root, "temp", templateName);
			if (Directory.Exists (templatePath))
				Directory.Delete (templatePath, true);

			TestOutputDirectories [TestContext.CurrentContext.Test.ID] = templatePath;
			var projectFile = Path.Combine (templatePath, $"{templateName}.csproj");
			var dotnet = new DotNetCLI (projectFile);
			Assert.IsTrue (dotnet.New (template, noRestore: true, framework: framework), $"`dotnet new {template}` should succeed with framework '{framework}'");

			var project = System.Xml.Linq.XDocument.Load (projectFile);
			Assert.AreEqual (framework ?? "net11.0-android", project.Root.Element ("PropertyGroup").Element ("TargetFramework").Value);
		}

		[Test]
		public void DotNetNew ([Values ("android", "androidlib", "android-bindinglib", "androidwear")] string template)
		{
			var templateName = TestName.Replace ("-", "");
			var templatePath = Path.Combine (Root, "temp", templateName);
			if (Directory.Exists (templatePath))
				Directory.Delete (templatePath, true);

			TestOutputDirectories [TestContext.CurrentContext.Test.ID] = templatePath;
			var dotnet = new DotNetCLI (Path.Combine (templatePath, $"{templateName}.csproj"));
			Assert.IsTrue (dotnet.New (template, noRestore: true), $"`dotnet new {template} --no-restore` should succeed");

			var repoNuGetConfig = Path.Combine (XABuildPaths.TopDirectory, "NuGet.config");
			var projectNuGetConfig = Path.Combine (dotnet.ProjectDirectory, "NuGet.config");
			File.Copy (repoNuGetConfig, projectNuGetConfig);
			FileAssert.AreEqual (repoNuGetConfig, projectNuGetConfig);

			File.WriteAllBytes (Path.Combine (dotnet.ProjectDirectory, "foo.jar"), ResourceData.JavaSourceJarTestJar);
			Assert.IsTrue (dotnet.New ("android-activity"), "`dotnet new android-activity` should succeed");
			Assert.IsTrue (dotnet.New ("android-layout", Path.Combine (dotnet.ProjectDirectory, "Resources", "layout")), "`dotnet new android-layout` should succeed");

			// Debug build
			Assert.IsTrue (dotnet.Build (parameters: GetBuildParameters ("Debug")), "`dotnet build` should succeed");
			dotnet.AssertHasNoWarnings ();

			// Release build
			Assert.IsTrue (dotnet.Build (parameters: GetBuildParameters ("Release")), "`dotnet build` should succeed");
			dotnet.AssertHasNoWarnings ();

			string [] GetBuildParameters (string configuration)
			{
				var parameters = new List<string> {
					$"Configuration={configuration}",
					"TrimmerSingleWarn=false",
				};
				if (template == "androidwear") {
					var mavenTargets = Path.Combine (
						XABuildPaths.TopDirectory,
						"src",
						"Xamarin.Android.Build.Tasks",
						"Tests",
						"Xamarin.Android.Build.Tests",
						"dotnet-public-maven.targets");
					parameters.Add ($"CustomAfterMicrosoftCommonTargets=\"{mavenTargets}\"");
				}
				return parameters.ToArray ();
			}
		}

		static IEnumerable<object[]> Get_DotNetPack_Data ()
		{
			var ret = new List<object[]> ();

			foreach (AndroidRuntime runtime in new[] { AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT }) {
				AddTestData (
					dotnetVersion: XABuildConfig.PreviousDotNetTargetFramework,
					platform: "android",
					apiLevel: new Version (36, 0),
					runtime: runtime);

				AddTestData (
					dotnetVersion: XABuildConfig.PreviousDotNetTargetFramework,
					platform: "android36",
					apiLevel: new Version (36, 0),
					runtime: runtime);

				AddTestData (
					dotnetVersion: XABuildConfig.LatestDotNetTargetFramework,
					platform: "android",
					apiLevel: XABuildConfig.AndroidDefaultTargetDotnetApiLevel,
					runtime: runtime);

				AddTestData (
					dotnetVersion: XABuildConfig.LatestDotNetTargetFramework,
					platform: $"android{XABuildConfig.AndroidDefaultTargetDotnetApiLevel}",
					apiLevel: XABuildConfig.AndroidDefaultTargetDotnetApiLevel,
					runtime: runtime);
			}

			return ret;

			void AddTestData (string dotnetVersion, string platform, Version apiLevel, AndroidRuntime runtime)
			{
				ret.Add (new object[] {
					dotnetVersion,
					platform,
					apiLevel,
					runtime,
				});
			}
		}

		[Test]
		[TestCaseSource (nameof (Get_DotNetPack_Data))]
		public void DotNetPack (string dotnetVersion, string platform, Version apiLevel, [Values (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
		{
			const bool isRelease = true;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			var targetFramework = $"{dotnetVersion}-{platform}";
			var proj = new XamarinAndroidLibraryProject {
				TargetFramework = targetFramework,
				IsRelease = isRelease,
				EnableDefaultItems = true,
				Sources = {
					new BuildItem.Source ("Foo.cs") {
						TextContent = () => "public class Foo { }",
					},
					new AndroidItem.AndroidResource ("Resources\\raw\\bar.txt") {
						BinaryContent = () => [],
					},
					new AndroidItem.AndroidLibrary ("sub\\directory\\foo.jar") {
						BinaryContent = () => ResourceData.JavaSourceJarTestJar,
					},
					new AndroidItem.AndroidLibrary ("sub\\directory\\bar.aar") {
						WebContent = $"{TestEnvironment.DotNetPublicMaven}/com/balysv/material-menu/1.1.0/material-menu-1.1.0.aar",
					},
					new AndroidItem.AndroidJavaSource ("JavaSourceTest.java") {
						Encoding = Encoding.ASCII,
						TextContent = () =>
@"package com.xamarin.android.test.msbuildtest;
public class JavaSourceTest {
	public String Say (String quote) {
		return quote;
	}
}",
					},
				},
				PackageReferences = {
					new Package { Id = "Xamarin.Kotlin.StdLib", Version = "2.0.10.1" },
					new Package { Id = "Xamarin.Kotlin.StdLib.Common", Version = "2.0.10.1" },
					new Package { Id = "Xamarin.KotlinX.Serialization.Core.Jvm", Version = "1.7.1.1" },
				}
			};
			proj.SetRuntime (runtime);
			if (IsPreviewFrameworkVersion (targetFramework)) {
				proj.SetProperty ("EnablePreviewFeatures", "true");
			}
			proj.OtherBuildItems.Add (new AndroidItem.AndroidLibrary ("sub\\directory\\arm64-v8a\\libfoo.so") {
				BinaryContent = () => [],
			});
			proj.OtherBuildItems.Add (new AndroidItem.AndroidNativeLibrary (default (Func<string>)) {
				Update = () => "libfoo.so",
				MetadataValues = "Link=x86\\libfoo.so",
				BinaryContent = () => [],
			});
			proj.OtherBuildItems.Add (new AndroidItem.LibraryProjectZip ("..\\baz.aar") {
				WebContent = $"{TestEnvironment.DotNetPublicMaven}/com/balysv/material-menu/1.1.0/material-menu-1.1.0.aar",
				MetadataValues = "Bind=false",
			});
			proj.OtherBuildItems.Add (new AndroidItem.AndroidLibrary (default (Func<string>)) {
				Update = () => "nopack.aar",
				WebContent = $"{TestEnvironment.DotNetPublicMaven}/com/balysv/material-menu/1.1.0/material-menu-1.1.0.aar",
				MetadataValues = "Pack=false;Bind=false",
			});
			proj.OtherBuildItems.Add (new AndroidItem.AndroidMavenLibrary ("org.jetbrains.kotlinx:kotlinx-serialization-json-jvm") {
				MetadataValues = $"Version=1.3.3;Bind=false;Repository={TestEnvironment.DotNetPublicMaven}",
				BinaryContent = () => [],
			});

			var projBuilder = CreateDllBuilder ();
			projBuilder.Save (proj);
			var dotnet = new DotNetCLI (Path.Combine (Root, projBuilder.ProjectDirectory, proj.ProjectFilePath));
			if (dotnetVersion == XABuildConfig.PreviousDotNetTargetFramework) {
				dotnet.JavaSdkPath = GetPreviousWorkloadJavaSdkPath ();
			}
			Assert.IsTrue (dotnet.Pack (parameters: ["Configuration=Release"]), "`dotnet pack` should succeed");

			var nupkgPath = Path.Combine (Root, projBuilder.ProjectDirectory, proj.OutputPath, $"{proj.ProjectName}.1.0.0.nupkg");
			FileAssert.Exists (nupkgPath);
			using var nupkg = ZipHelper.OpenZip (nupkgPath);
			string aarPath = $"lib/{dotnetVersion}-android{apiLevel}/{proj.ProjectName}.aar";
			nupkg.AssertContainsEntry (nupkgPath, aarPath);
			nupkg.AssertContainsEntry (nupkgPath, $"lib/{dotnetVersion}-android{apiLevel}/{proj.ProjectName}.dll");
			nupkg.AssertContainsEntry (nupkgPath, $"lib/{dotnetVersion}-android{apiLevel}/bar.aar");
			nupkg.AssertDoesNotContainEntry (nupkgPath, "content/bar.aar");
			nupkg.AssertDoesNotContainEntry (nupkgPath, "content/sub/directory/bar.aar");
			nupkg.AssertDoesNotContainEntry (nupkgPath, $"contentFiles/any/{dotnetVersion}-android{apiLevel}/sub/directory/bar.aar");
			nupkg.AssertDoesNotContainEntry (nupkgPath, $"lib/{dotnetVersion}-android{apiLevel}/nopack.aar");
			nupkg.AssertDoesNotContainEntry (nupkgPath, "content/nopack.aar");
			nupkg.AssertDoesNotContainEntry (nupkgPath, $"contentFiles/any/{dotnetVersion}-android{apiLevel}/nopack.aar");
			nupkg.AssertContainsEntry (nupkgPath, $"lib/{dotnetVersion}-android{apiLevel}/baz.aar");
			nupkg.AssertDoesNotContainEntry (nupkgPath, $"lib/{dotnetVersion}-android{apiLevel}/_Microsoft.Android.Resource.Designer.dll");

			using var aarStream = new MemoryStream ();
			var aarEntry = nupkg.GetEntry (aarPath) ?? throw new InvalidOperationException ($"Missing AAR '{aarPath}'.");
			using (var source = aarEntry.Open ())
				source.CopyTo (aarStream);
			aarStream.Seek (0, SeekOrigin.Begin);

			// Look for the Maven dependency, foo.jar, and the compiled AndroidJavaSource output under libs/
			using var aar = new ZipArchive (aarStream, ZipArchiveMode.Read, leaveOpen: true);
			int count = aar.Entries.Count (e =>
				e.FullName.StartsWith ("libs/", StringComparison.OrdinalIgnoreCase) &&
				e.FullName.EndsWith (".jar", StringComparison.OrdinalIgnoreCase));
			Assert.AreEqual (3, count, $"There should be 3 .jar files in the {aarPath} archive, but found {count}.");
		}

		static IEnumerable<object[]> Get_DotNetTargetFrameworks_Data ()
		{
			var ret = new List<object[]> ();

			foreach (AndroidRuntime runtime in new[] { AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT }) {
				AddTestData (
					dotnetVersion: XABuildConfig.PreviousDotNetTargetFramework,
					platform: "android",
					apiLevel: new Version (36, 0),
					runtime: runtime
				);

				AddTestData (
					dotnetVersion: XABuildConfig.LatestDotNetTargetFramework,
					platform: "android",
					apiLevel: XABuildConfig.AndroidDefaultTargetDotnetApiLevel,
					runtime: runtime
				);

				AddTestData (
					dotnetVersion: XABuildConfig.LatestDotNetTargetFramework,
					platform: $"android{XABuildConfig.AndroidDefaultTargetDotnetApiLevel}",
					apiLevel: XABuildConfig.AndroidDefaultTargetDotnetApiLevel,
					runtime: runtime
				);

				AddTestData (
					dotnetVersion: XABuildConfig.LatestDotNetTargetFramework,
					platform: XABuildConfig.AndroidLatestStableApiLevel == XABuildConfig.AndroidDefaultTargetDotnetApiLevel ? null : $"android{XABuildConfig.AndroidLatestStableApiLevel}",
					apiLevel: XABuildConfig.AndroidLatestStableApiLevel,
					runtime: runtime
				);

				AddTestData (
					dotnetVersion: XABuildConfig.LatestDotNetTargetFramework,
					platform: XABuildConfig.AndroidLatestUnstableApiLevel == XABuildConfig.AndroidLatestStableApiLevel ? null : $"android{XABuildConfig.AndroidLatestUnstableApiLevel}",
					apiLevel: XABuildConfig.AndroidLatestUnstableApiLevel,
					runtime: runtime
				);
			}

			return ret;

			void AddTestData (string dotnetVersion, string platform, Version apiLevel, AndroidRuntime runtime)
			{
				ret.Add (new object[] {
					dotnetVersion,
					platform,
					apiLevel,
					runtime,
				});
			}
		}

		static bool IsPreviewFrameworkVersion (string targetFramework)
		{
			return (targetFramework.Contains ($"{XABuildConfig.AndroidLatestUnstableApiLevel}")
				&& XABuildConfig.AndroidLatestUnstableApiLevel != XABuildConfig.AndroidLatestStableApiLevel);
		}

		[Test]
		public void DotNetPublishDefaultValues ([ValueSource (typeof (BaseTest), nameof (BaseTest.ValidRuntimeConfigurations))] (bool isRelease, AndroidRuntime runtime) configuration)
		{
			var (isRelease, runtime) = configuration;

			// TODO: fix NativeAOT build. It currently fails with
			//
			//  error MSB3030: Could not copy the file "bin/Release/native/UnnamedProject.so" because it was not found.
			//
			// The file exists in bin/Release/{RID}/native/UnnamedProject.so
			if (runtime == AndroidRuntime.NativeAOT) {
				Assert.Ignore ("NativeAOT publish support is broken atm");
			}

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = isRelease
			};
			proj.SetRuntime (runtime);
			var builder = CreateDllBuilder ();
			builder.Save (proj);
			var dotnet = new DotNetCLI (Path.Combine (Root, builder.ProjectDirectory, proj.ProjectFilePath));
			Assert.IsTrue (dotnet.Publish (), "`dotnet publish` should succeed");
		}

		[Test]
		public void DotNetPublish ([Values] bool isRelease, [ValueSource (nameof(Get_DotNetTargetFrameworks_Data))] object[] data)
		{
			var dotnetVersion = (string)data[0];
			var platform = (string)data[1];
			var apiLevel = (Version)data[2];
			var runtime = (AndroidRuntime)data[3];

			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			//FIXME: will revisit this in a future PR
			if (dotnetVersion != XABuildConfig.LatestDotNetTargetFramework) {
				Assert.Ignore ("error NETSDK1185: The Runtime Pack for FrameworkReference 'Microsoft.Android.Runtime.34.android-arm' was not available. This may be because DisableTransitiveFrameworkReferenceDownloads was set to true.");
			}

			if (string.IsNullOrEmpty (platform))
				Assert.Ignore ($"Test for API level {apiLevel} was skipped as it matched the default or latest stable API level.");

			var targetFramework = $"{dotnetVersion}-{platform}";
			const string runtimeIdentifier = "android-arm64";
			var proj = new XamarinAndroidApplicationProject {
				TargetFramework = targetFramework,
				IsRelease = isRelease,
				EnableDefaultItems = true,
				ExtraNuGetConfigSources = {
					Path.Combine (XABuildPaths.BuildOutputDirectory, "nuget-unsigned"),
				}
			};
			proj.SetRuntime (runtime);
			proj.SetProperty (KnownProperties.RuntimeIdentifier, runtimeIdentifier);

			var preview = IsPreviewFrameworkVersion (targetFramework);
			if (preview) {
				proj.SetProperty ("EnablePreviewFeatures", "true");
			}

			var projBuilder = CreateDllBuilder ();
			projBuilder.Save (proj);
			var dotnet = new DotNetCLI (Path.Combine (Root, projBuilder.ProjectDirectory, proj.ProjectFilePath));
			dotnet.Verbosity = "detailed";
			string[] configParam = isRelease ? new [] { "Configuration=Release" } : new [] { "Configuration=Debug" };
			Assert.IsTrue (dotnet.Publish (parameters: configParam), "first `dotnet publish` should succeed");

			// NOTE: Preview API levels emit XA4211
			if (!preview) {
				dotnet.AssertHasNoWarnings ();
			}

			// Only check latest TFM, as previous or preview TFMs will come from NuGet
			if (dotnetVersion == XABuildConfig.LatestDotNetTargetFramework && !preview) {
				var buildOutput = dotnet.LastBuildOutput.ToArray ();
				var versionString = apiLevel.Minor == 0 ? $"{apiLevel.Major}" : $"{apiLevel.Major}.{apiLevel.Minor}";
				AssertUsingPack (buildOutput, $"Microsoft.Android.Ref.{versionString}", "ref", dotnetVersion, "Mono.Android.dll");

				var runtimeApiLevel = (apiLevel == XABuildConfig.AndroidDefaultTargetDotnetApiLevel && apiLevel < XABuildConfig.AndroidLatestStableApiLevel) ? XABuildConfig.AndroidLatestStableApiLevel : apiLevel;
				versionString = runtimeApiLevel.Minor == 0 ? $"{runtimeApiLevel.Major}" : $"{runtimeApiLevel.Major}.{runtimeApiLevel.Minor}";
				AssertUsingPack (buildOutput, $"Microsoft.Android.Runtime.{versionString}.android", "runtimes", "android", "lib", dotnetVersion, "Mono.Android.dll");
			}

			var publishDirectory = Path.Combine (Root, projBuilder.ProjectDirectory, proj.OutputPath, runtimeIdentifier, "publish");
			var apk = Path.Combine (publishDirectory, $"{proj.PackageName}.apk");
			var apkSigned = Path.Combine (publishDirectory, $"{proj.PackageName}-Signed.apk");
			// NOTE: the unsigned .apk doesn't exist when $(AndroidPackageFormats) is `aab;apk`
			if (!isRelease) {
				FileAssert.Exists (apk);
			}
			FileAssert.Exists (apkSigned);

			// NOTE: $(AndroidPackageFormats) defaults to `aab;apk` in Release
			if (isRelease) {
				var aab = Path.Combine (publishDirectory, $"{proj.PackageName}.aab");
				var aabSigned = Path.Combine (publishDirectory, $"{proj.PackageName}-Signed.aab");
				FileAssert.Exists (aab);
				FileAssert.Exists (aabSigned);
			}
		}

		static void AssertUsingPack (IEnumerable<string> buildOutput, string packName, params string [] assemblyPath)
		{
			var packDirectory = Path.Combine (TestEnvironment.DotNetPreviewPacksDirectory, packName);
			var assemblyPathSuffix = Path.Combine (assemblyPath);
			var assemblyFileName = Path.GetFileName (assemblyPathSuffix);
			Assert.That (Directory.EnumerateFiles (packDirectory, assemblyFileName, SearchOption.AllDirectories),
				Has.Exactly (1).Matches<string> (path =>
					path.EndsWith (assemblyPathSuffix, StringComparison.OrdinalIgnoreCase) && buildOutput.ContainsText (path)),
				$"Build should use exactly one installed '{packName}' pack.");
		}

		[Test]
		[TestCaseSource (nameof (Get_DotNetTargetFrameworks_Data))]
		public void MauiTargetFramework (string dotnetVersion, string platform, Version apiLevel, AndroidRuntime runtime)
		{
			bool isRelease = runtime == AndroidRuntime.NativeAOT;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			if (string.IsNullOrEmpty (platform))
				Assert.Ignore ($"Test for API level {apiLevel} was skipped as it matched the default or latest stable API level.");

			var targetFramework = $"{dotnetVersion}-{platform}";
			var library = new XamarinAndroidLibraryProject {
				IsRelease = isRelease,
				TargetFramework = targetFramework,
				EnableDefaultItems = true,
				ExtraNuGetConfigSources = {
					Path.Combine (XABuildPaths.BuildOutputDirectory, "nuget-unsigned"),
				}
			};
			library.SetRuntime (runtime);

			var preview = IsPreviewFrameworkVersion (targetFramework);
			if (preview) {
				library.SetProperty ("EnablePreviewFeatures", "true");
			}
			library.Sources.Clear ();
			library.Sources.Add (new BuildItem.Source ("Foo.cs") {
				TextContent = () =>
@"public abstract partial class ViewHandler<TVirtualView, TNativeView> { }

public interface IView { }

public abstract class Foo<TVirtualView, TNativeView> : ViewHandler<TVirtualView, TNativeView>
	where TVirtualView : class, IView
#if ANDROID
	where TNativeView : Android.Views.View
#else
	where TNativeView : class
#endif
{
}",
			});

			var builder = CreateDllBuilder ();
			string [] parameters = null;
			if (dotnetVersion == XABuildConfig.PreviousDotNetTargetFramework) {
				parameters = [$"JavaSdkDirectory=\"{GetPreviousWorkloadJavaSdkPath ()}\""];
			}
			Assert.IsTrue (builder.Build (library, parameters: parameters), $"{library.ProjectName} should succeed");
			// NOTE: Preview API levels emit XA4211
			if (!preview) {
				builder.AssertHasNoWarnings ();
			}
		}

		static string GetPreviousWorkloadJavaSdkPath ()
		{
			var architecture = RuntimeInformation.ProcessArchitecture.ToString ().ToUpperInvariant ();
			var variableName = $"JAVA_HOME_21_{architecture}";
			var javaSdkPath = Environment.GetEnvironmentVariable (variableName);
			if (string.IsNullOrEmpty (javaSdkPath)) {
				Assert.Ignore ($"The previous Android workload requires JDK 21, but {variableName} is not set.");
			}
			return javaSdkPath;
		}
	}
}
