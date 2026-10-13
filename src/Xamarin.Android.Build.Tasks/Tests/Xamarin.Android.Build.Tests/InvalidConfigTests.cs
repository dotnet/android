using NUnit.Framework;
using System.IO;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	public class InvalidConfigTests : BaseTest
	{
		[Test]
		public void EolFrameworks ([Values ("net6.0-android", "net7.0-android")] string targetFramework)
		{
			var library = new XamarinAndroidLibraryProject () {
				TargetFramework = targetFramework,
				EnableDefaultItems = true,
			};
			var builder = CreateApkBuilder ();
			builder.ThrowOnBuildFailure = false;
			Assert.IsFalse (builder.Restore (library), $"{library.ProjectName} restore should fail");
			Assert.IsTrue (StringAssertEx.ContainsText (builder.LastBuildOutput, $"NETSDK1202: The workload '{targetFramework}' is out of support"), $"{builder.BuildLogFile} should have NETSDK1202.");
		}

		[Test]
		public void XA0119 ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.SetProperty (proj.DebugProperties, "AndroidLinkMode", "Full");
			proj.EmbedAssembliesIntoApk = false;
			using (var b = CreateApkBuilder ()) {
				b.Target = "Build"; // SignAndroidPackage would fail for OSS builds
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				Assert.IsTrue (StringAssertEx.ContainsText (b.LastBuildOutput, "XA0119"), "Output should contain XA0119 warnings");
			}
		}

		[Test]
		public void XA0119AAB ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.SetProperty ("AndroidPackageFormat", "aab");
			using (var builder = CreateApkBuilder ()) {
				builder.ThrowOnBuildFailure = false;
				Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
				Assert.IsTrue (StringAssertEx.ContainsText (builder.LastBuildOutput, "XA0119"), "Output should contain XA0119 warnings");
			}
		}

		[Test]
		public void UnsupportedCodegenTargetIsRejected (
			[Values ("XamarinAndroid", "JavaInterop1", "javainterop1")] string codegenTarget,
			[Values (true, false)] bool isApplication)
		{
			XamarinProject project = isApplication
				? new XamarinAndroidApplicationProject ()
				: new XamarinAndroidLibraryProject ();
			project.SetProperty ("AndroidCodegenTarget", codegenTarget);
			using var builder = isApplication ? CreateApkBuilder () : CreateDllBuilder ();
			builder.Target = "_CheckForInvalidConfigurationAndPlatform";
			builder.ThrowOnBuildFailure = false;
			Assert.IsFalse (builder.Build (project), "Build should have failed.");
			StringAssertEx.Contains ("error XA4232:", builder.LastBuildOutput, "Build should fail with XA4232.");
			StringAssertEx.Contains (codegenTarget, builder.LastBuildOutput, "Error should identify the unsupported code generation target.");
		}

		[Test]
		public void UnsupportedJcwCodegenTargetIsRejected (
			[Values ("XamarinAndroid", "JavaInterop1")] string codegenTarget,
			[Values (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
		{
			var project = new XamarinAndroidApplicationProject {
				IsRelease = runtime == AndroidRuntime.NativeAOT,
			};
			project.SetRuntime (runtime);
			project.SetProperty ("_AndroidJcwCodegenTarget", codegenTarget);
			using (var builder = CreateApkBuilder ()) {
				builder.Target = "_CheckForInvalidConfigurationAndPlatform";
				builder.ThrowOnBuildFailure = false;
				Assert.IsFalse (builder.Build (project), "Build should have failed.");
				StringAssertEx.Contains ("error XA4240:", builder.LastBuildOutput, "Build should fail with XA4240.");
				StringAssertEx.Contains (codegenTarget, builder.LastBuildOutput, "Error should identify the unsupported code generation target.");
			}
		}

		[TestCase (null, "private-members", "false", "")]
		[TestCase ("private-members", "private-members", "false", "")]
		[TestCase ("disabled", "disabled", "false", "")]
		[TestCase ("runtime-remapping", "runtime-remapping", "true", "false")]
		public void R8ObfuscationDefaults (string? mode, string expectedMode, string expectedRemapping, string expectedTypeMapR8Trimming)
		{
			var project = new XamarinAndroidApplicationProject { IsRelease = true };
			project.SetRuntime (AndroidRuntime.CoreCLR);
			project.SetProperty ("AndroidLinkTool", "r8");
			if (mode != null) {
				project.SetProperty ("AndroidR8ObfuscationMode", mode);
			}
			project.Imports.Add (new Import ("R8Options.targets") {
				TextContent = () => """
					<Project>
					  <Target Name="ReportR8Options" DependsOnTargets="_ValidateAndroidR8ObfuscationMode">
					    <Message Importance="High" Text="R8_OPTIONS=$(AndroidR8ObfuscationMode)|$(_AndroidR8RuntimeRemappingEnabled)|$(_AndroidEnableTypemapR8Trimming)" />
					  </Target>
					</Project>
					""",
			});
			using var builder = CreateApkBuilder ();
			builder.Target = "ReportR8Options";
			Assert.IsTrue (builder.Build (project));
			StringAssertEx.Contains ($"R8_OPTIONS={expectedMode}|{expectedRemapping}|{expectedTypeMapR8Trimming}", builder.LastBuildOutput);
		}
		[TestCase ("AndroidLinkTool", "d8", "AndroidLinkTool")]
		[TestCase ("AndroidLinkTool", "", "AndroidLinkTool")]
		[TestCase ("AndroidLinkTool", "", "AndroidLinkTool")]
		[TestCase ("PublishTrimmed", "false", "PublishTrimmed")]
		[TestCase ("_AndroidRuntime", "MonoVM", "Supported runtimes are CoreCLR and NativeAOT")]
		public void R8ObfuscationInvalidConfiguration (string property, string value, string expectedMessage)
		{
			var project = new XamarinAndroidApplicationProject { IsRelease = true };
			project.SetRuntime (AndroidRuntime.CoreCLR);
			project.SetProperty ("RunAOTCompilation", "false");
			project.SetProperty ("AndroidLinkTool", "r8");
			project.SetProperty ("AndroidR8ObfuscationMode", "runtime-remapping");
			if (property == "_AndroidRuntime") {
				project.Imports.Add (new Import ("InvalidAndroidRuntime.targets") {
					TextContent = () => $"""
						<Project>
						  <Target Name="_SetInvalidAndroidRuntimeForTest" BeforeTargets="_ValidateAndroidR8ObfuscationMode">
						    <PropertyGroup>
						      <_AndroidRuntime>{value}</_AndroidRuntime>
						    </PropertyGroup>
						  </Target>
						</Project>
						""",
				});
			} else {
				project.SetProperty (property, value);
			}
			using var builder = CreateApkBuilder ();
			builder.Target = "_ValidateAndroidR8ObfuscationMode";
			builder.ThrowOnBuildFailure = false;
			Assert.IsFalse (builder.Build (project));
			StringAssertEx.Contains ("error XA4329:", builder.LastBuildOutput);
			StringAssertEx.Contains (expectedMessage, builder.LastBuildOutput);
		}

		[Test]
		public void R8ObfuscationDoesNotEnableLibraries ()
		{
			var project = new XamarinAndroidLibraryProject ();
			project.SetProperty ("AndroidR8ObfuscationMode", "runtime-remapping");
			using var builder = CreateDllBuilder ();
			builder.Target = "_ValidateAndroidR8ObfuscationMode";
			Assert.IsTrue (builder.Build (project), "Application obfuscation settings must not affect referenced libraries.");
		}
		[Test]
		public void LibraryBuildDoesNotGenerateTypeMap ([Values (null, "", "llvm-ir", "trimmable", "unsupported")] string? obsoleteImplementation)
		{
			var project = new XamarinAndroidLibraryProject ();
			using var builder = CreateDllBuilder ();
			string [] parameters = obsoleteImplementation == null ? [] : [$"AndroidTypeMapImplementation={obsoleteImplementation}"];
			Assert.IsTrue (builder.Build (project, parameters: parameters), "A library should build regardless of the obsolete type map property.");
			FileAssert.DoesNotExist (builder.Output.GetIntermediaryPath (Path.Combine ("typemap", "typemap-assemblies.txt")));
		}

		[Test]
		[TestCase ("RunAOTCompilation", "true", false)]
		[TestCase ("RunAOTCompilation", "true", true)]
		[TestCase ("RunAOTCompilation", "false", false)]
		[TestCase ("RunAOTCompilation", "false", true)]
		[TestCase ("EnableLLVM", "true", false)]
		[TestCase ("EnableLLVM", "true", true)]
		public void UnsupportedMonoAotPropertyFailsBuild (string property, string value, bool isRelease)
		{
			var project = new XamarinAndroidApplicationProject {
				IsRelease = isRelease,
			};
			project.SetRuntime (AndroidRuntime.CoreCLR);
			project.SetProperty (property, value);

			using var builder = CreateApkBuilder ();
			builder.Target = "_CheckNonIdealAppConfigurations";
			builder.ThrowOnBuildFailure = false;
			Assert.IsFalse (builder.Build (project), "Build should have failed.");
			StringAssertEx.Contains ("error XA1044", builder.LastBuildOutput, "Build output should contain error XA1044");
			StringAssertEx.Contains (property, builder.LastBuildOutput, $"Build output should mention {property}");
			StringAssertEx.Contains ("CoreCLR", builder.LastBuildOutput, "Build output should mention CoreCLR");
			if (property == "RunAOTCompilation" && value == "false") {
				StringAssertEx.Contains ("The build cannot continue while this property is set to 'false'.", builder.LastBuildOutput, "Error should identify the explicitly disabled property.");
				StringAssertEx.Contains ("Starting with .NET 11", builder.LastBuildOutput, "Error should identify the .NET version.");
				StringAssertEx.Contains ("'PublishReadyToRun' to 'false'", builder.LastBuildOutput, "Error should explain how to disable ReadyToRun.");
			} else {
				StringAssertEx.Contains ("The build cannot continue while this property is enabled.", builder.LastBuildOutput, "Error should identify the explicitly enabled property.");
				StringAssertEx.DoesNotContain ("The build cannot continue while this property is set to 'false'.", builder.LastBuildOutput, "Enabled properties should not produce the disabled-property error.");
			}
		}

		[Test]
		public void ReadyToRunWithoutMonoAotProperty (
			[Values ("", "true", "false")] string publishReadyToRun,
			[Values] bool isRelease)
		{
			var project = new XamarinAndroidApplicationProject {
				IsRelease = isRelease,
			};
			project.SetRuntime (AndroidRuntime.CoreCLR);
			project.SetProperty ("PublishReadyToRun", publishReadyToRun);

			using var builder = CreateApkBuilder ();
			builder.Target = "_CheckNonIdealAppConfigurations";
			Assert.IsTrue (builder.Build (project), "Configuration should be valid without RunAOTCompilation.");
		}

		[Test]
		public void RunAotCompilationFalseAllowedForNativeAot ()
		{
			var project = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			project.SetRuntime (AndroidRuntime.NativeAOT);
			project.SetProperty ("RunAOTCompilation", "false");

			using var builder = CreateApkBuilder ();
			builder.Target = "_CheckNonIdealAppConfigurations";
			Assert.IsTrue (builder.Build (project), "RunAOTCompilation=false should remain valid for NativeAOT.");
		}
	}
}
