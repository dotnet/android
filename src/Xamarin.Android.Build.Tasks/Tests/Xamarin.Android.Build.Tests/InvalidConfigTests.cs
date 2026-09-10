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
			[Values (AndroidRuntime.MonoVM, AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
		{
			var project = new XamarinAndroidApplicationProject {
				IsRelease = runtime == AndroidRuntime.NativeAOT,
			};
			if (runtime == AndroidRuntime.MonoVM) {
				project.SetProperty ("_DisableCheckForUnsupportedMonoMobileRuntime", "true");
			}
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

		[TestCase (null, "disabled", "false")]
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

		[TestCase ("AndroidR8ObfuscationMode", "unknown", "AndroidR8ObfuscationMode")]
		[TestCase ("AndroidR8ObfuscationMode", "experimental-rewriting", "not available in this SDK")]
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
			project.SetProperty ("AndroidR8ObfuscationMode", "experimental-rewriting");
			using var builder = CreateDllBuilder ();
			builder.Target = "_ValidateAndroidR8ObfuscationMode";
			Assert.IsTrue (builder.Build (project), "Application obfuscation settings must not affect referenced libraries.");
		}

	}
}
