using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Mono.Cecil;
using NUnit.Framework;
using Xamarin.ProjectTools;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	[Parallelizable (ParallelScope.Children)]
	public class EnvironmentContentTests : BaseTest
	{
		[Test]
		[NonParallelizable]
		public void BuildApplicationWithMonoEnvironment ([Values ("", "Normal", "Offline")] string sequencePointsMode, [Values] AndroidRuntime runtime)
		{
			const bool isRelease = true;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			// TODO: NativeAOT fails all the tests, `MONO_DEBUG` is not found in the environment. Question is - should we fix it for backward compatibility,
			//       even though NativeAOT won't use it (and CoreCLR passes the tests), or should we just ignore this test for NativeAOT?
			if (runtime == AndroidRuntime.NativeAOT) {
				Assert.Ignore ("NativeAOT doesn't currently export the MONO_DEBUG environment variable");
			}

			string supportedAbis = runtime switch {
				AndroidRuntime.MonoVM  => "armeabi-v7a;x86",
				AndroidRuntime.CoreCLR => "arm64-v8a;x86_64",
				AndroidRuntime.NativeAOT => "arm64-v8a;x86_64",
				_                      => throw new NotSupportedException ($"Unsupported runtime '{runtime}'")
			};

			var lib = new XamarinAndroidLibraryProject {
				ProjectName = "Library1",
				IsRelease = isRelease,
				OtherBuildItems = { new AndroidItem.AndroidEnvironment ("Mono.env") {
						TextContent = () => "MONO_DEBUG=soft-breakpoints"
					},
				},
			};
			lib.SetRuntime (runtime);
			var app = new XamarinFormsAndroidApplicationProject () {
				IsRelease = isRelease,
				AndroidLinkModeRelease = AndroidLinkMode.Full,
				References = {
					new BuildItem ("ProjectReference","..\\Library1\\Library1.csproj"),
				},
			};
			app.SetRuntime (runtime);
			//LinkSkip one assembly that contains __AndroidLibraryProjects__.zip
			string linkSkip = "FormsViewGroup";
			app.SetProperty ("AndroidLinkSkip", linkSkip);
			app.SetProperty ("_AndroidSequencePointsMode", sequencePointsMode);
			app.SetAndroidSupportedAbis (supportedAbis);
			using (var libb = CreateDllBuilder (Path.Combine ("temp", TestName, lib.ProjectName)))
			using (var appb = CreateApkBuilder (Path.Combine ("temp", TestName, app.ProjectName))) {
				Assert.IsTrue (libb.Build (lib), "Library build should have succeeded.");
				Assert.IsTrue (appb.Build (app), "App should have succeeded.");

				string intermediateOutputDir = Path.Combine (Root, appb.ProjectDirectory, app.IntermediateOutputPath);
				List<EnvironmentHelper.EnvironmentFile> envFiles = EnvironmentHelper.GatherEnvironmentFiles (intermediateOutputDir, supportedAbis, true, runtime);
				Dictionary<string, string> envvars = EnvironmentHelper.ReadEnvironmentVariables (envFiles, runtime);
				Assert.IsTrue (envvars.Count > 0, $"No environment variables defined");

				string monoDebugVar;
				Assert.IsTrue (envvars.TryGetValue ("MONO_DEBUG", out monoDebugVar), "Environment should contain MONO_DEBUG");
				Assert.IsFalse (String.IsNullOrEmpty (monoDebugVar), "Environment must contain MONO_DEBUG with a value");
				Assert.IsTrue (monoDebugVar.IndexOf ("soft-breakpoints", StringComparison.Ordinal) >= 0, "Environment must contain MONO_DEBUG with 'soft-breakpoints' in its value");

				if (!String.IsNullOrEmpty (sequencePointsMode))
					Assert.IsTrue (monoDebugVar.IndexOf ("gen-compact-seq-points", StringComparison.Ordinal) >= 0, "The values from Mono.env should have been merged into environment");

				EnvironmentHelper.AssertValidEnvironmentSharedLibrary (intermediateOutputDir, AndroidSdkPath, AndroidNdkPath, supportedAbis, runtime);

				var assemblyDir = Path.Combine (Root, appb.ProjectDirectory, app.IntermediateOutputPath, "android", "assets");
				var rp = new ReaderParameters { ReadSymbols = false };
				foreach (var assemblyFile in Directory.EnumerateFiles (assemblyDir, "*.dll")) {
					using (var assembly = AssemblyDefinition.ReadAssembly (assemblyFile)) {
						foreach (var module in assembly.Modules) {
							var resources = module.Resources.Select (r => r.Name).ToArray ();
							Assert.IsFalse (StringAssertEx.ContainsText (resources, "__AndroidEnvironment__"), "AndroidEnvironment EmbeddedResource should be stripped!");
							Assert.IsFalse (StringAssertEx.ContainsText (resources, "__AndroidLibraryProjects__.zip"), "__AndroidLibraryProjects__.zip should be stripped!");
							Assert.IsFalse (StringAssertEx.ContainsText (resources, "__AndroidNativeLibraries__.zip"), "__AndroidNativeLibraries__.zip should be stripped!");
						}
					}
				}
			}
		}

		[Test]
		public void CheckMonoDebugIsAddedToEnvironment ([Values ("", "Normal", "Offline")] string sequencePointsMode)
		{
			const string supportedAbis = "armeabi-v7a;x86";

			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
			};

			// Mono-only test
			proj.SetRuntime (AndroidRuntime.MonoVM);
			proj.SetProperty ("_AndroidSequencePointsMode", sequencePointsMode);
			proj.SetAndroidSupportedAbis (supportedAbis);
			using (var b = CreateApkBuilder ()) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");

				string intermediateOutputDir = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath);
				List<EnvironmentHelper.EnvironmentFile> envFiles = EnvironmentHelper.GatherEnvironmentFiles (intermediateOutputDir, supportedAbis, true, AndroidRuntime.MonoVM);
				Dictionary<string, string> envvars = EnvironmentHelper.ReadEnvironmentVariables (envFiles, AndroidRuntime.MonoVM);
				Assert.IsTrue (envvars.Count > 0, $"No environment variables defined");

				string monoDebugVar;
				bool monoDebugVarFound = envvars.TryGetValue ("MONO_DEBUG", out monoDebugVar);
				if (String.IsNullOrEmpty (sequencePointsMode))
					Assert.IsFalse (monoDebugVarFound, $"environment should not contain MONO_DEBUG={monoDebugVar}");
				else {
					Assert.IsTrue (monoDebugVarFound, "environment should contain MONO_DEBUG");
					Assert.AreEqual ("gen-compact-seq-points", monoDebugVar, "environment should contain MONO_DEBUG=gen-compact-seq-points");
				}

				EnvironmentHelper.AssertValidEnvironmentSharedLibrary (intermediateOutputDir, AndroidSdkPath, AndroidNdkPath, supportedAbis, AndroidRuntime.MonoVM);
			}
		}

		[Test]
		public void CheckConcurrentGC ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
			};

			var gcVarName = "MONO_GC_PARAMS";
			var expectedDefaultValue = "major=marksweep";
			var expectedUpdatedValue = "major=marksweep-conc";
			var supportedAbis = "armeabi-v7a;arm64-v8a";
			// MonoVM-only test
			proj.SetRuntime (Android.Tasks.AndroidRuntime.MonoVM);
			proj.SetAndroidSupportedAbis (supportedAbis);

			using (var b = CreateApkBuilder ()) {
				proj.SetProperty ("AndroidEnableSGenConcurrent", "False");
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				var intermediateOutputDir = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath);
				// AndroidEnableSGenConcurrent=False by default
				List<EnvironmentHelper.EnvironmentFile> envFiles = EnvironmentHelper.GatherEnvironmentFiles (intermediateOutputDir, supportedAbis, true, AndroidRuntime.MonoVM);
				Dictionary<string, string> envvars = EnvironmentHelper.ReadEnvironmentVariables (envFiles, AndroidRuntime.MonoVM);
				Assert.IsTrue (envvars.ContainsKey (gcVarName), $"Environment should contain '{gcVarName}'.");
				Assert.AreEqual (expectedDefaultValue, envvars[gcVarName], $"'{gcVarName}' should have been '{expectedDefaultValue}' when concurrent GC is disabled.");

				proj.SetProperty ("AndroidEnableSGenConcurrent", "True");
				Assert.IsTrue (b.Build (proj), "Second build should have succeeded.");
				envFiles = EnvironmentHelper.GatherEnvironmentFiles (intermediateOutputDir, supportedAbis, true, AndroidRuntime.MonoVM);
				envvars = EnvironmentHelper.ReadEnvironmentVariables (envFiles, AndroidRuntime.MonoVM);
				Assert.IsTrue (envvars.ContainsKey (gcVarName), $"Environment should contain '{gcVarName}'.");
				Assert.AreEqual (expectedUpdatedValue, envvars[gcVarName], $"'{gcVarName}' should have been '{expectedUpdatedValue}' when concurrent GC is enabled.");
			}
		}

		[Test]
		public void CheckForInvalidHttpClientHandlerType ([Values] AndroidRuntime runtime)
		{
			const bool isRelease = true;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
			};
			proj.SetRuntime (runtime);
			using (var b = CreateApkBuilder ()) {
				b.ThrowOnBuildFailure = false;
				proj.SetProperty ("AndroidHttpClientHandlerType", "Android.App.Application");
				Assert.IsFalse (b.Build (proj), "Build should not have succeeded.");
				Assert.IsTrue (StringAssertEx.ContainsText (b.LastBuildOutput, "XA1031"), "Output should contain XA1031");
			}
		}

		[Test]
		public void CheckHttpClientHandlerType ([Values] AndroidRuntime runtime)
		{
			bool isRelease = runtime == AndroidRuntime.NativeAOT;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
			};
			var httpClientHandlerVarName = "XA_HTTP_CLIENT_HANDLER_TYPE";
			var expectedDefaultValue = "System.Net.Http.SocketsHttpHandler, System.Net.Http";
			var expectedUpdatedValue = "Xamarin.Android.Net.AndroidMessageHandler";

			string supportedAbis = runtime switch {
				AndroidRuntime.MonoVM  => "armeabi-v7a;arm64-v8a",
				AndroidRuntime.CoreCLR => "arm64-v8a;x86_64",
				AndroidRuntime.NativeAOT => "arm64-v8a;x86_64",
				_                      => throw new NotSupportedException ($"Unsupported runtime '{runtime}'")
			};
			proj.SetRuntime (runtime);
			proj.SetAndroidSupportedAbis (supportedAbis);
			proj.PackageReferences.Add (new Package() { Id = "System.Net.Http", Version = "*" });
			proj.MainActivity = proj.DefaultMainActivity.Replace ("//${AFTER_ONCREATE}", "var _ = new System.Net.Http.HttpClient ();");

			using (var b = CreateApkBuilder ()) {
				proj.SetProperty ("AndroidHttpClientHandlerType", expectedDefaultValue);
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				var intermediateOutputDir = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath);

				List<EnvironmentHelper.EnvironmentFile>? envFiles = null;
				Dictionary<string, string> envvars;

				if (runtime == AndroidRuntime.NativeAOT) {
					envvars = EnvironmentHelper.ReadNativeAotEnvironmentVariables (intermediateOutputDir);
				} else {
					envFiles = EnvironmentHelper.GatherEnvironmentFiles (intermediateOutputDir, supportedAbis, true, runtime);
					envvars = EnvironmentHelper.ReadEnvironmentVariables (envFiles, runtime);
				}

				Assert.IsTrue (envvars.ContainsKey (httpClientHandlerVarName), $"Environment should contain '{httpClientHandlerVarName}'.");
				Assert.AreEqual (expectedDefaultValue, envvars[httpClientHandlerVarName]);

				proj.SetProperty ("AndroidHttpClientHandlerType", expectedUpdatedValue);
				Assert.IsTrue (b.Build (proj), "Second build should have succeeded.");

				if (runtime == AndroidRuntime.NativeAOT) {
					envvars = EnvironmentHelper.ReadNativeAotEnvironmentVariables (intermediateOutputDir);
				} else {
					envFiles = EnvironmentHelper.GatherEnvironmentFiles (intermediateOutputDir, supportedAbis, true, runtime);
					envvars = EnvironmentHelper.ReadEnvironmentVariables (envFiles, runtime);
				}

				Assert.IsTrue (envvars.ContainsKey (httpClientHandlerVarName), $"Environment should contain '{httpClientHandlerVarName}'.");
				Assert.AreEqual (expectedUpdatedValue, envvars[httpClientHandlerVarName]);
			}
		}

		[Test]
		public void CheckDebuggerEnvironmentVariables ()
		{
			const string supportedAbis = "arm64-v8a;x86_64";

			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = false,
			};

			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetProperty ("AndroidEnableDebugger", "true");
			proj.SetAndroidSupportedAbis (supportedAbis);

			using (var b = CreateApkBuilder ()) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");

				string intermediateOutputDir = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath);
				List<EnvironmentHelper.EnvironmentFile> envFiles = EnvironmentHelper.GatherEnvironmentFiles (intermediateOutputDir, supportedAbis, true, AndroidRuntime.CoreCLR);
				Dictionary<string, string> envvars = EnvironmentHelper.ReadEnvironmentVariables (envFiles, AndroidRuntime.CoreCLR);
				Assert.IsTrue (envvars.Count > 0, $"No environment variables defined");

				Assert.IsTrue (envvars.ContainsKey ("CORECLR_ENABLE_PROFILING"), "Environment should contain CORECLR_ENABLE_PROFILING");
				Assert.AreEqual ("1", envvars ["CORECLR_ENABLE_PROFILING"], "CORECLR_ENABLE_PROFILING should be '1'");

				Assert.IsTrue (envvars.ContainsKey ("CORECLR_PROFILER"), "Environment should contain CORECLR_PROFILER");
				Assert.AreEqual ("{9DC623E8-C88F-4FD5-AD99-77E67E1D9631}", envvars ["CORECLR_PROFILER"], "CORECLR_PROFILER GUID mismatch");

				Assert.IsTrue (envvars.ContainsKey ("CORECLR_PROFILER_PATH"), "Environment should contain CORECLR_PROFILER_PATH");
				Assert.AreEqual ("libremotemscordbitarget.so", envvars ["CORECLR_PROFILER_PATH"], "CORECLR_PROFILER_PATH should point to libremotemscordbitarget.so");

				Assert.IsFalse (envvars.ContainsKey ("MONO_GC_PARAMS"), "CoreCLR builds should not set MONO_GC_PARAMS");
				Assert.IsFalse (envvars.ContainsKey ("MONO_DEBUG"), "CoreCLR builds should not set MONO_DEBUG");
			}
		}

		[Test]
		public void CheckDebuggerNativeLibraryInApk ()
		{
			const string supportedAbis = "arm64-v8a;x86_64";

			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = false,
			};

			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetProperty ("AndroidEnableDebugger", "true");
			proj.SetAndroidSupportedAbis (supportedAbis);

			using (var b = CreateApkBuilder ()) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");

				string apk = Path.Combine (Root, b.ProjectDirectory, proj.OutputPath, $"{proj.PackageName}-Signed.apk");
				Assert.IsTrue (File.Exists (apk), $"APK not found at {apk}");

				foreach (var abi in supportedAbis.Split (';')) {
					string entryPath = $"lib/{abi}/libremotemscordbitarget.so";
					var data = ZipHelper.ReadFileFromZip (apk, entryPath);
					Assert.IsNotNull (data, $"{entryPath} should be present in the APK");
					Assert.IsTrue (data.Length > 0, $"{entryPath} should not be empty");
				}
			}
		}

		[Test]
		public void CheckDebuggerNativeLibraryNotInReleaseApk ()
		{
			const string supportedAbis = "arm64-v8a";

			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
			};

			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetAndroidSupportedAbis (supportedAbis);

			using (var b = CreateApkBuilder ()) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");

				string apk = Path.Combine (Root, b.ProjectDirectory, proj.OutputPath, $"{proj.PackageName}-Signed.apk");
				Assert.IsTrue (File.Exists (apk), $"APK not found at {apk}");

				string entryPath = $"lib/{supportedAbis}/libremotemscordbitarget.so";
				var data = ZipHelper.ReadFileFromZip (apk, entryPath);
				Assert.IsNull (data, $"{entryPath} should NOT be present in Release APK");
			}
		}

		[Test]
		public void CheckDebuggerNotEnabledInRelease ()
		{
			const string supportedAbis = "arm64-v8a";

			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
			};

			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetAndroidSupportedAbis (supportedAbis);

			using (var b = CreateApkBuilder ()) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");

				string intermediateOutputDir = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath);
				List<EnvironmentHelper.EnvironmentFile> envFiles = EnvironmentHelper.GatherEnvironmentFiles (intermediateOutputDir, supportedAbis, true, AndroidRuntime.CoreCLR);
				Dictionary<string, string> envvars = EnvironmentHelper.ReadEnvironmentVariables (envFiles, AndroidRuntime.CoreCLR);

				Assert.IsFalse (envvars.ContainsKey ("CORECLR_ENABLE_PROFILING"), "Release builds should not enable CoreCLR profiling");
				Assert.IsFalse (envvars.ContainsKey ("CORECLR_PROFILER"), "Release builds should not set CORECLR_PROFILER");
			}
		}
	}
}
