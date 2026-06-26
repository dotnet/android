using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Mono.Cecil;
using NUnit.Framework;
using Xamarin.ProjectTools;
using Xamarin.Android.Build;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	[Category ("AOT")]
	[Parallelizable (ParallelScope.Children)]
	public class AotTests : BaseTest
	{
		public string SdkWithSpacesPath {
			get {
				return Path.Combine (Root, "temp", string.Format ("SDK Ümläüts"));
			}
		}

		[OneTimeSetUp]
		public void Setup ()
		{
			if (!IsWindows)
				return;

			var sdkPath = AndroidSdkPath;
			var ndkPath = AndroidNdkPath;

			var symSdkPath = Path.Combine (SdkWithSpacesPath, "sdk");
			var symNdkPath = Path.Combine (SdkWithSpacesPath, "ndk");

			SymbolicLink.Create (symSdkPath, sdkPath);
			SymbolicLink.Create (symNdkPath, ndkPath);

			Environment.SetEnvironmentVariable ("TEST_ANDROID_SDK_PATH", symSdkPath);
			Environment.SetEnvironmentVariable ("TEST_ANDROID_NDK_PATH", symNdkPath);
		}

		[OneTimeTearDown]
		public void TearDown ()
		{
			if (!IsWindows)
				return;
			Environment.SetEnvironmentVariable ("TEST_ANDROID_SDK_PATH", "");
			Environment.SetEnvironmentVariable ("TEST_ANDROID_NDK_PATH", "");
			Directory.Delete (SdkWithSpacesPath, recursive: true);
		}

		void AssertProfiledAotBuildMessages(ProjectBuilder b)
		{
			StringAssertEx.ContainsRegex (@$"Using profile data file.*dotnet\.aotprofile", b.LastBuildOutput, "Should use default AOT profile", RegexOptions.IgnoreCase);
			StringAssertEx.ContainsRegex (@$"Method.*emitted at", b.LastBuildOutput, "Should contain verbose AOT compiler output", RegexOptions.IgnoreCase);
		}

		static IEnumerable<object[]> Get_BuildAotApplicationWithSpecialCharactersInProjectData ()
		{
			var ret = new List<object[]> ();

			foreach (AndroidRuntime runtime in new[] { AndroidRuntime.CoreCLR }) {
				AddTestData ("テスト", false, false, runtime);
				AddTestData ("テスト", true, true, runtime);
				AddTestData ("テスト", true, false, runtime);
				AddTestData ("随机生成器", false, false, runtime);
				AddTestData ("随机生成器", true, true, runtime);
				AddTestData ("随机生成器", true, false, runtime);
				AddTestData ("中国", false, false, runtime);
				AddTestData ("中国", true, true, runtime);
				AddTestData ("中国", true, false, runtime);
			}

			return ret;

			void AddTestData (string testName, bool isRelease, bool aot, AndroidRuntime runtime)
			{
				ret.Add (new object[] {
					testName,
					isRelease,
					aot,
					runtime,
				});
			}
		}

		[Test]
		[TestCaseSource (nameof (Get_BuildAotApplicationWithSpecialCharactersInProjectData))]
		public void BuildAotApplicationWithSpecialCharactersInProject (string testName, bool isRelease, bool aot, AndroidRuntime runtime)
		{
			if (aot && runtime == AndroidRuntime.CoreCLR) {
				Assert.Ignore ("AOT + CoreCLR == NativeAOT; Not supported yet here");
				return;
			}

			var rootPath = Path.Combine (Root, "temp", TestName);
			var proj = new XamarinAndroidApplicationProject () {
				ProjectName = testName,
				IsRelease = isRelease,
				AotAssemblies = aot,
			};
			proj.SetRuntime (runtime);
			using (var builder = CreateApkBuilder (Path.Combine (rootPath, proj.ProjectName))){
				Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
			}
		}


		[Test]
		[TestCase (AndroidRuntime.CoreCLR)]
		[NonParallelizable]
		[Category ("XamarinBuildDownload")]
		public void BuildAMassiveApp (AndroidRuntime runtime)
		{
			var testPath = Path.Combine ("temp", "BuildAMassiveApp", runtime.ToString ());
			TestOutputDirectories [TestContext.CurrentContext.Test.ID] = Path.Combine (Root, testPath);
			var sb = new SolutionBuilder ("BuildAMassiveApp.sln") {
				SolutionPath = Path.Combine (Root, testPath),
			};
			var app1 = new XamarinFormsMapsApplicationProject {
				ProjectName = "App1",
				AotAssemblies = runtime == AndroidRuntime.MonoVM,
				IsRelease = true,
			};
			app1.SetRuntime (runtime);
			//NOTE: BuildingInsideVisualStudio prevents the projects from being built as dependencies
			sb.BuildingInsideVisualStudio = false;
			app1.Imports.Add (new Import ("foo.targets") {
				TextContent = () => @"<?xml version=""1.0"" encoding=""utf-16""?>
<Project ToolsVersion=""4.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
<Target Name=""_Foo"" AfterTargets=""_SetLatestTargetFrameworkVersion"">
	<PropertyGroup>
		<AotAssemblies Condition=""!Exists('$(MonoAndroidBinDirectory)" + Path.DirectorySeparatorChar + @"cross-arm')"">False</AotAssemblies>
	</PropertyGroup>
	<Message Text=""$(AotAssemblies)"" />
</Target>
</Project>
",
			});
			sb.Projects.Add (app1);
			var code = new StringBuilder ();
			code.AppendLine ("using System;");
			code.AppendLine ("namespace App1 {");
			code.AppendLine ("\tpublic class AppCode {");
			code.AppendLine ("\t\tpublic void Foo () {");
			for (int i = 0; i < 128; i++) {
				var libName = $"Lib{i}";
				var lib = new XamarinAndroidLibraryProject () {
					ProjectName = libName,
					IsRelease = true,
					OtherBuildItems = {
						new AndroidItem.AndroidAsset ($"Assets\\{libName}.txt") {
							TextContent = () => "Asset1",
							Encoding = Encoding.ASCII,
						},
						new AndroidItem.AndroidAsset ($"Assets\\subfolder\\{libName}.txt") {
							TextContent = () => "Asset2",
							Encoding = Encoding.ASCII,
						},
					},
					Sources = {
						new BuildItem.Source ($"{libName}.cs") {
							TextContent = () => @"using System;

namespace "+ libName + @" {

	public class " + libName + @" {
		public static void Foo () {
		}
	}
}"
						},
					}
				};
				var strings = lib.AndroidResources.First (x => x.Include () == "Resources\\values\\Strings.xml");
				strings.TextContent = () => @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
	<string name=""" + libName + @"_name"">" + libName + @"</string>
</resources>";
				sb.Projects.Add (lib);
				app1.References.Add (new BuildItem.ProjectReference ($"..\\{libName}\\{libName}.csproj", libName, lib.ProjectGuid));
				code.AppendLine ($"\t\t\t{libName}.{libName}.Foo ();");
			}
			code.AppendLine ("\t\t}");
			code.AppendLine ("\t}");
			code.AppendLine ("}");
			app1.Sources.Add (new BuildItem.Source ("Code.cs") {
				TextContent = () => code.ToString (),
			});
			Assert.IsTrue (sb.Build (new string [] { "Configuration=Release" }), "Solution should have built.");
			Assert.IsTrue (sb.BuildProject (app1, "SignAndroidPackage"), "Build of project should have succeeded");
			sb.Dispose ();
		}

		[Test]
		public void RunAOTCompilationWithCoreClrFailsBuild ()
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetProperty ("RunAOTCompilation", "true");

			using var b = CreateApkBuilder ();
			b.ThrowOnBuildFailure = false;
			Assert.IsFalse (b.Build (proj), "Build should have failed.");
			StringAssertEx.Contains ("error XA1044", b.LastBuildOutput, "Build output should contain error XA1044");
			StringAssertEx.Contains ("RunAOTCompilation", b.LastBuildOutput, "Build output should mention RunAOTCompilation");
			StringAssertEx.Contains ("CoreCLR", b.LastBuildOutput, "Build output should mention CoreCLR");
		}

		[Test]
		public void EnableLLVMWithCoreClrFailsBuild ()
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetProperty ("EnableLLVM", "true");

			using var b = CreateApkBuilder ();
			b.ThrowOnBuildFailure = false;
			Assert.IsFalse (b.Build (proj), "Build should have failed.");
			StringAssertEx.Contains ("error XA1044", b.LastBuildOutput, "Build output should contain error XA1044");
			StringAssertEx.Contains ("EnableLLVM", b.LastBuildOutput, "Build output should mention EnableLLVM");
			StringAssertEx.Contains ("CoreCLR", b.LastBuildOutput, "Build output should mention CoreCLR");
		}

	}
}
