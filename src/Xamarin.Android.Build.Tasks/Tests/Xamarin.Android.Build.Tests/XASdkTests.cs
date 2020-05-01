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
	[Category ("Node-2")]
	public class XASdkTests : BaseTest
	{
		static readonly string SdkVersion = Assembly.GetExecutingAssembly ()
			.GetCustomAttributes<AssemblyMetadataAttribute> ()
			.Where (attr => attr.Key == "SdkVersion")
			.Select (attr => attr.Value)
			.FirstOrDefault () ?? "0.0.1";

		[Test]
		[Category ("SmokeTests")]
		public void DotNetBuildLibrary ([Values (false, true)] bool isRelease)
		{
			var proj = new XASdkProject (SdkVersion, outputType: "Library") {
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
			var proj = new XASdkProject (SdkVersion, outputType: "Library");
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
				/* runtimeIdentifier */  "android.21-arm",
				/* isRelease */          false,
			},
			new object [] {
				/* runtimeIdentifier */  "android.21-arm64",
				/* isRelease */          false,
			},
			new object [] {
				/* runtimeIdentifier */  "android.21-x86",
				/* isRelease */          false,
			},
			new object [] {
				/* runtimeIdentifier */  "android.21-x64",
				/* isRelease */          false,
			},
			new object [] {
				/* runtimeIdentifier */  "android.21-arm",
				/* isRelease */          true,
			},
		};

		[Test]
		[Category ("SmokeTests")]
		[TestCaseSource (nameof (DotNetBuildSource))]
		public void DotNetBuild (string runtimeIdentifier, bool isRelease)
		{
			var abi = MonoAndroidHelper.RuntimeIdentifierToAbi (runtimeIdentifier);
			var proj = new XASdkProject (SdkVersion) {
				IsRelease = isRelease
			};
			proj.OtherBuildItems.Add (new AndroidItem.InputJar ("javaclasses.jar") {
				BinaryContent = () => Convert.FromBase64String (InlineData.JavaClassesJarBase64)
			});
			// TODO: bring back when Xamarin.Android.Bindings.Documentation.targets is working
			//proj.OtherBuildItems.Add (new BuildItem ("JavaSourceJar", "javasources.jar") {
			//	BinaryContent = () => Convert.FromBase64String (InlineData.JavaSourcesJarBase64)
			//});
			proj.SetProperty (KnownProperties.RuntimeIdentifier, runtimeIdentifier);

			var dotnet = CreateDotNetBuilder (proj);
			Assert.IsTrue (dotnet.Build (), "`dotnet build` should succeed");
			Assert.IsTrue (StringAssertEx.ContainsText (dotnet.LastBuildOutput, " 0 Warning(s)"), "Should have no MSBuild warnings.");

			var outputPath = Path.Combine (Root, dotnet.ProjectDirectory, proj.OutputPath, runtimeIdentifier);
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
				Assert.IsTrue (zip.ContainsEntry ($"lib/{abi}/libmonodroid.so"), "libmonodroid.so should exist.");
				Assert.IsTrue (zip.ContainsEntry ($"lib/{abi}/libmonosgen-2.0.so"), "libmonosgen-2.0.so should exist.");
			}
		}

		[Test]
		public void BuildWithLiteSdk ()
		{
			var proj = new XASdkProject () {
				Sdk = $"Xamarin.Android.Sdk.Lite/{SdkVersion}",
				TargetFramework = "monoandroid10.0"
			};
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
			}
		}

		DotNetCLI CreateDotNetBuilder (XASdkProject project)
		{
			var relativeProjDir = Path.Combine ("temp", TestName);
			var fullProjDir = Path.Combine (Root, relativeProjDir);
			TestOutputDirectories [TestContext.CurrentContext.Test.ID] = fullProjDir;
			var files = project.Save ();
			project.Populate (relativeProjDir, files);
			project.CopyNuGetConfig (relativeProjDir);
			return new DotNetCLI (project, Path.Combine (fullProjDir, project.ProjectFilePath));
		}
	}
}
