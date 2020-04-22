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
		public void DotNetBuild ([Values (false, true)] bool isRelease)
		{
			var proj = new XASdkProject (SdkVersion) {
				IsRelease = isRelease
			};
			var dotnet = CreateDotNetBuilder (proj);
			Assert.IsTrue (dotnet.Build (), "`dotnet build` should succeed");
		}

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

		static readonly object [] DotNetPublishSource = new object [] {
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
		[TestCaseSource (nameof (DotNetPublishSource))]
		public void DotNetPublish (string runtimeIdentifier, bool isRelease)
		{
			var abi = MonoAndroidHelper.RuntimeIdentifierToAbi (runtimeIdentifier);
			//TODO: re-enable these when we have a public .NET 5 Preview 4 build
			if (abi == "x86" || abi == "x86_64")
				Assert.Ignore ($"Ignoring RID {runtimeIdentifier} until a new .NET 5 build is available.");

			var proj = new XASdkProject (SdkVersion) {
				IsRelease = isRelease
			};
			proj.SetProperty (KnownProperties.RuntimeIdentifier, runtimeIdentifier);

			var dotnet = CreateDotNetBuilder (proj);
			Assert.IsTrue (dotnet.Publish (), "`dotnet publish` should succeed");

			var apk = Path.Combine (Root, dotnet.ProjectDirectory, proj.OutputPath,
				runtimeIdentifier, "UnnamedProject.UnnamedProject.apk");
			FileAssert.Exists (apk);
			using (var zip = ZipHelper.OpenZip (apk)) {
				Assert.IsTrue (zip.ContainsEntry ($"lib/{abi}/libmonodroid.so"), "libmonodroid.so should exist.");
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
