using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Mono.Cecil;
using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[Parallelizable (ParallelScope.Children)]
	public class EnvironmentContentTests : BaseTest
	{
		[Test]
		[NonParallelizable]
		public void BuildApplicationWithMonoEnvironment ([Values ("", "Normal", "Offline")] string sequencePointsMode, [Values(false, true)] bool useNativeRuntimeLinkingMode)
		{
			const string supportedAbis = "armeabi-v7a;x86";

			var lib = new XamarinAndroidLibraryProject {
				ProjectName = "Library1",
				IsRelease = true,
				OtherBuildItems = { new AndroidItem.AndroidEnvironment ("Mono.env") {
						TextContent = () => "MONO_DEBUG=soft-breakpoints"
					},
				},
			};
			var app = new XamarinFormsAndroidApplicationProject () {
				IsRelease = true,
				AndroidLinkModeRelease = AndroidLinkMode.Full,
				References = {
					new BuildItem ("ProjectReference","..\\Library1\\Library1.csproj"),
				},
			};
			//LinkSkip one assembly that contains __AndroidLibraryProjects__.zip
			string linkSkip = "FormsViewGroup";
			app.SetProperty ("AndroidLinkSkip", linkSkip);
			app.SetProperty ("_AndroidSequencePointsMode", sequencePointsMode);
			app.SetProperty ("_AndroidEnableNativeRuntimeLinking", useNativeRuntimeLinkingMode.ToString ());
			app.SetAndroidSupportedAbis (supportedAbis);
			using (var libb = CreateDllBuilder (Path.Combine ("temp", TestName, lib.ProjectName)))
			using (var appb = CreateApkBuilder (Path.Combine ("temp", TestName, app.ProjectName))) {
				Assert.IsTrue (libb.Build (lib), "Library build should have succeeded.");
				Assert.IsTrue (appb.Build (app), "App should have succeeded.");

				string intermediateOutputDir = Path.Combine (Root, appb.ProjectDirectory, app.IntermediateOutputPath);
				List<EnvironmentHelper.EnvironmentFile> envFiles = EnvironmentHelper.GatherEnvironmentFiles (intermediateOutputDir, supportedAbis, true);
				Dictionary<string, string> envvars = EnvironmentHelper.ReadEnvironmentVariables (envFiles);
				Assert.IsTrue (envvars.Count > 0, $"No environment variables defined");

				string monoDebugVar;
				Assert.IsTrue (envvars.TryGetValue ("MONO_DEBUG", out monoDebugVar), "Environment should contain MONO_DEBUG");
				Assert.IsFalse (String.IsNullOrEmpty (monoDebugVar), "Environment must contain MONO_DEBUG with a value");
				Assert.IsTrue (monoDebugVar.IndexOf ("soft-breakpoints", StringComparison.Ordinal) >= 0, "Environment must contain MONO_DEBUG with 'soft-breakpoints' in its value");

				if (!String.IsNullOrEmpty (sequencePointsMode))
					Assert.IsTrue (monoDebugVar.IndexOf ("gen-compact-seq-points", StringComparison.Ordinal) >= 0, "The values from Mono.env should have been merged into environment");

				EnvironmentHelper.AssertValidEnvironmentSharedLibrary (intermediateOutputDir, AndroidSdkPath, AndroidNdkPath, supportedAbis, useNativeRuntimeLinkingMode);

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
		public void CheckMonoDebugIsAddedToEnvironment ([Values ("", "Normal", "Offline")] string sequencePointsMode, [Values(false, true)] bool useNativeRuntimeLinkingMode)
		{
			const string supportedAbis = "armeabi-v7a;x86";

			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
			};
			proj.SetProperty ("_AndroidSequencePointsMode", sequencePointsMode);
			proj.SetProperty ("_AndroidEnableNativeRuntimeLinking", useNativeRuntimeLinkingMode.ToString ());
			proj.SetAndroidSupportedAbis (supportedAbis);
			using (var b = CreateApkBuilder ()) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");

				string intermediateOutputDir = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath);
				List<EnvironmentHelper.EnvironmentFile> envFiles = EnvironmentHelper.GatherEnvironmentFiles (intermediateOutputDir, supportedAbis, true);
				Dictionary<string, string> envvars = EnvironmentHelper.ReadEnvironmentVariables (envFiles);
				Assert.IsTrue (envvars.Count > 0, $"No environment variables defined");

				string monoDebugVar;
				bool monoDebugVarFound = envvars.TryGetValue ("MONO_DEBUG", out monoDebugVar);
				if (String.IsNullOrEmpty (sequencePointsMode))
					Assert.IsFalse (monoDebugVarFound, $"environment should not contain MONO_DEBUG={monoDebugVar}");
				else {
					Assert.IsTrue (monoDebugVarFound, "environment should contain MONO_DEBUG");
					Assert.AreEqual ("gen-compact-seq-points", monoDebugVar, "environment should contain MONO_DEBUG=gen-compact-seq-points");
				}

				EnvironmentHelper.AssertValidEnvironmentSharedLibrary (intermediateOutputDir, AndroidSdkPath, AndroidNdkPath, supportedAbis, useNativeRuntimeLinkingMode);
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
			proj.SetAndroidSupportedAbis (supportedAbis);

			using (var b = CreateApkBuilder ()) {
				proj.SetProperty ("AndroidEnableSGenConcurrent", "False");
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				var intermediateOutputDir = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath);
				// AndroidEnableSGenConcurrent=False by default
				List<EnvironmentHelper.EnvironmentFile> envFiles = EnvironmentHelper.GatherEnvironmentFiles (intermediateOutputDir, supportedAbis, true);
				Dictionary<string, string> envvars = EnvironmentHelper.ReadEnvironmentVariables (envFiles);
				Assert.IsTrue (envvars.ContainsKey (gcVarName), $"Environment should contain '{gcVarName}'.");
				Assert.AreEqual (expectedDefaultValue, envvars[gcVarName], $"'{gcVarName}' should have been '{expectedDefaultValue}' when concurrent GC is disabled.");

				proj.SetProperty ("AndroidEnableSGenConcurrent", "True");
				Assert.IsTrue (b.Build (proj), "Second build should have succeeded.");
				envFiles = EnvironmentHelper.GatherEnvironmentFiles (intermediateOutputDir, supportedAbis, true);
				envvars = EnvironmentHelper.ReadEnvironmentVariables (envFiles);
				Assert.IsTrue (envvars.ContainsKey (gcVarName), $"Environment should contain '{gcVarName}'.");
				Assert.AreEqual (expectedUpdatedValue, envvars[gcVarName], $"'{gcVarName}' should have been '{expectedUpdatedValue}' when concurrent GC is enabled.");
			}
		}

		[Test]
		public void CheckForInvalidHttpClientHandlerType ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
			};
			using (var b = CreateApkBuilder ()) {
				b.ThrowOnBuildFailure = false;
				proj.SetProperty ("AndroidHttpClientHandlerType", "Android.App.Application");
				Assert.IsFalse (b.Build (proj), "Build should not have succeeded.");
				Assert.IsTrue (StringAssertEx.ContainsText (b.LastBuildOutput, "XA1031"), "Output should contain XA1031");
			}
		}

		[Test]
		public void CheckHttpClientHandlerType ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
			};
			var httpClientHandlerVarName = "XA_HTTP_CLIENT_HANDLER_TYPE";
			var expectedDefaultValue = "System.Net.Http.SocketsHttpHandler, System.Net.Http";
			var expectedUpdatedValue = "Xamarin.Android.Net.AndroidMessageHandler";

			var supportedAbis = "armeabi-v7a;arm64-v8a";
			proj.SetAndroidSupportedAbis (supportedAbis);
			proj.PackageReferences.Add (new Package() { Id = "System.Net.Http", Version = "*" });
			proj.MainActivity = proj.DefaultMainActivity.Replace ("//${AFTER_ONCREATE}", "var _ = new System.Net.Http.HttpClient ();");

			using (var b = CreateApkBuilder ()) {
				proj.SetProperty ("AndroidHttpClientHandlerType", expectedDefaultValue);
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				var intermediateOutputDir = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath);
				List<EnvironmentHelper.EnvironmentFile> envFiles = EnvironmentHelper.GatherEnvironmentFiles (intermediateOutputDir, supportedAbis, true);
				Dictionary<string, string> envvars = EnvironmentHelper.ReadEnvironmentVariables (envFiles);
				Assert.IsTrue (envvars.ContainsKey (httpClientHandlerVarName), $"Environment should contain '{httpClientHandlerVarName}'.");
				Assert.AreEqual (expectedDefaultValue, envvars[httpClientHandlerVarName]);

				proj.SetProperty ("AndroidHttpClientHandlerType", expectedUpdatedValue);
				Assert.IsTrue (b.Build (proj), "Second build should have succeeded.");
				envFiles = EnvironmentHelper.GatherEnvironmentFiles (intermediateOutputDir, supportedAbis, true);
				envvars = EnvironmentHelper.ReadEnvironmentVariables (envFiles);
				Assert.IsTrue (envvars.ContainsKey (httpClientHandlerVarName), $"Environment should contain '{httpClientHandlerVarName}'.");
				Assert.AreEqual (expectedUpdatedValue, envvars[httpClientHandlerVarName]);
			}
		}
	}
}
