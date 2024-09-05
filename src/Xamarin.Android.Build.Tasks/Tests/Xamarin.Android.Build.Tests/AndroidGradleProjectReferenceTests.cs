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
	public class AndroidGradleProjectReferenceTests : BaseTest
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
					new BuildItem (KnownProperties.AndroidGradleProjectReference, gradleProject.ProjectDirectory) {
						Metadata = {
							{ "ModuleName", moduleName },
							{ "Configuration", "Release" },
						},
					},
				},
			};

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
			FileAssert.Exists (Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath, "gradle", "outputs", "apk", "release", $"{moduleName}-release-unsigned.apk"));
		}

		static readonly object [] AGPMetadataTestSources = new object [] {
			new object [] {
				/* Bind */                       true,
				/* Configuration */              "Release",
				/* ReferenceLibraryOutputs */    true,
			},
			new object [] {
				/* Bind */                       true,
				/* Configuration */              "Debug",
				/* ReferenceLibraryOutputs */    true,
			},
			new object [] {
				/* Bind */                       false,
				/* Configuration */              "Release",
				/* ReferenceLibraryOutputs */    true,
			},
			new object [] {
				/* Bind */                       false,
				/* Configuration */              "Debug",
				/* ReferenceLibraryOutputs */    false,
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
					new BuildItem (KnownProperties.AndroidGradleProjectReference, gradleProject.ProjectDirectory) {
						Metadata = {
							{ "ModuleName", moduleName },
							{ "Bind", bind.ToString ()},
							{ "Configuration", configuration },
							{ "ReferenceLibraryOutputs", refOutputs.ToString () },
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
			} else {
				Assert.IsFalse (buildResult, "Build should have failed.");
			}
			if (bind) {
				Assert.IsFalse (builder.Output.IsTargetSkipped ("GenerateBindings"), "The 'GenerateBindings' target should run when Bind=true");
			} else {
				Assert.IsTrue (builder.Output.IsTargetSkipped ("GenerateBindings"), "The 'GenerateBindings' target should not run when Bind=false");
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
					new BuildItem (KnownProperties.AndroidGradleProjectReference, gradleProject.ProjectDirectory) {
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

			var nupkgPath = Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath, $"{proj.ProjectName}.1.0.0.nupkg");
			FileAssert.Exists (nupkgPath);
			using var nupkg = ZipHelper.OpenZip (nupkgPath);
			nupkg.AssertContainsEntry (nupkgPath, $"lib/{dotnetVersion}-android{apiLevel}.0/{proj.ProjectName}.dll");
			nupkg.AssertContainsEntry (nupkgPath, $"lib/{dotnetVersion}-android{apiLevel}.0/{proj.ProjectName}.aar");
			if (packGradleRef) {
				// TODO Fix nupkg inclusion
				//nupkg.AssertContainsEntry (nupkgPath, $"lib/{dotnetVersion}-android{apiLevel}.0/{moduleName}-release.aar");
			} else {
				nupkg.AssertDoesNotContainEntry (nupkgPath, $"lib/{dotnetVersion}-android{apiLevel}.0/{moduleName}-release.aar");
			}
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
					new BuildItem (KnownProperties.AndroidGradleProjectReference, gradleProject.ProjectDirectory) {
						Metadata = {
							{ "ModuleName", "TestAppModule" },
							{ "Configuration", "Debug" },
						},
					},
					new BuildItem (KnownProperties.AndroidGradleProjectReference, gradleProject.ProjectDirectory) {
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
					new BuildItem (KnownProperties.AndroidGradleProjectReference, gradleProject.ProjectDirectory) {
						Metadata = {
							{ "ModuleName", gradleProject.Modules.First ().Name },
							{ "Configuration", "Debug" },
						},
					},
					new BuildItem (KnownProperties.AndroidGradleProjectReference, gradleProject2.ProjectDirectory) {
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
					new BuildItem (KnownProperties.AndroidGradleProjectReference, invalidProjectPath),
				},
			};

			using var builder = CreateDllBuilder ();
			builder.ThrowOnBuildFailure = false;
			Assert.IsFalse (builder.Build (proj), "Build should have failed.");
			StringAssertEx.Contains ("error XAGRDL1000", builder.LastBuildOutput);
			StringAssertEx.Contains ($"Executable 'gradlew' not found in project directory '{invalidProjectPath}'. Please ensure the path to your Gradle project folder is correct", builder.LastBuildOutput);
		}

		[Test]
		public void InvalidModuleNameError ()
		{
			var gradleProject = AndroidGradleProject.CreateDefault (GradleTestProjectDir);
			var invalidModuleName = "Invalid";
			var proj = new XamarinAndroidLibraryProject {
				OtherBuildItems = {
					 new BuildItem (KnownProperties.AndroidGradleProjectReference, gradleProject.ProjectDirectory) {
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
