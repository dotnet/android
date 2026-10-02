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

		[TestCase (null, "private-members", "false")]
		[TestCase ("private-members", "private-members", "false")]
		[TestCase ("disabled", "disabled", "false")]
		[TestCase ("runtime-remapping", "runtime-remapping", "true")]
		public void R8ObfuscationDefaults (string? mode, string expectedMode, string expectedRemapping)
		{
			var project = new XamarinAndroidApplicationProject { IsRelease = true };
			project.SetRuntime (AndroidRuntime.CoreCLR);
			project.SetProperty ("AndroidLinkTool", "r8");
			project.SetProperty ("AndroidTypeMapImplementation", "trimmable");
			if (mode != null) {
				project.SetProperty ("AndroidR8ObfuscationMode", mode);
			}
			project.Imports.Add (new Import ("R8Options.targets") {
				TextContent = () => """
					<Project>
					  <Target Name="ReportR8Options" DependsOnTargets="_ValidateAndroidR8ObfuscationMode">
					    <Message Importance="High" Text="R8_OPTIONS=$(AndroidR8ObfuscationMode)|$(_AndroidR8RuntimeRemappingEnabled)" />
					  </Target>
					</Project>
					""",
			});
			using var builder = CreateApkBuilder ();
			builder.Target = "ReportR8Options";
			Assert.IsTrue (builder.Build (project));
			StringAssertEx.Contains ($"R8_OPTIONS={expectedMode}|{expectedRemapping}", builder.LastBuildOutput);
		}

		[TestCase ("AndroidLinkTool", "d8", "AndroidLinkTool")]
		[TestCase ("AndroidLinkTool", "", "AndroidLinkTool")]
		[TestCase ("AndroidTypeMapImplementation", "llvm-ir", "AndroidTypeMapImplementation")]
		[TestCase ("PublishTrimmed", "false", "PublishTrimmed")]
		[TestCase ("_AndroidRuntime", "MonoVM", "Supported runtimes are CoreCLR and NativeAOT")]
		public void R8ObfuscationInvalidConfiguration (string property, string value, string expectedMessage)
		{
			var project = new XamarinAndroidApplicationProject { IsRelease = true };
			project.SetRuntime (AndroidRuntime.CoreCLR);
			project.SetProperty ("RunAOTCompilation", "false");
			project.SetProperty ("AndroidLinkTool", "r8");
			project.SetProperty ("AndroidTypeMapImplementation", "trimmable");
			project.SetProperty ("AndroidR8ObfuscationMode", "runtime-remapping");
			project.SetProperty (property, value);
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

		[TestCase (true, false)]
		[TestCase (true, true)]
		[TestCase (false, false)]
		[TestCase (false, true)]
		public void TypeMapDefaultsToTrimmable (bool isApplication, bool isRelease)
		{
			XamarinProject project = isApplication
				? new XamarinAndroidApplicationProject { IsRelease = isRelease }
				: new XamarinAndroidLibraryProject { IsRelease = isRelease };
			project.Imports.Add (new Import ("assert-typemap.targets") {
				TextContent = () => """
					<Project>
					  <Target Name="_AssertTypeMapDefault" DependsOnTargets="_CheckForInvalidConfigurationAndPlatform">
					    <Error Condition=" '$(AndroidTypeMapImplementation)' != 'trimmable' "
					        Text="Expected the trimmable type map by default." />
					  </Target>
					</Project>
					""",
			});

			using var builder = isApplication ? CreateApkBuilder () : CreateDllBuilder ();
			builder.Target = "_AssertTypeMapDefault";
			Assert.IsTrue (builder.Build (project), "The default type map should be trimmable for applications and libraries.");
		}

		[Test]
		public void LibraryBuildDoesNotGenerateTypeMap ()
		{
			var project = new XamarinAndroidLibraryProject ();
			using var builder = CreateDllBuilder ();
			Assert.IsTrue (builder.Build (project), "A library should build with the trimmable type map default.");
			FileAssert.DoesNotExist (builder.Output.GetIntermediaryPath (Path.Combine ("typemap", "typemap-assemblies.txt")));
		}

		[TestCase (true, "llvm-ir", "XA4267")]
		[TestCase (false, "llvm-ir", "XA4267")]
		[TestCase (true, "unsupported", "Invalid value for AndroidTypeMapImplementation")]
		[TestCase (false, "unsupported", "Invalid value for AndroidTypeMapImplementation")]
		public void UnsupportedTypeMapIsRejected (bool isApplication, string typeMapImplementation, string expectedError)
		{
			XamarinProject project = isApplication
				? new XamarinAndroidApplicationProject ()
				: new XamarinAndroidLibraryProject ();
			project.SetProperty ("AndroidTypeMapImplementation", typeMapImplementation);

			using var builder = isApplication ? CreateApkBuilder () : CreateDllBuilder ();
			builder.Target = "_CheckForInvalidConfigurationAndPlatform";
			builder.ThrowOnBuildFailure = false;
			Assert.IsFalse (builder.Build (project), "An unsupported type map should fail validation.");
			StringAssertEx.Contains (expectedError, builder.LastBuildOutput);
		}

		[TestCase (true, "Build")]
		[TestCase (false, "Build")]
		[TestCase (true, "Publish")]
		[TestCase (false, "Publish")]
		[TestCase (true, "_GenerateJavaStubs")]
		[TestCase (true, "_PrepareLinking")]
		public void LegacyTypeMapIsRejectedBeforeBuildTargets (bool isApplication, string target)
		{
			XamarinProject project = isApplication
				? new XamarinAndroidApplicationProject ()
				: new XamarinAndroidLibraryProject ();
			project.SetProperty ("AndroidTypeMapImplementation", "llvm-ir");
			if (target == "_PrepareLinking") {
				project.SetProperty ("PublishTrimmed", "true");
			}

			using var builder = isApplication ? CreateApkBuilder () : CreateDllBuilder ();
			builder.Target = target;
			builder.ThrowOnBuildFailure = false;
			Assert.IsFalse (builder.Build (project), "Legacy type maps should be rejected before generation.");
			StringAssertEx.Contains ("error XA4267:", builder.LastBuildOutput);
		}

		[TestCase (true, "_CheckForInvalidConfigurationAndPlatform")]
		[TestCase (false, "_CheckForInvalidConfigurationAndPlatform")]
		[TestCase (true, "Build")]
		[TestCase (false, "Build")]
		[TestCase (true, "Publish")]
		[TestCase (false, "Publish")]
		[TestCase (true, "_GenerateJavaStubs")]
		[TestCase (true, "_PrepareLinking")]
		public void EmptyGlobalTypeMapIsRejected (bool isApplication, string target)
		{
			XamarinProject project = isApplication
				? new XamarinAndroidApplicationProject ()
				: new XamarinAndroidLibraryProject ();
			project.SetProperty ("PublishTrimmed", "true");

			using var builder = isApplication ? CreateApkBuilder () : CreateDllBuilder ();
			builder.Target = target;
			builder.ThrowOnBuildFailure = false;
			Assert.IsFalse (builder.Build (project, parameters: ["AndroidTypeMapImplementation="]),
				"An empty global property must not select the legacy type map.");
			StringAssertEx.Contains ("Invalid value for AndroidTypeMapImplementation: ''.", builder.LastBuildOutput);
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
