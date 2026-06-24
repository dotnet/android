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
		public void BuildApplicationWithMonoEnvironment ([Values ("", "Normal", "Offline")] string sequencePointsMode, [Values (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
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
			app.SetRuntimeIdentifiers (supportedAbis.Split (';'));
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

	}
}
