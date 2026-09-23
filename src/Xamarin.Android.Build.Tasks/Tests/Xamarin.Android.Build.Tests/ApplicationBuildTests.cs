using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Microsoft.Build.Framework;
using NUnit.Framework;
using Xamarin.Android.Build;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[Parallelizable (ParallelScope.Children)]
	public class ApplicationBuildTests : BaseTest
	{
		static IEnumerable<object[]> GetBuildApplicationWithSpecialCharactersData ()
		{
			foreach (string projectName in new [] { "テスト", "随机生成器", "中国" }) {
				yield return new object [] { projectName, false };
				yield return new object [] { projectName, true };
			}
		}

		[Test]
		[TestCaseSource (nameof (GetBuildApplicationWithSpecialCharactersData))]
		public void BuildApplicationWithSpecialCharactersInProject (string projectName, bool isRelease)
		{
			var rootPath = Path.Combine (Root, "temp", TestName);
			var project = new XamarinAndroidApplicationProject {
				ProjectName = projectName,
				IsRelease = isRelease,
			};
			project.SetRuntime (AndroidRuntime.CoreCLR);
			using var builder = CreateApkBuilder (Path.Combine (rootPath, project.ProjectName));
			Assert.IsTrue (builder.Build (project), "Build should have succeeded.");
		}

		[Test]
		[NonParallelizable]
		public void BuildApplicationWithSpecialCharactersInToolchainPath ()
		{
			if (!IsWindows) {
				Assert.Ignore ("Symbolic-link handling for this path scenario is Windows-specific.");
			}

			string toolchainRoot = Path.Combine (Root, "temp", $"{TestName}-toolchain");
			string toolchainPath = Path.Combine (toolchainRoot, "SDK Ümläüts");
			string sdkPath = Path.Combine (toolchainPath, "sdk");
			string? oldSdkPath = Environment.GetEnvironmentVariable ("TEST_ANDROID_SDK_PATH");
			try {
				if (!SymbolicLink.Create (sdkPath, AndroidSdkPath)) {
					Assert.Ignore ("Creating symbolic links requires Windows Developer Mode or elevated permissions.");
				}
				DirectoryAssert.Exists (Path.Combine (sdkPath, "platforms"), "The SDK symbolic link should resolve.");
				Environment.SetEnvironmentVariable ("TEST_ANDROID_SDK_PATH", sdkPath);

				var project = new XamarinAndroidApplicationProject ();
				project.SetRuntime (AndroidRuntime.CoreCLR);
				using var builder = CreateApkBuilder (Path.Combine ("temp", TestName, "App"));
				builder.Verbosity = LoggerVerbosity.Detailed;
				Assert.IsTrue (builder.Build (project), "Build should have succeeded.");
				StringAssertEx.Contains ($"_AndroidSdkDirectory={sdkPath}", builder.LastBuildOutput, "The build should use the SDK path containing spaces and non-ASCII characters.");
			} finally {
				Environment.SetEnvironmentVariable ("TEST_ANDROID_SDK_PATH", oldSdkPath);
				try {
					if (Directory.Exists (toolchainRoot)) {
						Directory.Delete (toolchainRoot, recursive: true);
					}
				} catch (Exception e) {
					TestContext.Out.WriteLine ($"Failed to delete '{toolchainRoot}': {e}");
				}
			}
		}

		[Test]
		[NonParallelizable]
		[Category ("XamarinBuildDownload")]
		public void BuildAMassiveApp ()
		{
			var testPath = Path.Combine ("temp", TestName);
			TestOutputDirectories [TestContext.CurrentContext.Test.ID] = Path.Combine (Root, testPath);
			using var solution = new SolutionBuilder ("BuildAMassiveApp.sln") {
				SolutionPath = Path.Combine (Root, testPath),
				BuildingInsideVisualStudio = false,
			};
			var app = new XamarinFormsMapsApplicationProject {
				ProjectName = "App1",
				IsRelease = true,
			};
			app.SetRuntime (AndroidRuntime.CoreCLR);
			solution.Projects.Add (app);

			var code = new StringBuilder ();
			code.AppendLine ("using System;");
			code.AppendLine ("namespace App1 {");
			code.AppendLine ("\tpublic class AppCode {");
			code.AppendLine ("\t\tpublic void Foo () {");
			for (int i = 0; i < 128; i++) {
				string libraryName = $"Lib{i}";
				var library = new XamarinAndroidLibraryProject {
					ProjectName = libraryName,
					IsRelease = true,
					OtherBuildItems = {
						new AndroidItem.AndroidAsset ($"Assets\\{libraryName}.txt") {
							TextContent = () => "Asset1",
							Encoding = Encoding.ASCII,
						},
						new AndroidItem.AndroidAsset ($"Assets\\subfolder\\{libraryName}.txt") {
							TextContent = () => "Asset2",
							Encoding = Encoding.ASCII,
						},
					},
					Sources = {
						new BuildItem.Source ($"{libraryName}.cs") {
							TextContent = () => $$"""
using System;

namespace {{libraryName}} {
	public class {{libraryName}} {
		public static void Foo () {
		}
	}
}
""",
						},
					},
				};
				var strings = library.AndroidResources.First (item => item.Include () == "Resources\\values\\Strings.xml");
				strings.TextContent = () => $$"""
<?xml version="1.0" encoding="utf-8"?>
<resources>
	<string name="{{libraryName}}_name">{{libraryName}}</string>
</resources>
""";
				solution.Projects.Add (library);
				app.References.Add (new BuildItem.ProjectReference ($"..\\{libraryName}\\{libraryName}.csproj", libraryName, library.ProjectGuid));
				code.AppendLine ($"\t\t\t{libraryName}.{libraryName}.Foo ();");
			}
			code.AppendLine ("\t\t}");
			code.AppendLine ("\t}");
			code.AppendLine ("}");
			app.Sources.Add (new BuildItem.Source ("Code.cs") {
				TextContent = () => code.ToString (),
			});

			Assert.IsTrue (solution.Build (new [] { "Configuration=Release" }), "Solution should have built.");
			Assert.IsTrue (solution.BuildProject (app, "SignAndroidPackage"), "Build of project should have succeeded");
		}
	}
}
