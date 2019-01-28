using Microsoft.Build.Framework;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[Parallelizable (ParallelScope.Children)]
	public class IncrementalBuildTest : BaseTest
	{
		[Test]
		public void CheckNothingIsDeletedByIncrementalClean ([Values (true, false)] bool enableMultiDex, [Values (true, false)] bool useAapt2)
		{
			var path = Path.Combine ("temp", TestName);
			var proj = new XamarinFormsAndroidApplicationProject () {
				ProjectName = "App1",
				IsRelease = true,
			};
			if (enableMultiDex)
				proj.SetProperty ("AndroidEnableMultiDex", "True");
			if (useAapt2)
				proj.SetProperty ("AndroidUseAapt2", "True");
			using (var b = CreateApkBuilder (path)) {
				//To be sure we are at a clean state
				var projectDir = Path.Combine (Root, b.ProjectDirectory);
				if (Directory.Exists (projectDir))
					Directory.Delete (projectDir, true);

				Assert.IsTrue (b.Build (proj), "First should have succeeded" );
				var intermediate = Path.Combine (projectDir, proj.IntermediateOutputPath);
				var output = Path.Combine (projectDir, proj.OutputPath);
				var fileWrites = Path.Combine (intermediate, $"{proj.ProjectName}.csproj.FileListAbsolute.txt");
				FileAssert.Exists (fileWrites);
				var expected = File.ReadAllText (fileWrites);
				var files = Directory.EnumerateFiles (intermediate, "*", SearchOption.AllDirectories).ToList ();
				files.AddRange (Directory.EnumerateFiles (output, "*", SearchOption.AllDirectories));

				//Touch a few files, do an incremental build
				var filesToTouch = new [] {
 					Path.Combine (intermediate, "build.props"),
 					Path.Combine (intermediate, $"{proj.ProjectName}.pdb"),
 				};
				foreach (var file in filesToTouch) {
					FileAssert.Exists (file);
					File.SetLastWriteTimeUtc (file, DateTime.UtcNow);
					File.SetLastAccessTimeUtc (file, DateTime.UtcNow);
				}
				Assert.IsTrue (b.Build (proj, doNotCleanupOnUpdate: true, saveProject: false), "Second should have succeeded");

				//No changes
				Assert.IsTrue (b.Build (proj, doNotCleanupOnUpdate: true, saveProject: false), "Third should have succeeded");
				Assert.IsFalse (b.Output.IsTargetSkipped ("IncrementalClean"), "`IncrementalClean` should have run!");
				foreach (var file in files) {
					FileAssert.Exists (file, $"{file} should not have been deleted!" );
				}
				FileAssert.Exists (fileWrites);
				var actual = File.ReadAllText (fileWrites);
				Assert.AreEqual (expected, actual, $"`{fileWrites}` has changes!");
			}
		}

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
				class1Source.Timestamp = DateTimeOffset.UtcNow.Add (TimeSpan.FromMinutes (1));
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
				IsRelease = true,
				ProjectGuid = Guid.NewGuid ().ToString (),
				OtherBuildItems = {
					new BuildItem (AndroidBuildActions.EmbeddedNativeLibrary, "libs/armeabi-v7a/libfoo.so") {
						TextContent = () => string.Empty,
						Encoding = Encoding.ASCII,
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
				IsRelease = true,
				OtherBuildItems = {
					new BuildItem (AndroidBuildActions.EmbeddedNativeLibrary, "libs/armeabi-v7a/libfoo2.so") {
						TextContent = () => string.Empty,
						Encoding = Encoding.ASCII,
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
						IsRelease = true,
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
						so.Timestamp = DateTimeOffset.UtcNow;
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

		//https://github.com/xamarin/xamarin-android/issues/2247
		[Test]
		public void AppProjectTargetsDoNotBreak ()
		{
			var targets = new List<string> {
				"_CopyIntermediateAssemblies",
				"_GeneratePackageManagerJava",
				"_ResolveLibraryProjectImports",
				"_BuildAdditionalResourcesCache",
				"_CleanIntermediateIfNuGetsChange",
				"_CopyConfigFiles",
				"_CopyPdbFiles",
				"_CopyMdbFiles",
			};
			var proj = new XamarinFormsAndroidApplicationProject {
				OtherBuildItems = {
					new BuildItem.NoActionResource ("UnnamedProject.dll.config") {
						TextContent = () => "<?xml version='1.0' ?><configuration/>",
						Metadata = {
							{ "CopyToOutputDirectory", "PreserveNewest"},
						}
					}
				}
			};
			if (IsWindows) {
				//NOTE: pdb2mdb will run on Windows on the current project's symbols if DebugType=Full
				proj.SetProperty (proj.DebugProperties, "DebugType", "Full");
				targets.Add ("_ConvertPdbFiles");
			}
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				Assert.IsTrue (b.Build (proj), "first build should succeed");
				foreach (var target in targets) {
					Assert.IsFalse (b.Output.IsTargetSkipped (target), $"`{target}` should *not* be skipped!");
				}

				var output = Path.Combine (Root, b.ProjectDirectory, proj.OutputPath);
				var intermediate = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath);
				var filesToTouch = new [] {
					Path.Combine (intermediate, "..", "project.assets.json"),
					Path.Combine (intermediate, "build.props"),
					Path.Combine (intermediate, $"{proj.ProjectName}.dll"),
					Path.Combine (intermediate, $"{proj.ProjectName}.pdb"),
					Path.Combine (intermediate, "android", "assets", $"{proj.ProjectName}.dll"),
					Path.Combine (output, $"{proj.ProjectName}.dll.config"),
				};
				foreach (var file in filesToTouch) {
					FileAssert.Exists (file);
					File.SetLastWriteTimeUtc (file, DateTime.UtcNow);
					File.SetLastAccessTimeUtc (file, DateTime.UtcNow);
				}

				//NOTE: second build, targets will run because inputs changed
				Assert.IsTrue (b.Build (proj, doNotCleanupOnUpdate: true, saveProject: false), "second build should succeed");
				foreach (var target in targets) {
					Assert.IsFalse (b.Output.IsTargetSkipped (target), $"`{target}` should *not* be skipped on second build!");
				}

				//NOTE: third build, targets should certainly *not* run! there are no changes
				Assert.IsTrue (b.Build (proj, doNotCleanupOnUpdate: true, saveProject: false), "third build should succeed");
				foreach (var target in targets) {
					Assert.IsTrue (b.Output.IsTargetSkipped (target), $"`{target}` should be skipped on third build!");
				}
			}
		}

		[Test]
		public void LibraryProjectTargetsDoNotBreak ()
		{
			var targets = new [] {
				"_CreateNativeLibraryArchive",
				"_CreateManagedLibraryResourceArchive",
			};
			var proj = new XamarinAndroidLibraryProject {
				Sources = {
					new BuildItem.Source ("Class1.cs") {
						TextContent= () => "public class Class1 { }"
					},
				},
				OtherBuildItems = {
					new AndroidItem.EmbeddedNativeLibrary ("foo\\armeabi-v7a\\libtest.so") {
						BinaryContent = () => new byte [10],
						MetadataValues = "Link=libs\\armeabi-v7a\\libtest.so",
					},
					new AndroidItem.EmbeddedNativeLibrary ("foo\\x86\\libtest.so") {
						BinaryContent = () => new byte [10],
						MetadataValues = "Link=libs\\x86\\libtest.so",
					},
					new AndroidItem.AndroidAsset ("Assets\\foo.txt") {
						TextContent =  () => "bar",
					},
				},
			};
			using (var b = CreateDllBuilder (Path.Combine ("temp", TestName))) {
				Assert.IsTrue (b.Build (proj), "first build should succeed");
				foreach (var target in targets) {
					Assert.IsFalse (b.Output.IsTargetSkipped (target), $"`{target}` should *not* be skipped!");
				}

				var intermediate = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath);
				var filesToTouch = new [] {
					Path.Combine (Root, b.ProjectDirectory, "foo", "armeabi-v7a", "libtest.so"),
					Path.Combine (Root, b.ProjectDirectory, "Assets", "foo.txt"),
				};
				foreach (var file in filesToTouch) {
					FileAssert.Exists (file);
					File.SetLastWriteTimeUtc (file, DateTime.UtcNow);
					File.SetLastAccessTimeUtc (file, DateTime.UtcNow);
				}

				//NOTE: second build, targets will run because inputs changed
				Assert.IsTrue (b.Build (proj, doNotCleanupOnUpdate: true, saveProject: false), "second build should succeed");
				foreach (var target in targets) {
					Assert.IsFalse (b.Output.IsTargetSkipped (target), $"`{target}` should *not* be skipped on second build!");
				}

				//NOTE: third build, targets should certainly *not* run! there are no changes
				Assert.IsTrue (b.Build (proj, doNotCleanupOnUpdate: true, saveProject: false), "third build should succeed");
				foreach (var target in targets) {
					Assert.IsTrue (b.Output.IsTargetSkipped (target), $"`{target}` should be skipped on third build!");
				}
			}
		}

		[Test]
		public void ProduceReferenceAssembly ()
		{
			var path = Path.Combine ("temp", TestName);
			var app = new XamarinAndroidApplicationProject {
				ProjectName = "MyApp",
				Sources = {
					new BuildItem.Source ("Foo.cs") {
						TextContent = () => "public class Foo : Bar { }"
					},
				}
			};
			//NOTE: so _BuildApkEmbed runs in commercial tests
			app.SetProperty ("EmbedAssembliesIntoApk", true.ToString ());
			app.SetProperty ("AndroidUseSharedRuntime", false.ToString ());

			int count = 0;
			var lib = new XamarinAndroidLibraryProject {
				ProjectName = "MyLibrary",
				Sources = {
					new BuildItem.Source ("Bar.cs") {
						TextContent = () => "public class Bar { public Bar () { System.Console.WriteLine (" + count++ + "); } }"
					},
				}
			};
			lib.SetProperty ("ProduceReferenceAssembly", "True");
			app.References.Add (new BuildItem.ProjectReference ($"..\\{lib.ProjectName}\\{lib.ProjectName}.csproj", lib.ProjectName, lib.ProjectGuid));

			using (var libBuilder = CreateDllBuilder (Path.Combine (path, lib.ProjectName), false))
			using (var appBuilder = CreateApkBuilder (Path.Combine (path, app.ProjectName))) {
				Assert.IsTrue (libBuilder.Build (lib), "first library build should have succeeded.");
				Assert.IsTrue (appBuilder.Build (app), "first app build should have succeeded.");

				lib.Touch ("Bar.cs");

				Assert.IsTrue (libBuilder.Build (lib, doNotCleanupOnUpdate: true, saveProject: false), "second library build should have succeeded.");
				Assert.IsTrue (appBuilder.Build (app, doNotCleanupOnUpdate: true, saveProject: false), "second app build should have succeeded.");

				var targetsShouldSkip = new [] {
					//TODO: perhaps more targets will skip here eventually?
					"CoreCompile",
				};
				foreach (var target in targetsShouldSkip) {
					Assert.IsTrue (appBuilder.Output.IsTargetSkipped (target), $"`{target}` should be skipped!");
				}

				var targetsShouldRun = new [] {
					"_BuildApkEmbed",
					"_CopyPackage",
					"_Sign",
				};
				foreach (var target in targetsShouldRun) {
					Assert.IsFalse (appBuilder.Output.IsTargetSkipped (target), $"`{target}` should *not* be skipped!");
				}
			}
		}
	}
}
