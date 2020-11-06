using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;
using Xamarin.Tools.Zip;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[NonParallelizable] // On MacOS, parallel /restore causes issues
	[Category ("Node-2"), Category ("DotNetIgnore")] // These don't need to run under `dotnet test`
	public class XASdkTests : BaseTest
	{
		/// <summary>
		/// The full path to the project directory
		/// </summary>
		public string FullProjectDirectory { get; set; }

		static readonly object [] DotNetBuildLibrarySource = new object [] {
			new object [] {
				/* isRelease */    false,
				/* duplicateAar */ false,
			},
			new object [] {
				/* isRelease */    false,
				/* duplicateAar */ true,
			},
			new object [] {
				/* isRelease */    true,
				/* duplicateAar */ false,
			},
		};

		[Test]
		[Category ("SmokeTests")]
		[TestCaseSource (nameof (DotNetBuildLibrarySource))]
		public void DotNetBuildLibrary (bool isRelease, bool duplicateAar)
		{
			var path = Path.Combine ("temp", TestName);
			var env_var = "MY_ENVIRONMENT_VAR";
			var env_val = "MY_VALUE";

			// Setup dependencies App A -> Lib B -> Lib C

			var libC = new XASdkProject (outputType: "Library") {
				ProjectName = "LibraryC",
				IsRelease = isRelease,
				Sources = {
					new BuildItem.Source ("Bar.cs") {
						TextContent = () => "public class Bar { }",
					}
				}
			};
			libC.OtherBuildItems.Add (new AndroidItem.AndroidAsset ("Assets\\bar\\bar.txt") {
				BinaryContent = () => Array.Empty<byte> (),
			});
			var activity = libC.Sources.FirstOrDefault (s => s.Include () == "MainActivity.cs");
			if (activity != null)
				libC.Sources.Remove (activity);
			var libCBuilder = CreateDotNetBuilder (libC, Path.Combine (path, libC.ProjectName));
			Assert.IsTrue (libCBuilder.Build (), $"{libC.ProjectName} should succeed");

			var libB = new XASdkProject (outputType: "Library") {
				ProjectName = "LibraryB",
				IsRelease = isRelease,
				Sources = {
					new BuildItem.Source ("Foo.cs") {
						TextContent = () => "public class Foo : Bar { }",
					}
				}
			};
			libB.OtherBuildItems.Add (new AndroidItem.AndroidAsset ("Assets\\foo\\foo.txt") {
				BinaryContent = () => Array.Empty<byte> (),
			});
			libB.OtherBuildItems.Add (new AndroidItem.AndroidResource ("Resources\\layout\\MyLayout.axml") {
				TextContent = () => "<?xml version=\"1.0\" encoding=\"utf-8\" ?><LinearLayout xmlns:android=\"http://schemas.android.com/apk/res/android\" />"
			});
			libB.OtherBuildItems.Add (new AndroidItem.AndroidResource ("Resources\\raw\\bar.txt") {
				BinaryContent = () => Array.Empty<byte> (),
			});
			libB.OtherBuildItems.Add (new AndroidItem.AndroidEnvironment ("env.txt") {
				TextContent = () => $"{env_var}={env_val}",
			});
			libB.OtherBuildItems.Add (new AndroidItem.AndroidEnvironment ("sub\\directory\\env.txt") {
				TextContent = () => $"{env_var}={env_val}",
			});
			libB.OtherBuildItems.Add (new AndroidItem.AndroidLibrary ("sub\\directory\\foo.jar") {
				BinaryContent = () => Convert.FromBase64String (InlineData.JavaClassesJarBase64),
			});
			libB.OtherBuildItems.Add (new AndroidItem.AndroidLibrary ("sub\\directory\\arm64-v8a\\libfoo.so") {
				BinaryContent = () => Array.Empty<byte> (),
			});
			libB.OtherBuildItems.Add (new AndroidItem.AndroidLibrary ("libfoo.so") {
				MetadataValues = "Link=x86\\libfoo.so",
				BinaryContent = () => Array.Empty<byte> (),
			});
			libB.AddReference (libC);

			activity = libB.Sources.FirstOrDefault (s => s.Include () == "MainActivity.cs");
			if (activity != null)
				libB.Sources.Remove (activity);
			var libBBuilder = CreateDotNetBuilder (libB, Path.Combine (path, libB.ProjectName));
			Assert.IsTrue (libBBuilder.Build (), $"{libB.ProjectName} should succeed");

			// Check .aar file for class library
			var aarPath = Path.Combine (FullProjectDirectory, libB.OutputPath, $"{libB.ProjectName}.aar");
			FileAssert.Exists (aarPath);
			using (var aar = ZipHelper.OpenZip (aarPath)) {
				aar.AssertContainsEntry (aarPath, "assets/foo/foo.txt");
				aar.AssertContainsEntry (aarPath, "res/layout/mylayout.xml");
				aar.AssertContainsEntry (aarPath, "res/raw/bar.txt");
				aar.AssertContainsEntry (aarPath, ".net/__res_name_case_map.txt");
				aar.AssertContainsEntry (aarPath, ".net/env/190E30B3D205731E.env");
				aar.AssertContainsEntry (aarPath, ".net/env/2CBDAB7FEEA94B19.env");
				aar.AssertContainsEntry (aarPath, "libs/A1AFA985571E728E.jar");
				aar.AssertContainsEntry (aarPath, "jni/arm64-v8a/libfoo.so");
				aar.AssertContainsEntry (aarPath, "jni/x86/libfoo.so");
			}

			// Check EmbeddedResource files do not exist
			var assemblyPath = Path.Combine (FullProjectDirectory, libB.OutputPath, $"{libB.ProjectName}.dll");
			FileAssert.Exists (assemblyPath);
			using (var assembly = AssemblyDefinition.ReadAssembly (assemblyPath)) {
				Assert.AreEqual (0, assembly.MainModule.Resources.Count);
			}

			var appA = new XASdkProject {
				ProjectName = "AppA",
				IsRelease = isRelease,
				Sources = {
					new BuildItem.Source ("Bar.cs") {
						TextContent = () => "public class Bar : Foo { }",
					}
				}
			};
			appA.AddReference (libB);
			if (duplicateAar) {
				// Test a duplicate @(AndroidLibrary) item with the same path of LibraryB.aar
				appA.OtherBuildItems.Add (new AndroidItem.AndroidLibrary (aarPath));
			}
			var appBuilder = CreateDotNetBuilder (appA, Path.Combine (path, appA.ProjectName));
			Assert.IsTrue (appBuilder.Build (), $"{appA.ProjectName} should succeed");

			// Check .apk for assets, res, and native libraries
			var apkPath = Path.Combine (FullProjectDirectory, appA.OutputPath, $"{appA.PackageName}.apk");
			FileAssert.Exists (apkPath);
			using (var apk = ZipHelper.OpenZip (apkPath)) {
				apk.AssertContainsEntry (apkPath, "assets/foo/foo.txt");
				apk.AssertContainsEntry (apkPath, "assets/bar/bar.txt");
				apk.AssertContainsEntry (aarPath, "res/layout/mylayout.xml");
				apk.AssertContainsEntry (apkPath, "res/raw/bar.txt");
				apk.AssertContainsEntry (apkPath, "lib/arm64-v8a/libfoo.so");
				apk.AssertContainsEntry (apkPath, "lib/x86/libfoo.so");
			}

			// Check classes.dex contains foo.jar
			var intermediate = Path.Combine (FullProjectDirectory, appA.IntermediateOutputPath);
			var dexFile = Path.Combine (intermediate, "android", "bin", "classes.dex");
			FileAssert.Exists (dexFile);
			string className = "Lcom/xamarin/android/test/msbuildtest/JavaSourceJarTest;";
			Assert.IsTrue (DexUtils.ContainsClass (className, dexFile, AndroidSdkPath), $"`{dexFile}` should include `{className}`!");

			// Check environment variable
			var environmentFiles = EnvironmentHelper.GatherEnvironmentFiles (intermediate, "x86", required: true);
			var environmentVariables = EnvironmentHelper.ReadEnvironmentVariables (environmentFiles);
			Assert.IsTrue (environmentVariables.TryGetValue (env_var, out string actual), $"Environment should contain {env_var}");
			Assert.AreEqual (env_val, actual, $"{env_var} should be {env_val}");

			// Check Resource.designer.cs
			var resource_designer_cs = Path.Combine (intermediate, "Resource.designer.cs");
			FileAssert.Exists (resource_designer_cs);
			var resource_designer_text = File.ReadAllText (resource_designer_cs);
			StringAssert.Contains ("public const int MyLayout", resource_designer_text);
		}

		[Test]
		public void DotNetPack ([Values ("net5.0-android", "net5.0-android30")] string targetFramework)
		{
			var proj = new XASdkProject (outputType: "Library") {
				TargetFramework = targetFramework,
				IsRelease = true,
				Sources = {
					new BuildItem.Source ("Foo.cs") {
						TextContent = () => "public class Foo { }",
					}
				}
			};
			proj.OtherBuildItems.Add (new AndroidItem.AndroidResource ("Resources\\raw\\bar.txt") {
				BinaryContent = () => Array.Empty<byte> (),
			});
			proj.OtherBuildItems.Add (new AndroidItem.AndroidLibrary ("sub\\directory\\foo.jar") {
				BinaryContent = () => Convert.FromBase64String (InlineData.JavaClassesJarBase64),
			});
			proj.OtherBuildItems.Add (new AndroidItem.AndroidLibrary ("sub\\directory\\arm64-v8a\\libfoo.so") {
				BinaryContent = () => Array.Empty<byte> (),
			});
			proj.OtherBuildItems.Add (new AndroidItem.AndroidLibrary ("libfoo.so") {
				MetadataValues = "Link=x86\\libfoo.so",
				BinaryContent = () => Array.Empty<byte> (),
			});

			var dotnet = CreateDotNetBuilder (proj);
			Assert.IsTrue (dotnet.Pack (), "`dotnet pack` should succeed");

			var nupkgPath = Path.Combine (FullProjectDirectory, proj.OutputPath, "..", $"{proj.ProjectName}.1.0.0.nupkg");
			FileAssert.Exists (nupkgPath);
			using (var nupkg = ZipHelper.OpenZip (nupkgPath)) {
				nupkg.AssertContainsEntry (nupkgPath, $"lib/net5.0-android30.0/{proj.ProjectName}.dll");
				nupkg.AssertContainsEntry (nupkgPath, $"lib/net5.0-android30.0/{proj.ProjectName}.aar");
			}
		}

		[Test]
		public void DotNetLibraryAarChanges ()
		{
			var proj = new XASdkProject (outputType: "Library");
			proj.Sources.Add (new AndroidItem.AndroidResource ("Resources\\raw\\foo.txt") {
				TextContent = () => "foo",
			});
			proj.Sources.Add (new AndroidItem.AndroidResource ("Resources\\raw\\bar.txt") {
				TextContent = () => "bar",
			});

			var dotnet = CreateDotNetBuilder (proj);
			Assert.IsTrue (dotnet.Build (), "first build should succeed");
			var aarPath = Path.Combine (FullProjectDirectory, proj.OutputPath, $"{proj.ProjectName}.aar");
			FileAssert.Exists (aarPath);
			using (var aar = ZipHelper.OpenZip (aarPath)) {
				aar.AssertEntryContents (aarPath, "res/raw/foo.txt", contents: "foo");
				aar.AssertEntryContents (aarPath, "res/raw/bar.txt", contents: "bar");
			}

			// Change res/raw/bar.txt contents
			var bar_txt = Path.Combine (FullProjectDirectory, "Resources", "raw", "bar.txt");
			File.WriteAllText (bar_txt, contents: "baz");
			Assert.IsTrue (dotnet.Build (), "second build should succeed");
			FileAssert.Exists (aarPath);
			using (var aar = ZipHelper.OpenZip (aarPath)) {
				aar.AssertEntryContents (aarPath, "res/raw/foo.txt", contents: "foo");
				aar.AssertEntryContents (aarPath, "res/raw/bar.txt", contents: "baz");
			}

			// Delete res/raw/bar.txt
			File.Delete (bar_txt);
			Assert.IsTrue (dotnet.Build (), "third build should succeed");
			FileAssert.Exists (aarPath);
			using (var aar = ZipHelper.OpenZip (aarPath)) {
				aar.AssertEntryContents (aarPath, "res/raw/foo.txt", contents: "foo");
				aar.AssertDoesNotContainEntry (aarPath, "res/raw/bar.txt");
			}
		}

		[Test]
		[Category ("SmokeTests")]
		public void DotNetBuildBinding ()
		{
			var proj = new XASdkProject (outputType: "Library");
			proj.OtherBuildItems.Add (new AndroidItem.AndroidLibrary ("javaclasses.jar") {
				MetadataValues = "Bind=true",
				BinaryContent = () => Convert.FromBase64String (InlineData.JavaClassesJarBase64)
			});
			// TODO: bring back when Xamarin.Android.Bindings.Documentation.targets is working
			//proj.OtherBuildItems.Add (new BuildItem ("JavaSourceJar", "javasources.jar") {
			//	BinaryContent = () => Convert.FromBase64String (InlineData.JavaSourcesJarBase64)
			//});
			var dotnet = CreateDotNetBuilder (proj);
			Assert.IsTrue (dotnet.Build (), "`dotnet build` should succeed");

			var assemblyPath = Path.Combine (FullProjectDirectory, proj.OutputPath, "UnnamedProject.dll");
			FileAssert.Exists (assemblyPath);
			using (var assembly = AssemblyDefinition.ReadAssembly (assemblyPath)) {
				var typeName = "Com.Xamarin.Android.Test.Msbuildtest.JavaSourceJarTest";
				var type = assembly.MainModule.GetType (typeName);
				Assert.IsNotNull (type, $"{assemblyPath} should contain {typeName}");
			}
		}

		static readonly object [] DotNetBuildSource = new object [] {
			new object [] {
				/* runtimeIdentifiers */ "android.21-arm",
				/* isRelease */          false,
			},
			new object [] {
				/* runtimeIdentifiers */ "android.21-arm64",
				/* isRelease */          false,
			},
			new object [] {
				/* runtimeIdentifiers */ "android.21-x86",
				/* isRelease */          false,
			},
			new object [] {
				/* runtimeIdentifiers */ "android.21-x64",
				/* isRelease */          false,
			},
			new object [] {
				/* runtimeIdentifiers */ "android.21-arm",
				/* isRelease */          true,
			},
			new object [] {
				/* runtimeIdentifiers */ "android.21-arm;android.21-arm64;android.21-x86;android.21-x64",
				/* isRelease */          false,
			},
			new object [] {
				/* runtimeIdentifiers */ "android.21-arm;android.21-arm64;android.21-x86",
				/* isRelease */          true,
			},
			new object [] {
				/* runtimeIdentifiers */ "android.21-arm;android.21-arm64;android.21-x86;android.21-x64",
				/* isRelease */          true,
			},
		};

		[Test]
		[Category ("SmokeTests")]
		[TestCaseSource (nameof (DotNetBuildSource))]
		public void DotNetBuild (string runtimeIdentifiers, bool isRelease)
		{
			var proj = new XASdkProject {
				IsRelease = isRelease
			};
			proj.OtherBuildItems.Add (new AndroidItem.InputJar ("javaclasses.jar") {
				BinaryContent = () => Convert.FromBase64String (InlineData.JavaClassesJarBase64)
			});
			// TODO: bring back when Xamarin.Android.Bindings.Documentation.targets is working
			//proj.OtherBuildItems.Add (new BuildItem ("JavaSourceJar", "javasources.jar") {
			//	BinaryContent = () => Convert.FromBase64String (InlineData.JavaSourcesJarBase64)
			//});
			if (!runtimeIdentifiers.Contains (";")) {
				proj.SetProperty (KnownProperties.RuntimeIdentifier, runtimeIdentifiers);
			} else {
				proj.SetProperty (KnownProperties.RuntimeIdentifiers, runtimeIdentifiers);
			}

			var dotnet = CreateDotNetBuilder (proj);
			Assert.IsTrue (dotnet.Build (), "`dotnet build` should succeed");

			// TODO: run for release once illink warnings are gone
			// context: https://github.com/xamarin/xamarin-android/issues/4708
			if (!isRelease)
				Assert.IsTrue (StringAssertEx.ContainsText (dotnet.LastBuildOutput, " 0 Warning(s)"), "Should have no MSBuild warnings.");

			var outputPath = Path.Combine (FullProjectDirectory, proj.OutputPath);
			if (!runtimeIdentifiers.Contains (";")) {
				outputPath = Path.Combine (outputPath, runtimeIdentifiers);
			}

			// TODO: With workloads we don't control the import of Microsoft.NET.Sdk/Sdk.targets.
			//  We can no longer change the default values of `$(GenerateDependencyFile)` and `$(ProduceReferenceAssembly)` as a result.
			//  We should update Microsoft.NET.Sdk to default both of these properties to false when the `$(TargetPlatformIdentifier)` is "mobile" (Android, iOS, etc).
			//  Alternatively, the workload concept could be updated to support some sort of `Before.Microsoft.NET.targets` hook.

			/* var files = Directory.EnumerateFileSystemEntries (outputPath)
				.Select (Path.GetFileName)
				.OrderBy (f => f);
			CollectionAssert.AreEqual (new [] {
				$"{proj.ProjectName}.dll",
				$"{proj.ProjectName}.pdb",
				$"{proj.PackageName}.apk",
				$"{proj.PackageName}-Signed.apk",
			}, files);
			*/

			var assemblyPath = Path.Combine (outputPath, $"{proj.ProjectName}.dll");
			FileAssert.Exists (assemblyPath);
			using (var assembly = AssemblyDefinition.ReadAssembly (assemblyPath)) {
				var typeName = "Com.Xamarin.Android.Test.Msbuildtest.JavaSourceJarTest";
				var type = assembly.MainModule.GetType (typeName);
				Assert.IsNotNull (type, $"{assemblyPath} should contain {typeName}");
			}

			bool expectEmbeddedAssembies = !(CommercialBuildAvailable && !isRelease);
			var apkPath = Path.Combine (outputPath, "UnnamedProject.UnnamedProject.apk");
			FileAssert.Exists (apkPath);
			using (var apk = ZipHelper.OpenZip (apkPath)) {
				apk.AssertContainsEntry (apkPath, $"assemblies/{proj.ProjectName}.dll", shouldContainEntry: expectEmbeddedAssembies);
				apk.AssertContainsEntry (apkPath, $"assemblies/{proj.ProjectName}.pdb", shouldContainEntry: !CommercialBuildAvailable && !isRelease);
				apk.AssertContainsEntry (apkPath, $"assemblies/System.Linq.dll",        shouldContainEntry: expectEmbeddedAssembies);
				var rids = runtimeIdentifiers.Split (';');
				foreach (var abi in rids.Select (MonoAndroidHelper.RuntimeIdentifierToAbi)) {
					apk.AssertContainsEntry (apkPath, $"lib/{abi}/libmonodroid.so");
					apk.AssertContainsEntry (apkPath, $"lib/{abi}/libmonosgen-2.0.so");
					if (rids.Length > 1) {
						apk.AssertContainsEntry (apkPath, $"assemblies/{abi}/System.Private.CoreLib.dll",        shouldContainEntry: expectEmbeddedAssembies);
						apk.AssertContainsEntry (apkPath, $"assemblies/{abi}/System.Collections.Concurrent.dll", shouldContainEntry: expectEmbeddedAssembies);
					} else {
						apk.AssertContainsEntry (apkPath, "assemblies/System.Private.CoreLib.dll",        shouldContainEntry: expectEmbeddedAssembies);
						apk.AssertContainsEntry (apkPath, "assemblies/System.Collections.Concurrent.dll", shouldContainEntry: expectEmbeddedAssembies);
					}
				}
			}
		}

		[Test]
		[Category ("SmokeTests")]
		public void DotNetBuildXamarinForms ()
		{
			var proj = new XamarinFormsXASdkProject ();
			var dotnet = CreateDotNetBuilder (proj);
			Assert.IsTrue (dotnet.Build (), "`dotnet build` should succeed");
			Assert.IsTrue (StringAssertEx.ContainsText (dotnet.LastBuildOutput, " 0 Warning(s)"), "Should have no MSBuild warnings.");
		}

		[Test]
		public void DotNetPublish ([Values (false, true)] bool isRelease)
		{
			const string runtimeIdentifier = "android.21-arm";
			var proj = new XASdkProject {
				IsRelease = isRelease
			};
			proj.SetProperty (KnownProperties.RuntimeIdentifier, runtimeIdentifier);
			var dotnet = CreateDotNetBuilder (proj);
			Assert.IsTrue (dotnet.Publish (), "first `dotnet publish` should succeed");

			var publishDirectory = Path.Combine (FullProjectDirectory, proj.OutputPath, runtimeIdentifier, "publish");
			var apk = Path.Combine (publishDirectory, $"{proj.PackageName}.apk");
			var apkSigned = Path.Combine (publishDirectory, $"{proj.PackageName}-Signed.apk");
			FileAssert.Exists (apk);
			FileAssert.Exists (apkSigned);

			Assert.IsTrue (dotnet.Publish (parameters: new [] { "AndroidPackageFormat=aab" }), $"second `dotnet publish` should succeed");
			var aab = Path.Combine (publishDirectory, $"{proj.PackageName}.aab");
			var aabSigned = Path.Combine (publishDirectory, $"{proj.PackageName}-Signed.aab");
			FileAssert.DoesNotExist (apk);
			FileAssert.DoesNotExist (apkSigned);
			FileAssert.Exists (aab);
			FileAssert.Exists (aabSigned);
		}

		[Test]
		public void DefaultItems ()
		{
			void CreateEmptyFile (params string [] paths)
			{
				var path = Path.Combine (FullProjectDirectory, Path.Combine (paths));
				Directory.CreateDirectory (Path.GetDirectoryName (path));
				File.WriteAllText (path, contents: "");
			}

			var proj = new XASdkProject ();
			var dotnet = CreateDotNetBuilder (proj);

			// Build error -> no nested sub-directories in Resources
			CreateEmptyFile ("Resources", "drawable", "foo", "bar.png");
			CreateEmptyFile ("Resources", "raw", "foo", "bar.png");

			// Build error -> no files/directories that start with .
			CreateEmptyFile ("Resources", "raw", ".DS_Store");
			CreateEmptyFile ("Assets", ".DS_Store");
			CreateEmptyFile ("Assets", ".svn", "foo.txt");

			// Files that should work
			CreateEmptyFile ("Resources", "raw", "foo.txt");
			CreateEmptyFile ("Assets", "foo", "bar.txt");

			Assert.IsTrue (dotnet.Build (), "`dotnet build` should succeed");

			var apkPath = Path.Combine (FullProjectDirectory, proj.OutputPath, $"{proj.PackageName}.apk");
			FileAssert.Exists (apkPath);
			using (var apk = ZipHelper.OpenZip (apkPath)) {
				apk.AssertContainsEntry (apkPath, "res/raw/foo.txt");
				apk.AssertContainsEntry (apkPath, "assets/foo/bar.txt");
			}
		}

		DotNetCLI CreateDotNetBuilder (XASdkProject project, string relativeProjectDir = null)
		{
			if (string.IsNullOrEmpty (relativeProjectDir)) {
				relativeProjectDir = Path.Combine ("temp", TestName);
			}
			TestOutputDirectories [TestContext.CurrentContext.Test.ID] =
				FullProjectDirectory = Path.Combine (Root, relativeProjectDir);
			var files = project.Save ();
			project.Populate (relativeProjectDir, files);
			project.CopyNuGetConfig (relativeProjectDir);
			return new DotNetCLI (project, Path.Combine (FullProjectDirectory, project.ProjectFilePath));
		}
	}
}
