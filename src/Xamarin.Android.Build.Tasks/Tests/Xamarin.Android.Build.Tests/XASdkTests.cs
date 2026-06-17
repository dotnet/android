using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using Xamarin.Android.Tools;
using Xamarin.ProjectTools;
using Xamarin.Tools.Zip;
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
		public void DotNetNew ([Values ("android", "androidlib", "android-bindinglib", "androidwear")] string template)
		{
			var templateName = TestName.Replace ("-", "");
			var templatePath = Path.Combine (Root, "temp", templateName);
			if (Directory.Exists (templatePath))
				Directory.Delete (templatePath, true);

			TestOutputDirectories [TestContext.CurrentContext.Test.ID] = templatePath;
			var dotnet = new DotNetCLI (Path.Combine (templatePath, $"{templateName}.csproj"));
			Assert.IsTrue (dotnet.New (template), $"`dotnet new {template}` should succeed");
			File.WriteAllBytes (Path.Combine (dotnet.ProjectDirectory, "foo.jar"), ResourceData.JavaSourceJarTestJar);
			Assert.IsTrue (dotnet.New ("android-activity"), "`dotnet new android-activity` should succeed");
			Assert.IsTrue (dotnet.New ("android-layout", Path.Combine (dotnet.ProjectDirectory, "Resources", "layout")), "`dotnet new android-layout` should succeed");

			// Debug build
			Assert.IsTrue (dotnet.Build (parameters: new [] { "Configuration=Debug", "TrimmerSingleWarn=false" }), "`dotnet build` should succeed");
			dotnet.AssertHasNoWarnings ();

			// Release build
			Assert.IsTrue (dotnet.Build (parameters: new [] { "Configuration=Release", "TrimmerSingleWarn=false" }), "`dotnet build` should succeed");
			dotnet.AssertHasNoWarnings ();
		}

		static IEnumerable<object[]> Get_DotNetPack_Data ()
		{
			var ret = new List<object[]> ();

			foreach (AndroidRuntime runtime in Enum.GetValues (typeof (AndroidRuntime))) {
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
		public void DotNetPack (string dotnetVersion, string platform, Version apiLevel, [Values] AndroidRuntime runtime)
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
						WebContent = "https://repo1.maven.org/maven2/com/balysv/material-menu/1.1.0/material-menu-1.1.0.aar",
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
				WebContent = "https://repo1.maven.org/maven2/com/balysv/material-menu/1.1.0/material-menu-1.1.0.aar",
				MetadataValues = "Bind=false",
			});
			proj.OtherBuildItems.Add (new AndroidItem.AndroidLibrary (default (Func<string>)) {
				Update = () => "nopack.aar",
				WebContent = "https://repo1.maven.org/maven2/com/balysv/material-menu/1.1.0/material-menu-1.1.0.aar",
				MetadataValues = "Pack=false;Bind=false",
			});
			proj.OtherBuildItems.Add (new AndroidItem.AndroidMavenLibrary ("org.jetbrains.kotlinx:kotlinx-serialization-json-jvm") {
				MetadataValues = "Version=1.3.3;Bind=false",
				BinaryContent = () => [],
			});

			var projBuilder = CreateDllBuilder ();
			projBuilder.Save (proj);
			var dotnet = new DotNetCLI (Path.Combine (Root, projBuilder.ProjectDirectory, proj.ProjectFilePath));
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
			var aarEntry = nupkg.ReadEntry (aarPath);
			aarEntry.Extract (aarStream);
			aarStream.Seek (0, SeekOrigin.Begin);

			// Look for 2 .jar files under libs/
			using var aar = ZipArchive.Open (aarStream);
			int count = aar.Count (e =>
				e.FullName.StartsWith ("libs/", StringComparison.OrdinalIgnoreCase) &&
				e.FullName.EndsWith (".jar", StringComparison.OrdinalIgnoreCase));
			Assert.AreEqual (2, count, $"There should be 2 .jar files in the {aarPath} archive, but found {count}.");
		}

		static IEnumerable<object[]> Get_DotNetTargetFrameworks_Data ()
		{
			var ret = new List<object[]> ();

			foreach (AndroidRuntime runtime in Enum.GetValues (typeof (AndroidRuntime))) {
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
		public void DotNetPublishDefaultValues ([Values] bool isRelease, [Values] AndroidRuntime runtime)
		{
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

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
				},
				Imports = {
					new Import (() => "ApplicationArtifacts.targets") {
						TextContent = () => """
<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <GetApplicationArtifactsDependsOn>
      $(GetApplicationArtifactsDependsOn);
      AddMauiApplicationArtifactMetadata
    </GetApplicationArtifactsDependsOn>
  </PropertyGroup>
  <Target Name="AddMauiApplicationArtifactMetadata">
    <ItemGroup>
      <_ObservedApplicationArtifact Include="@(ApplicationArtifact)" />
      <_ObservedUnsignedApkApplicationArtifact
          Include="@(_ObservedApplicationArtifact)"
          Condition=" '%(_ObservedApplicationArtifact.Filename)%(_ObservedApplicationArtifact.Extension)' == '$(_AndroidPackage).apk' And '%(_ObservedApplicationArtifact.PackageFormat)' == 'apk' And '%(_ObservedApplicationArtifact.Signed)' == 'false' " />
      <_ObservedSignedApkApplicationArtifact
          Include="@(_ObservedApplicationArtifact)"
          Condition=" '%(_ObservedApplicationArtifact.Filename)%(_ObservedApplicationArtifact.Extension)' == '$(_AndroidPackage)-Signed.apk' And '%(_ObservedApplicationArtifact.PackageFormat)' == 'apk' And '%(_ObservedApplicationArtifact.Signed)' == 'true' " />
      <_ObservedUnsignedAabApplicationArtifact
          Include="@(_ObservedApplicationArtifact)"
          Condition=" '%(_ObservedApplicationArtifact.Filename)%(_ObservedApplicationArtifact.Extension)' == '$(_AndroidPackage).aab' And '%(_ObservedApplicationArtifact.PackageFormat)' == 'aab' And '%(_ObservedApplicationArtifact.Signed)' == 'false' " />
      <_ObservedSignedAabApplicationArtifact
          Include="@(_ObservedApplicationArtifact)"
          Condition=" '%(_ObservedApplicationArtifact.Filename)%(_ObservedApplicationArtifact.Extension)' == '$(_AndroidPackage)-Signed.aab' And '%(_ObservedApplicationArtifact.PackageFormat)' == 'aab' And '%(_ObservedApplicationArtifact.Signed)' == 'true' " />
    </ItemGroup>
    <Error Condition=" '@(_ObservedApplicationArtifact)' == '' "
        Text="Expected ApplicationArtifact items before MAUI metadata augmentation." />
    <Error Condition=" '@(_ObservedSignedApkApplicationArtifact)' == '' "
        Text="Expected signed APK ApplicationArtifact item before MAUI metadata augmentation." />
    <Error Condition=" '$(Configuration)' != 'Release' And '@(_ObservedUnsignedApkApplicationArtifact)' == '' "
        Text="Expected unsigned APK ApplicationArtifact item before MAUI metadata augmentation." />
    <Error Condition=" '$(Configuration)' == 'Release' And '@(_ObservedUnsignedAabApplicationArtifact)' == '' "
        Text="Expected unsigned AAB ApplicationArtifact item before MAUI metadata augmentation." />
    <Error Condition=" '$(Configuration)' == 'Release' And '@(_ObservedSignedAabApplicationArtifact)' == '' "
        Text="Expected signed AAB ApplicationArtifact item before MAUI metadata augmentation." />
    <WriteLinesToFile
        File="$(MSBuildProjectDirectory)/observed-application-artifact-items.txt"
        Lines="@(_ObservedApplicationArtifact->'%(FullPath)|%(Filename)%(Extension)|%(PackageFormat)|%(Signed)|%(PackageId)')"
        Overwrite="true" />
    <ItemGroup>
      <ApplicationArtifact Update="@(ApplicationArtifact)" MauiArtifact="true" />
    </ItemGroup>
  </Target>
  <Target Name="WriteApplicationArtifactItems" AfterTargets="Publish">
    <WriteLinesToFile
        File="$(MSBuildProjectDirectory)/application-artifact-items.txt"
        Lines="%(ApplicationArtifact.FullPath)|%(ApplicationArtifact.Filename)%(ApplicationArtifact.Extension)|%(ApplicationArtifact.PackageFormat)|%(ApplicationArtifact.Signed)|%(ApplicationArtifact.PackageId)"
        Overwrite="true" />
  </Target>
  <Target Name="WriteResolvedPackagePublishItems" AfterTargets="_CalculateAndroidFilesToPublish">
    <ItemGroup>
      <_ResolvedPackagePublishItem
          Include="@(ResolvedFileToPublish)"
          Condition=" '%(ResolvedFileToPublish.Extension)' == '.apk' Or '%(ResolvedFileToPublish.Extension)' == '.aab' " />
    </ItemGroup>
    <WriteLinesToFile
        File="$(MSBuildProjectDirectory)/resolved-package-publish-items.txt"
        Lines="@(_ResolvedPackagePublishItem->'%(FullPath)|%(RelativePath)')"
        Overwrite="true" />
  </Target>
  <Target Name="WriteQueriedApplicationArtifactItems" AfterTargets="GetApplicationArtifacts">
    <WriteLinesToFile
        File="$(MSBuildProjectDirectory)/queried-application-artifact-items.txt"
        Lines="%(ApplicationArtifact.FullPath)|%(ApplicationArtifact.Filename)%(ApplicationArtifact.Extension)|%(ApplicationArtifact.PackageFormat)|%(ApplicationArtifact.Signed)|%(ApplicationArtifact.PackageId)|%(ApplicationArtifact.MauiArtifact)"
        Overwrite="true" />
  </Target>
  <Target Name="WritePublishReturnedApplicationArtifactItems">
    <MSBuild
        Projects="$(MSBuildProjectFullPath)"
        Targets="Publish"
        Properties="Configuration=$(Configuration)">
      <Output TaskParameter="TargetOutputs" ItemName="_PublishReturnedApplicationArtifact" />
    </MSBuild>
    <WriteLinesToFile
        File="$(MSBuildProjectDirectory)/publish-returned-application-artifact-items.txt"
        Lines="@(_PublishReturnedApplicationArtifact->'%(FullPath)|%(Filename)%(Extension)|%(PackageFormat)|%(Signed)|%(PackageId)')"
        Overwrite="true" />
  </Target>
</Project>
"""
					},
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
				if (runtime != AndroidRuntime.NativeAOT) {
					dotnet.AssertHasNoWarnings ();
				} else {
					// NativeAOT currently issues 1 warning
					dotnet.AssertHasSomeWarnings (1);
				}
			}

			// Only check latest TFM, as previous or preview TFMs will come from NuGet
			if (dotnetVersion == XABuildConfig.LatestDotNetTargetFramework && !preview) {
				var versionString = apiLevel.Minor == 0 ? $"{apiLevel.Major}" : $"{apiLevel.Major}.{apiLevel.Minor}";
				var refDirectory = Directory.GetDirectories (Path.Combine (TestEnvironment.DotNetPreviewPacksDirectory, $"Microsoft.Android.Ref.{versionString}")).LastOrDefault ();
				var expectedMonoAndroidRefPath = Path.Combine (refDirectory, "ref", dotnetVersion, "Mono.Android.dll");
				Assert.IsTrue (dotnet.LastBuildOutput.ContainsText (expectedMonoAndroidRefPath), $"Build should be using {expectedMonoAndroidRefPath}");

				var runtimeApiLevel = (apiLevel == XABuildConfig.AndroidDefaultTargetDotnetApiLevel && apiLevel < XABuildConfig.AndroidLatestStableApiLevel) ? XABuildConfig.AndroidLatestStableApiLevel : apiLevel;
				versionString = runtimeApiLevel.Minor == 0 ? $"{runtimeApiLevel.Major}" : $"{runtimeApiLevel.Major}.{runtimeApiLevel.Minor}";
				var runtimeDirectory = Directory.GetDirectories (Path.Combine (TestEnvironment.DotNetPreviewPacksDirectory, $"Microsoft.Android.Runtime.{versionString}.android")).LastOrDefault ();
				var expectedMonoAndroidRuntimePath = Path.Combine (runtimeDirectory, "runtimes", "android", "lib", dotnetVersion, "Mono.Android.dll");
				Assert.IsTrue (dotnet.LastBuildOutput.ContainsText (expectedMonoAndroidRuntimePath), $"Build should be using {expectedMonoAndroidRuntimePath}");
			}

			var packageDirectory = Path.Combine (Root, projBuilder.ProjectDirectory, proj.OutputPath, runtimeIdentifier);
			var publishDirectory = Path.Combine (packageDirectory, "publish");
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

			var resolvedPackagePublishItems = ReadApplicationArtifactItems (Path.Combine (Root, projBuilder.ProjectDirectory, "resolved-package-publish-items.txt"));
			if (!isRelease) {
				Assert.AreEqual (2, resolvedPackagePublishItems.Count, $"Actual items:{Environment.NewLine}{FormatApplicationArtifactItems (resolvedPackagePublishItems)}");
				AssertResolvedPackagePublishItem (resolvedPackagePublishItems, Path.Combine (packageDirectory, $"{proj.PackageName}.apk"), $"{proj.PackageName}.apk");
				AssertResolvedPackagePublishItem (resolvedPackagePublishItems, Path.Combine (packageDirectory, $"{proj.PackageName}-Signed.apk"), $"{proj.PackageName}-Signed.apk");
			} else {
				Assert.AreEqual (3, resolvedPackagePublishItems.Count, $"Actual items:{Environment.NewLine}{FormatApplicationArtifactItems (resolvedPackagePublishItems)}");
				AssertResolvedPackagePublishItem (resolvedPackagePublishItems, Path.Combine (packageDirectory, $"{proj.PackageName}.aab"), $"{proj.PackageName}.aab");
				AssertResolvedPackagePublishItem (resolvedPackagePublishItems, Path.Combine (packageDirectory, $"{proj.PackageName}-Signed.aab"), $"{proj.PackageName}-Signed.aab");
				AssertResolvedPackagePublishItem (resolvedPackagePublishItems, Path.Combine (packageDirectory, $"{proj.PackageName}-Signed.apk"), $"{proj.PackageName}-Signed.apk");
			}

			var applicationArtifactItems = ReadApplicationArtifactItems (Path.Combine (Root, projBuilder.ProjectDirectory, "application-artifact-items.txt"));
			if (!isRelease) {
				Assert.AreEqual (2, applicationArtifactItems.Count, $"Actual items:{Environment.NewLine}{FormatApplicationArtifactItems (applicationArtifactItems)}");
				AssertApplicationArtifactItem (applicationArtifactItems, Path.Combine (publishDirectory, $"{proj.PackageName}.apk"), $"{proj.PackageName}.apk", "apk", "false", proj.PackageName);
				AssertApplicationArtifactItem (applicationArtifactItems, Path.Combine (publishDirectory, $"{proj.PackageName}-Signed.apk"), $"{proj.PackageName}-Signed.apk", "apk", "true", proj.PackageName);
			} else {
				Assert.AreEqual (3, applicationArtifactItems.Count, $"Actual items:{Environment.NewLine}{FormatApplicationArtifactItems (applicationArtifactItems)}");
				AssertApplicationArtifactItem (applicationArtifactItems, Path.Combine (publishDirectory, $"{proj.PackageName}.aab"), $"{proj.PackageName}.aab", "aab", "false", proj.PackageName);
				AssertApplicationArtifactItem (applicationArtifactItems, Path.Combine (publishDirectory, $"{proj.PackageName}-Signed.aab"), $"{proj.PackageName}-Signed.aab", "aab", "true", proj.PackageName);
				AssertApplicationArtifactItem (applicationArtifactItems, Path.Combine (publishDirectory, $"{proj.PackageName}-Signed.apk"), $"{proj.PackageName}-Signed.apk", "apk", "true", proj.PackageName);
			}

			Assert.IsTrue (dotnet.Build (target: "WritePublishReturnedApplicationArtifactItems", parameters: configParam), "`dotnet build -t:WritePublishReturnedApplicationArtifactItems` should succeed");
			var publishReturnedApplicationArtifactItems = ReadApplicationArtifactItems (Path.Combine (Root, projBuilder.ProjectDirectory, "publish-returned-application-artifact-items.txt"));
			if (!isRelease) {
				Assert.AreEqual (2, publishReturnedApplicationArtifactItems.Count, $"Actual items:{Environment.NewLine}{FormatApplicationArtifactItems (publishReturnedApplicationArtifactItems)}");
				AssertApplicationArtifactItem (publishReturnedApplicationArtifactItems, Path.Combine (publishDirectory, $"{proj.PackageName}.apk"), $"{proj.PackageName}.apk", "apk", "false", proj.PackageName);
				AssertApplicationArtifactItem (publishReturnedApplicationArtifactItems, Path.Combine (publishDirectory, $"{proj.PackageName}-Signed.apk"), $"{proj.PackageName}-Signed.apk", "apk", "true", proj.PackageName);
			} else {
				Assert.AreEqual (3, publishReturnedApplicationArtifactItems.Count, $"Actual items:{Environment.NewLine}{FormatApplicationArtifactItems (publishReturnedApplicationArtifactItems)}");
				AssertApplicationArtifactItem (publishReturnedApplicationArtifactItems, Path.Combine (publishDirectory, $"{proj.PackageName}.aab"), $"{proj.PackageName}.aab", "aab", "false", proj.PackageName);
				AssertApplicationArtifactItem (publishReturnedApplicationArtifactItems, Path.Combine (publishDirectory, $"{proj.PackageName}-Signed.aab"), $"{proj.PackageName}-Signed.aab", "aab", "true", proj.PackageName);
				AssertApplicationArtifactItem (publishReturnedApplicationArtifactItems, Path.Combine (publishDirectory, $"{proj.PackageName}-Signed.apk"), $"{proj.PackageName}-Signed.apk", "apk", "true", proj.PackageName);
			}

			Assert.IsTrue (dotnet.Build (target: "GetApplicationArtifacts", parameters: configParam), "`dotnet build -t:GetApplicationArtifacts` should succeed");
			var observedApplicationArtifactItems = ReadApplicationArtifactItems (Path.Combine (Root, projBuilder.ProjectDirectory, "observed-application-artifact-items.txt"));
			if (!isRelease) {
				Assert.AreEqual (2, observedApplicationArtifactItems.Count, $"Actual items:{Environment.NewLine}{FormatApplicationArtifactItems (observedApplicationArtifactItems)}");
				AssertApplicationArtifactItem (observedApplicationArtifactItems, Path.Combine (packageDirectory, $"{proj.PackageName}.apk"), $"{proj.PackageName}.apk", "apk", "false", proj.PackageName);
				AssertApplicationArtifactItem (observedApplicationArtifactItems, Path.Combine (packageDirectory, $"{proj.PackageName}-Signed.apk"), $"{proj.PackageName}-Signed.apk", "apk", "true", proj.PackageName);
			} else {
				Assert.AreEqual (3, observedApplicationArtifactItems.Count, $"Actual items:{Environment.NewLine}{FormatApplicationArtifactItems (observedApplicationArtifactItems)}");
				AssertApplicationArtifactItem (observedApplicationArtifactItems, Path.Combine (packageDirectory, $"{proj.PackageName}.aab"), $"{proj.PackageName}.aab", "aab", "false", proj.PackageName);
				AssertApplicationArtifactItem (observedApplicationArtifactItems, Path.Combine (packageDirectory, $"{proj.PackageName}-Signed.aab"), $"{proj.PackageName}-Signed.aab", "aab", "true", proj.PackageName);
				AssertApplicationArtifactItem (observedApplicationArtifactItems, Path.Combine (packageDirectory, $"{proj.PackageName}-Signed.apk"), $"{proj.PackageName}-Signed.apk", "apk", "true", proj.PackageName);
			}

			var queriedApplicationArtifactItems = ReadApplicationArtifactItems (Path.Combine (Root, projBuilder.ProjectDirectory, "queried-application-artifact-items.txt"));
			if (!isRelease) {
				Assert.AreEqual (2, queriedApplicationArtifactItems.Count, $"Actual items:{Environment.NewLine}{FormatApplicationArtifactItems (queriedApplicationArtifactItems)}");
				AssertQueriedApplicationArtifactItem (queriedApplicationArtifactItems, Path.Combine (packageDirectory, $"{proj.PackageName}.apk"), $"{proj.PackageName}.apk", "apk", "false", proj.PackageName, "true");
				AssertQueriedApplicationArtifactItem (queriedApplicationArtifactItems, Path.Combine (packageDirectory, $"{proj.PackageName}-Signed.apk"), $"{proj.PackageName}-Signed.apk", "apk", "true", proj.PackageName, "true");
			} else {
				Assert.AreEqual (3, queriedApplicationArtifactItems.Count, $"Actual items:{Environment.NewLine}{FormatApplicationArtifactItems (queriedApplicationArtifactItems)}");
				AssertQueriedApplicationArtifactItem (queriedApplicationArtifactItems, Path.Combine (packageDirectory, $"{proj.PackageName}.aab"), $"{proj.PackageName}.aab", "aab", "false", proj.PackageName, "true");
				AssertQueriedApplicationArtifactItem (queriedApplicationArtifactItems, Path.Combine (packageDirectory, $"{proj.PackageName}-Signed.aab"), $"{proj.PackageName}-Signed.aab", "aab", "true", proj.PackageName, "true");
				AssertQueriedApplicationArtifactItem (queriedApplicationArtifactItems, Path.Combine (packageDirectory, $"{proj.PackageName}-Signed.apk"), $"{proj.PackageName}-Signed.apk", "apk", "true", proj.PackageName, "true");
			}
		}

		static List<string []> ReadApplicationArtifactItems (string path)
		{
			FileAssert.Exists (path);
			return File.ReadAllLines (path)
				.Where (line => line.Length > 0)
				.Select (line => line.Split ('|'))
				.ToList ();
		}

		static void AssertApplicationArtifactItem (List<string []> items, string fullPath, string fileName, string packageFormat, string signed, string packageId)
		{
			var matches = items.Where (item =>
				item.Length == 5 &&
				item [0] == fullPath &&
				item [1] == fileName &&
				item [2] == packageFormat &&
				item [3] == signed &&
				item [4] == packageId).ToList ();
			Assert.AreEqual (1, matches.Count, $"Expected application artifact item '{fullPath}|{fileName}|{packageFormat}|{signed}|{packageId}'. Actual items:{Environment.NewLine}{FormatApplicationArtifactItems (items)}");
		}

		static void AssertQueriedApplicationArtifactItem (List<string []> items, string fullPath, string fileName, string packageFormat, string signed, string packageId, string mauiArtifact)
		{
			var matches = items.Where (item =>
				item.Length == 6 &&
				item [0] == fullPath &&
				item [1] == fileName &&
				item [2] == packageFormat &&
				item [3] == signed &&
				item [4] == packageId &&
				item [5] == mauiArtifact).ToList ();
			Assert.AreEqual (1, matches.Count, $"Expected queried application artifact item '{fullPath}|{fileName}|{packageFormat}|{signed}|{packageId}|{mauiArtifact}'. Actual items:{Environment.NewLine}{FormatApplicationArtifactItems (items)}");
		}

		static void AssertResolvedPackagePublishItem (List<string []> items, string fullPath, string relativePath)
		{
			var matches = items.Where (item =>
				item.Length == 2 &&
				item [0] == fullPath &&
				item [1] == relativePath).ToList ();
			Assert.AreEqual (1, matches.Count, $"Expected resolved package publish item '{fullPath}|{relativePath}'. Actual items:{Environment.NewLine}{FormatApplicationArtifactItems (items)}");
		}

		static string FormatApplicationArtifactItems (List<string []> items)
		{
			return string.Join (Environment.NewLine, items.Select (item => string.Join ("|", item)));
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
			Assert.IsTrue (builder.Build (library), $"{library.ProjectName} should succeed");
			// NOTE: Preview API levels emit XA4211
			if (!preview) {
				builder.AssertHasNoWarnings ();
			}
		}
	}
}
