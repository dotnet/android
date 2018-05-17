using System;
using NUnit.Framework;
using Xamarin.ProjectTools;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using System.Text;
using System.Xml.Linq;

namespace Xamarin.Android.Build.Tests
{
	[Parallelizable (ParallelScope.Children)]
	public class IncrementalBuildTest : BaseTest
	{
		[Test]
		public void CheckResourceDirectoryDoesNotGetHosed ()
		{
			// do a release build
			// change one of the properties (say AotAssemblies) 
			// do another build. it should NOT hose the resource directory.
			var path = Path.Combine ("temp", TestName);
			var proj = new XamarinAndroidApplicationProject () {
				ProjectName = "App1",
				IsRelease = true,
			};
			using (var b = CreateApkBuilder (path, false, false)) {
				Assert.IsTrue(b.Build (proj), "First should have succeeded");
				Assert.IsFalse (
					b.Output.IsTargetSkipped ("_GenerateAndroidResourceDir"),
					"the _GenerateAndroidResourceDir target should not be skipped");
				File.SetLastWriteTimeUtc (Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "build.props"),
					DateTime.UtcNow);
				Assert.IsTrue(b.Build (proj), "Second should have succeeded");
				Assert.IsFalse (
					b.Output.IsTargetSkipped ("_GenerateAndroidResourceDir"),
					"the _GenerateAndroidResourceDir target should not be skipped");
			}
		}

		[Test]
		public void IncrementalCleanDuringClean ()
		{
			var path = Path.Combine ("temp", TestName);
			var proj = new XamarinAndroidApplicationProject () {
				ProjectName = "App1",
				IsRelease = true,
			};
			proj.SetProperty ("AndroidUseManagedDesignTimeResourceGenerator", "True");
			proj.SetProperty ("BuildingInsideVisualStudio", "True");
			using (var b = CreateApkBuilder (path)) {
				b.Target = "Compile";
				Assert.IsTrue(b.Build (proj), "DesignTime Build should have succeeded");
				var designTimeDesigner = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "designtime", "Resource.designer.cs");
				FileAssert.Exists (designTimeDesigner, $"{designTimeDesigner} should have been created.");
				b.Target = "Build";
				Assert.IsTrue(b.Build (proj), "Build should have succeeded");
				FileAssert.Exists (designTimeDesigner, $"{designTimeDesigner} should still exist after Build.");
				b.Target = "Clean";
				Assert.IsTrue(b.Build (proj), "Clean should have succeeded");
				FileAssert.Exists (designTimeDesigner, $"{designTimeDesigner} should still exist after Clean.");
				b.Target = "Compile";
				Assert.IsTrue(b.Build (proj), "Build should have succeeded");
				FileAssert.Exists (designTimeDesigner, $"{designTimeDesigner} should still exist after Compile.");
				b.Target = "Build";
				Assert.IsTrue(b.Build (proj), "Build should have succeeded");
				FileAssert.Exists (designTimeDesigner, $"{designTimeDesigner} should still exist after second Build.");
				Assert.IsTrue(b.Build (proj), "Build should have succeeded");
				FileAssert.Exists (designTimeDesigner, $"{designTimeDesigner} should still exist after third Build.");
				b.Target = "Compile";
				Assert.IsTrue(b.Build (proj), "Build should have succeeded");
				FileAssert.Exists (designTimeDesigner, $"{designTimeDesigner} should still exist after second Compile.");
				b.Target = "Clean";
				Assert.IsTrue(b.Build (proj), "Clean should have succeeded");
				FileAssert.Exists (designTimeDesigner, $"{designTimeDesigner} should still exist after second Clean.");
				b.Target = "ReBuild";
				Assert.IsTrue(b.Build (proj), "ReBuild should have succeeded");
				FileAssert.Exists (designTimeDesigner, $"{designTimeDesigner} should still exist after ReBuild.");
			}

		}

		[Test]
		public void LibraryIncrementalBuild () {

			var testPath = Path.Combine ("temp", TestName);
			var class1Source = new BuildItem.Source ("Class1.cs") {
				TextContent = () => @"
using System;
namespace Lib
{
	public class Class1
	{
		public Class1 ()
		{
		}
	}
}"
			};
			var lib = new XamarinAndroidLibraryProject () {
				ProjectName = "Lib",
				ProjectGuid = Guid.NewGuid ().ToString (),
				Sources = {
					class1Source,
				},
			};
			using (var b = CreateDllBuilder (Path.Combine (testPath, "Lib"))) {
				Assert.IsTrue (b.Build (lib), "Build should have succeeded.");
				Assert.IsTrue (b.LastBuildOutput.ContainsText ("LogicalName=__AndroidLibraryProjects__.zip") ||
						b.LastBuildOutput.ContainsText ("Lib.obj.Debug.__AndroidLibraryProjects__.zip,__AndroidLibraryProjects__.zip"),
						"The LogicalName for __AndroidLibraryProjects__.zip should be set.");
				class1Source.Timestamp = DateTime.UtcNow.Add (TimeSpan.FromMinutes (1));
				Assert.IsTrue (b.Build (lib), "Build should have succeeded.");
				Assert.IsTrue (b.LastBuildOutput.ContainsText ("LogicalName=__AndroidLibraryProjects__.zip") ||
						b.LastBuildOutput.ContainsText ("Lib.obj.Debug.__AndroidLibraryProjects__.zip,__AndroidLibraryProjects__.zip"),
						"The LogicalName for __AndroidLibraryProjects__.zip should be set.");
			}
		}

		[Test]
		public void AllProjectsHaveSameOutputDirectory()
		{
			var testPath = Path.Combine ("temp", "AllProjectsHaveSameOutputDirectory");
			var sb = new SolutionBuilder("AllProjectsHaveSameOutputDirectory.sln") {
				SolutionPath = Path.Combine (Root, testPath),
				Verbosity = LoggerVerbosity.Diagnostic,
			};

			var app1 = new XamarinAndroidApplicationProject () {
				ProjectName = "App1",
				OutputPath = Path.Combine("..","bin","Debug"),
			};
			sb.Projects.Add (app1);
			var app2 = new XamarinAndroidApplicationProject () {
				ProjectName = "App2",
				OutputPath = Path.Combine("..","bin","Debug"),
			};
			sb.Projects.Add (app2);
			Assert.IsTrue (sb.Build (), "Build of solution should have succeeded");
			Assert.IsTrue (sb.ReBuild (), "ReBuild of solution should have succeeded");
			sb.Dispose ();
		}

		[Test]
		public void JavacTaskDoesNotRunOnSecondBuild ()
		{
			var app = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				ProjectName = "App",
				OtherBuildItems = {
					new AndroidItem.AndroidJavaSource ("TestMe.java") {
						TextContent = () => @"package com.android.test;
public class TestMe {
	public TestMe createTestMe () {
		return new TestMe ();
	}
}",
						Encoding = Encoding.ASCII,
						Timestamp = DateTimeOffset.Now,
					}
				},
			};

			using (var b = CreateApkBuilder (Path.Combine ("temp", "JavacTaskDoesNotRunOnSecondBuild"), false, false)) {
				Assert.IsTrue (b.Build (app), "First build should have succeeded");
				Assert.IsFalse (
					b.Output.IsTargetSkipped ("_CompileJava"),
					"the _CompileJava target should not be skipped");
				Assert.IsFalse (
					b.Output.IsTargetSkipped ("_BuildApkEmbed"),
					"the _BuildApkEmbed target should not be skipped");
				var expectedOutput = Path.Combine (Root, b.ProjectDirectory, app.IntermediateOutputPath, "android", "bin", "classes", 
					"com", "android", "test", "TestMe.class");
				Assert.IsTrue (File.Exists (expectedOutput), string.Format ("{0} should exist.", expectedOutput));
				Assert.IsTrue (b.Build (app), "Second build should have succeeded");
				Assert.IsTrue (
					b.Output.IsTargetSkipped ("_CompileJava"),
					"the _CompileJava target should be skipped");
				Assert.IsTrue (
					b.Output.IsTargetSkipped ("_BuildApkEmbed"),
					"the _BuildApkEmbed target should be skipped");
				Assert.IsTrue (File.Exists (expectedOutput), string.Format ("{0} should exist.", expectedOutput));
			}
		}

		[Test]
		public void ResolveNativeLibrariesInManagedReferences ([Values(true, false)] bool useShortFileNames)
		{
			var lib = new XamarinAndroidLibraryProject () {
				ProjectName = "Lib",
				ProjectGuid = Guid.NewGuid ().ToString (),
				OtherBuildItems = {
					new BuildItem (AndroidBuildActions.EmbeddedNativeLibrary, "libs/armeabi-v7a/libfoo.so") {
						TextContent = () => string.Empty,
						Encoding = Encoding.ASCII,
						Timestamp = DateTimeOffset.Now,
					}
				},
				Sources = {
					new BuildItem.Source ("Class1.cs") {
						TextContent= () => @"
using System;
namespace Lib
{
	public class Class1
	{
		public Class1 ()
		{
		}
	}
}"
					},
				},
			};
			lib.SetProperty (lib.ActiveConfigurationProperties, "UseShortFileNames", useShortFileNames);
			var so = lib.OtherBuildItems.First (x => x.Include () == "libs/armeabi-v7a/libfoo.so");

			var lib2 = new XamarinAndroidLibraryProject () {
				ProjectName = "Lib2",
				ProjectGuid = Guid.NewGuid ().ToString (),
				OtherBuildItems = {
					new BuildItem (AndroidBuildActions.EmbeddedNativeLibrary, "libs/armeabi-v7a/libfoo2.so") {
						TextContent = () => string.Empty,
						Encoding = Encoding.ASCII,
						Timestamp = DateTimeOffset.Now,
					},
					new BuildItem.ProjectReference (@"..\Lib\Lib.csproj", "Lib", lib.ProjectGuid) {
					}
				},
				Sources = {
					new BuildItem.Source ("Class2.cs") {
						TextContent= () => @"
using System;
namespace Lib2
{
	public class Class2
	{
		public Class2 ()
		{
			var c = new Lib.Class1 ();
		}
	}
}"
					},
				},
			};
			lib2.SetProperty (lib.ActiveConfigurationProperties, "UseShortFileNames", useShortFileNames);
			var path = Path.Combine (Root, "temp", $"ResolveNativeLibrariesInManagedReferences_{useShortFileNames}");
			using (var libbuilder = CreateDllBuilder (Path.Combine(path, "Lib"))) {

				Assert.IsTrue (libbuilder.Build (lib), "lib 1st. build failed");

				using (var libbuilder2 = CreateDllBuilder (Path.Combine (path, "Lib2"))) {

					Assert.IsTrue (libbuilder2.Build (lib2), "lib 1st. build failed");

					var app = new XamarinAndroidApplicationProject () { ProjectName = "App",
						OtherBuildItems = {
							new BuildItem.ProjectReference (@"..\Lib2\Lib2.csproj", "Lib2", lib2.ProjectGuid),
						}
					};
					app.SetProperty (app.ActiveConfigurationProperties, "UseShortFileNames", useShortFileNames);
					using (var builder = CreateApkBuilder (Path.Combine (path, "App"))) {
						builder.Verbosity = LoggerVerbosity.Diagnostic;
						Assert.IsTrue (builder.Build (app), "app 1st. build failed");

						var libfoo = ZipHelper.ReadFileFromZip (Path.Combine (Root, builder.ProjectDirectory, app.OutputPath, app.PackageName + "-Signed.apk"),
							"lib/armeabi-v7a/libfoo.so");
						Assert.IsNotNull (libfoo, "libfoo.so should exist in the .apk");

						so.TextContent = () => "newValue";
						so.Timestamp = DateTimeOffset.Now;
						Assert.IsTrue (libbuilder.Build (lib), "lib 2nd. build failed");
						Assert.IsTrue (libbuilder2.Build (lib2), "lib 2nd. build failed");
						Assert.IsTrue (builder.Build (app), "app 2nd. build failed");

						Assert.IsNotNull (libfoo, "libfoo.so should exist in the .apk");

						Assert.AreEqual (so.TextContent ().Length, new FileInfo (Path.Combine (Root, libbuilder.ProjectDirectory, lib.IntermediateOutputPath,
							useShortFileNames ? "nl" : "native_library_imports", "armeabi-v7a", "libfoo.so")).Length,
							"intermediate size mismatch");
						libfoo = ZipHelper.ReadFileFromZip (Path.Combine (Root, builder.ProjectDirectory, app.OutputPath, app.PackageName + "-Signed.apk"),
							"lib/armeabi-v7a/libfoo.so");
						Assert.AreEqual (so.TextContent ().Length, libfoo.Length, "compressed size mismatch");
						var libfoo2 = ZipHelper.ReadFileFromZip (Path.Combine (Root, builder.ProjectDirectory, app.OutputPath, app.PackageName + "-Signed.apk"),
							"lib/armeabi-v7a/libfoo2.so");
						Assert.IsNotNull (libfoo2, "libfoo2.so should exist in the .apk");
						Directory.Delete (path, recursive: true);
					}
				}
			}
		}
	}
}
