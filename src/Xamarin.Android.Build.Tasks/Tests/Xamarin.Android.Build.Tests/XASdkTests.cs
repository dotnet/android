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
	[Category ("Node-2"), Category ("DotNetIgnore")] // These don't need to run under `--params dotnet=true`
	public class XASdkTests : BaseTest
	{
		/// <summary>
		/// The full path to the project directory
		/// </summary>
		public string FullProjectDirectory { get; set; }

		[Test]
		[Category ("SmokeTests")]
		public void DotNetBuildLibrary ([Values (false, true)] bool isRelease)
		{
			var proj = new XASdkProject (outputType: "Library") {
				IsRelease = isRelease
			};
			var dotnet = CreateDotNetBuilder (proj);
			Assert.IsTrue (dotnet.Build (), "`dotnet build` should succeed");

			var assemblyPath = Path.Combine (Root, dotnet.ProjectDirectory, proj.OutputPath, "UnnamedProject.dll");
			FileAssert.Exists (assemblyPath);
			using (var assembly = AssemblyDefinition.ReadAssembly (assemblyPath)) {
				var resourceName = "__AndroidLibraryProjects__.zip";
				var resource = assembly.MainModule.Resources.OfType<EmbeddedResource> ().FirstOrDefault (r => r.Name == resourceName);
				Assert.IsNotNull (resource, $"{assemblyPath} should contain a {resourceName} EmbeddedResource");
				using (var zip = ZipArchive.Open (resource.GetResourceStream ())) {
					var entry = "library_project_imports/res/values/strings.xml";
					Assert.IsTrue (zip.ContainsEntry (entry), $"{resourceName} should contain {entry}");
				}
			}
		}

		[Test]
		[Category ("SmokeTests")]
		public void DotNetBuildBinding ()
		{
			var proj = new XASdkProject (outputType: "Library");
			proj.OtherBuildItems.Add (new AndroidItem.EmbeddedJar ("javaclasses.jar") {
				BinaryContent = () => Convert.FromBase64String (InlineData.JavaClassesJarBase64)
			});
			// TODO: bring back when Xamarin.Android.Bindings.Documentation.targets is working
			//proj.OtherBuildItems.Add (new BuildItem ("JavaSourceJar", "javasources.jar") {
			//	BinaryContent = () => Convert.FromBase64String (InlineData.JavaSourcesJarBase64)
			//});
			var dotnet = CreateDotNetBuilder (proj);
			Assert.IsTrue (dotnet.Build (), "`dotnet build` should succeed");

			var assemblyPath = Path.Combine (Root, dotnet.ProjectDirectory, proj.OutputPath, "UnnamedProject.dll");
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

			var outputPath = Path.Combine (Root, dotnet.ProjectDirectory, proj.OutputPath);
			if (!runtimeIdentifiers.Contains (";")) {
				outputPath = Path.Combine (outputPath, runtimeIdentifiers);
			}
			var assemblyPath = Path.Combine (outputPath, "UnnamedProject.dll");
			FileAssert.Exists (assemblyPath);
			using (var assembly = AssemblyDefinition.ReadAssembly (assemblyPath)) {
				var typeName = "Com.Xamarin.Android.Test.Msbuildtest.JavaSourceJarTest";
				var type = assembly.MainModule.GetType (typeName);
				Assert.IsNotNull (type, $"{assemblyPath} should contain {typeName}");
			}

			var apk = Path.Combine (outputPath, "UnnamedProject.UnnamedProject.apk");
			FileAssert.Exists (apk);
			using (var zip = ZipHelper.OpenZip (apk)) {
				var rids = runtimeIdentifiers.Split (';');
				foreach (var abi in rids.Select (MonoAndroidHelper.RuntimeIdentifierToAbi)) {
					Assert.IsTrue (zip.ContainsEntry ($"lib/{abi}/libmonodroid.so"), "libmonodroid.so should exist.");
					Assert.IsTrue (zip.ContainsEntry ($"lib/{abi}/libmonosgen-2.0.so"), "libmonosgen-2.0.so should exist.");
					if (rids.Length > 1) {
						var entry = $"assemblies/{abi}/System.Private.CoreLib.dll";
						Assert.IsTrue (zip.ContainsEntry (entry), $"{entry} should exist.");
					} else {
						var entry = "assemblies/System.Private.CoreLib.dll";
						Assert.IsTrue (zip.ContainsEntry (entry), $"{entry} should exist.");
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

			var publishDirectory = Path.Combine (Root, dotnet.ProjectDirectory, proj.OutputPath, runtimeIdentifier, "publish");
			var apk = Path.Combine (publishDirectory, $"{proj.PackageName}.apk");
			var apkSigned = Path.Combine (publishDirectory, $"{proj.PackageName}-Signed.apk");
			FileAssert.Exists (apk);
			FileAssert.Exists (apkSigned);

			Assert.IsTrue (dotnet.Publish (parameters: new [] { "AndroidPackageFormat=aab" }), "second `dotnet publish` should succeed");
			FileAssert.DoesNotExist (apk);
			FileAssert.DoesNotExist (apkSigned);
			var aab = Path.Combine (publishDirectory, $"{proj.PackageName}.aab");
			var aabSigned = Path.Combine (publishDirectory, $"{proj.PackageName}-Signed.aab");
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

			var apk = Path.Combine (Root, dotnet.ProjectDirectory, proj.OutputPath, $"{proj.PackageName}.apk");
			FileAssert.Exists (apk);
			using (var zip = ZipHelper.OpenZip (apk)) {
				Assert.IsTrue (zip.ContainsEntry ("res/raw/foo.txt"), "res/raw/foo.txt should exist!");
				Assert.IsTrue (zip.ContainsEntry ("assets/foo/bar.txt"), "assets/foo/bar.txt should exist!");
			}
		}

		[Test]
		public void BuildWithLiteSdk ()
		{
			var proj = new XASdkProject () {
				Sdk = $"Xamarin.Android.Sdk.Lite/{XASdkProject.SdkVersion}",
				TargetFramework = "monoandroid10.0"
			};
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
			}
		}

		DotNetCLI CreateDotNetBuilder (XASdkProject project)
		{
			var relativeProjDir = Path.Combine ("temp", TestName);
			TestOutputDirectories [TestContext.CurrentContext.Test.ID] =
				FullProjectDirectory = Path.Combine (Root, relativeProjDir);
			var files = project.Save ();
			project.Populate (relativeProjDir, files);
			return new DotNetCLI (project, Path.Combine (FullProjectDirectory, project.ProjectFilePath));
		}
	}
}
