using System;
using System.IO;
using System.Linq;

using Microsoft.Build.Framework;

using NUnit.Framework;

using Xamarin.Android.Tools;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class AndroidGradleProjectTests : BaseTest
	{
		string GradleTestProjectDir = string.Empty;

		[SetUp]
		public void GradleTestSetUp ()
		{
			GradleTestProjectDir = Path.Combine (Root, "temp", "gradle", TestName);
			if (Directory.Exists (GradleTestProjectDir))
				Directory.Delete (GradleTestProjectDir, recursive: true);
		}

		[TearDown]
		public void GradleTestTearDown ()
		{
			if (TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Passed ||
				TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Skipped) {
				try {
					if (Directory.Exists (GradleTestProjectDir))
						Directory.Delete (GradleTestProjectDir, recursive: true);
				} catch (Exception ex) {
					// This happens on CI occasionally, let's not fail the test
					TestContext.Out.WriteLine ($"Failed to delete '{GradleTestProjectDir}': {ex}");
				}
			}
		}

		[Test]
		public void BuildApp ()
		{
			var gradleProject = AndroidGradleProject.CreateDefault (GradleTestProjectDir, isApplication: true);
			var moduleName = gradleProject.Modules.First ().Name;

			var proj = new XamarinAndroidApplicationProject {
				OtherBuildItems = {
					new BuildItem (KnownProperties.AndroidGradleProject, gradleProject.BuildFilePath) {
						Metadata = {
							{ "ModuleName", moduleName },
							{ "Configuration", "Release" },
						},
					},
				},
			};

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
			FileAssert.Exists (Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath, $"{moduleName}-release-unsigned.apk"));
		}

		static readonly object [] AGPMetadataTestSources = new object [] {
			new object [] {
				/* Bind */                       true,
				/* Configuration */              "Release",
				/* CreateAndroidLibrary */       true,
			},
			new object [] {
				/* Bind */                       true,
				/* Configuration */              "Debug",
				/* CreateAndroidLibrary */       true,
			},
			new object [] {
				/* Bind */                       false,
				/* Configuration */              "Release",
				/* CreateAndroidLibrary */       true,
			},
			new object [] {
				/* Bind */                       true,
				/* Configuration */              "Debug",
				/* CreateAndroidLibrary */       false,
			},
		};
		[Test]
		[TestCaseSource (nameof (AGPMetadataTestSources))]
		public void BindLibrary (bool bind, string configuration, bool refOutputs)
		{
			var gradleProject = AndroidGradleProject.CreateDefault (GradleTestProjectDir);
			var moduleName = gradleProject.Modules.First ().Name;

			var proj = new XamarinAndroidBindingProject {
				Jars = {
					new BuildItem (KnownProperties.AndroidGradleProject, gradleProject.BuildFilePath) {
						Metadata = {
							{ "ModuleName", moduleName },
							{ "Bind", bind.ToString ()},
							{ "Configuration", configuration },
							{ "CreateAndroidLibrary", refOutputs.ToString () },
						},
					},
				},
				Sources = {
					new BuildItem.Source ("Foo.cs") {
						TextContent = () => @$"public class Foo {{ public Foo () {{ System.Console.WriteLine (GradleTest.{moduleName}Class.GetString(""TestString"")); }} }}"
					},
				},
				MetadataXml = $@"<metadata><attr path=""/api/package[@name='{gradleProject.Modules.First ().PackageName}']"" name=""managedName"">GradleTest</attr></metadata>",
			};

			using var builder = CreateDllBuilder ();
			builder.Verbosity = LoggerVerbosity.Detailed;
			builder.ThrowOnBuildFailure = false;
			var buildResult = builder.Build (proj);

			if (refOutputs && bind) {
				Assert.IsTrue (buildResult, "Build should have succeeded.");
				FileAssert.Exists (Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath, $"{moduleName}-{configuration}.aar"));
				Assert.IsFalse (builder.Output.IsTargetSkipped ("GenerateBindings"), "The 'GenerateBindings' target should run when Bind=true");
			} else {
				Assert.IsFalse (buildResult, "Build should have failed.");
			}
		}

		[Test]
		public void BindPackLibrary ([Values (false, true)] bool packGradleRef)
		{
			var dotnetVersion = "net9.0";
			var apiLevel = XABuildConfig.AndroidDefaultTargetDotnetApiLevel;
			var gradleProject = AndroidGradleProject.CreateDefault (GradleTestProjectDir);
			var moduleName = gradleProject.Modules.First ().Name;

			var proj = new XamarinAndroidLibraryProject {
				IsRelease = true,
				EnableDefaultItems = true,
				OtherBuildItems = {
					new BuildItem (KnownProperties.AndroidGradleProject, gradleProject.BuildFilePath) {
						Metadata = {
							{ "ModuleName", moduleName },
							{ "Pack", packGradleRef.ToString () },
						},
					},
				}
			};

			using var builder = CreateDllBuilder ();
			builder.Save (proj);

			var dotnet = new DotNetCLI (Path.Combine (Root, builder.ProjectDirectory, proj.ProjectFilePath));
			Assert.IsTrue (dotnet.Pack (parameters: new [] { "Configuration=Release" }), "`dotnet pack` should succeed");
			FileAssert.Exists (Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath, $"{moduleName}-Release.aar"));

			var nupkgPath = Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath, $"{proj.ProjectName}.1.0.0.nupkg");
			FileAssert.Exists (nupkgPath);
			using var nupkg = ZipHelper.OpenZip (nupkgPath);
			nupkg.AssertContainsEntry (nupkgPath, $"lib/{dotnetVersion}-android{apiLevel}.0/{proj.ProjectName}.dll");
			nupkg.AssertContainsEntry (nupkgPath, $"lib/{dotnetVersion}-android{apiLevel}.0/{proj.ProjectName}.aar");
			if (packGradleRef) {
				nupkg.AssertContainsEntry (nupkgPath, $"lib/{dotnetVersion}-android{apiLevel}.0/{moduleName}-release.aar");
			} else {
				nupkg.AssertDoesNotContainEntry (nupkgPath, $"lib/{dotnetVersion}-android{apiLevel}.0/{moduleName}-release.aar");
			}
		}

		[Test]
		public void BuildIncremental ()
		{
			var gradleProject = AndroidGradleProject.CreateDefault (GradleTestProjectDir);
			var gradleModule = gradleProject.Modules.First ();

			var proj = new XamarinAndroidLibraryProject {
				OtherBuildItems = {
					new BuildItem (KnownProperties.AndroidGradleProject, gradleProject.BuildFilePath) {
						Metadata = {
							{ "ModuleName", gradleModule.Name },
						},
					},
				},
			};

			using var builder = CreateDllBuilder ();
			builder.Verbosity = LoggerVerbosity.Detailed;
			Assert.IsTrue (builder.Build (proj), "First build should have succeeded.");
			var outputAar = Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath, $"{gradleModule.Name}-release.aar");
			FileAssert.Exists (outputAar);
			var outputAarFirstWriteTime = File.GetLastWriteTime (outputAar);
			var packagedManifestContent = System.Text.Encoding.UTF8.GetString (ZipHelper.ReadFileFromZip (outputAar, "AndroidManifest.xml"));
			StringAssert.Contains (@"uses-sdk android:minSdkVersion=""21""", packagedManifestContent);

			// Build again, _BuildAndroidGradleProjects should be skipped
			builder.BuildLogFile = "build2.log";
			Assert.IsTrue (builder.Build (proj), "Second build should have succeeded.");
			Assert.IsTrue (builder.Output.IsTargetSkipped ("_BuildAndroidGradleProjects"), "The '_BuildAndroidGradleProjects' target should be skipped on incremental build");
			FileAssert.Exists (outputAar);
			var outputAarSecondWriteTime = File.GetLastWriteTime (outputAar);
			Assert.IsTrue (outputAarFirstWriteTime == outputAarSecondWriteTime, $"Expected {outputAar} write time to be '{outputAarFirstWriteTime}', but was '{outputAarSecondWriteTime}'");

			// Update gradle project, _BuildAndroidGradleProjects should run and outputs should be updated
			builder.BuildLogFile = "build3.log";
			gradleModule.MinSdk = 30;
			gradleModule.WriteGradleBuildFile ();
			Assert.IsTrue (builder.Build (proj), "Third build should have succeeded.");
			Assert.IsFalse (builder.Output.IsTargetSkipped ("_BuildAndroidGradleProjects"), "The '_BuildAndroidGradleProjects' target should run on partial rebuild");
			FileAssert.Exists (outputAar);
			var outputAarThirdWriteTime = File.GetLastWriteTime (outputAar);
			Assert.IsTrue (outputAarThirdWriteTime > outputAarFirstWriteTime, $"Expected '{outputAar}' write time of '{outputAarThirdWriteTime}' to be greater than first write '{outputAarFirstWriteTime}'");
			packagedManifestContent = System.Text.Encoding.UTF8.GetString (ZipHelper.ReadFileFromZip (outputAar, "AndroidManifest.xml"));
			StringAssert.Contains (@"uses-sdk android:minSdkVersion=""30""", packagedManifestContent);
		}

		[Test]
		public void BuildMultipleModules ()
		{
			var gradleProject = new AndroidGradleProject (GradleTestProjectDir) {
				Modules = {
					new AndroidGradleModule (Path.Combine (GradleTestProjectDir, "TestAppModule")) {
						IsApplication = true,
					},
					new AndroidGradleModule (Path.Combine (GradleTestProjectDir, "TestLibModule")),
				},
			};
			gradleProject.Create ();

			var proj = new XamarinAndroidLibraryProject {
				OtherBuildItems = {
					new BuildItem (KnownProperties.AndroidGradleProject, gradleProject.BuildFilePath) {
						Metadata = {
							{ "ModuleName", "TestAppModule" },
							{ "Configuration", "Debug" },
						},
					},
					new BuildItem (KnownProperties.AndroidGradleProject, gradleProject.BuildFilePath) {
						Metadata = {
							{ "ModuleName", "TestLibModule" },
							{ "Configuration", "Release" },
						},
					},
				},
			};

			using var builder = CreateDllBuilder ();
			Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
			FileAssert.Exists (Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath, "TestLibModule-release.aar"));
		}

		[Test]
		public void BuildMultipleLibraries ()
		{
			var gradleProject = new AndroidGradleProject (Path.Combine (GradleTestProjectDir, "First")) {
				Modules = {
					new AndroidGradleModule (Path.Combine (GradleTestProjectDir, "First", "FirstModule")),
				},
			};
			gradleProject.Create ();
			var gradleProject2 = new AndroidGradleProject (Path.Combine (GradleTestProjectDir, "Second")) {
				Modules = {
					new AndroidGradleModule (Path.Combine (GradleTestProjectDir, "Second", "SecondModule")),
				},
			};
			gradleProject2.Create ();

			var proj = new XamarinAndroidLibraryProject {
				OtherBuildItems = {
					new BuildItem (KnownProperties.AndroidGradleProject, gradleProject.BuildFilePath) {
						Metadata = {
							{ "ModuleName", gradleProject.Modules.First ().Name },
							{ "Configuration", "Debug" },
						},
					},
					new BuildItem (KnownProperties.AndroidGradleProject, gradleProject2.BuildFilePath) {
						Metadata = {
							{ "ModuleName", gradleProject2.Modules.First ().Name },
							{ "Configuration", "Release" },
						},
					},
				},
			};

			using var builder = CreateDllBuilder ();
			Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
			FileAssert.Exists (Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath, "FirstModule-debug.aar"));
			FileAssert.Exists (Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath, "SecondModule-release.aar"));
		}

		[Test]
		public void InvalidItemRefError ()
		{
			var invalidProjectPath = Path.Combine (Root, "doesnotexist");
			var proj = new XamarinAndroidLibraryProject {
				OtherBuildItems = {
					new BuildItem (KnownProperties.AndroidGradleProject, Path.Combine (invalidProjectPath, "build.gradle.kts")),
				},
			};

			using var builder = CreateDllBuilder ();
			builder.ThrowOnBuildFailure = false;
			Assert.IsFalse (builder.Build (proj), "Build should have failed.");
			StringAssertEx.Contains ("error XAGRDL1000", builder.LastBuildOutput);
			StringAssertEx.Contains ($"Executable 'gradlew' not found in project directory '{invalidProjectPath}{Path.DirectorySeparatorChar}'. Please ensure the path to your Gradle project folder is correct", builder.LastBuildOutput);
		}

		[Test]
		public void InvalidModuleNameError ()
		{
			var gradleProject = AndroidGradleProject.CreateDefault (GradleTestProjectDir);
			var invalidModuleName = "Invalid";
			var proj = new XamarinAndroidLibraryProject {
				OtherBuildItems = {
					 new BuildItem (KnownProperties.AndroidGradleProject, gradleProject.BuildFilePath) {
						Metadata = {
							{ "ModuleName", invalidModuleName },
						},
					 },
				 },
			};

			using var builder = CreateDllBuilder ();
			builder.ThrowOnBuildFailure = false;
			Assert.IsFalse (builder.Build (proj), "Build should have failed.");
			StringAssertEx.Contains ("error XAGRDL0000", builder.LastBuildOutput);
			StringAssertEx.Contains ($"'{invalidModuleName}' not found in root project '{TestName}'", builder.LastBuildOutput);
		}

	}
}
