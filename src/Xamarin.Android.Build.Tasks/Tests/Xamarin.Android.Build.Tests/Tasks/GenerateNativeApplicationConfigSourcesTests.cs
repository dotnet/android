#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tasks.LLVMIR;
using Xamarin.Android.Tools;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests.Tasks;

[TestFixture]
public class GenerateNativeApplicationConfigSourcesTests : BaseTest
{
	[TestCase (AndroidTargetArch.Arm64)]
	[TestCase (AndroidTargetArch.X86_64)]
	public void LocalDateTimeOffsetIsReservedForTheDevice (AndroidTargetArch arch)
	{
		const string name = "System.TimeZoneInfo.LocalDateTimeOffset";
		string Generate (string? offset)
		{
			var properties = new Dictionary<string, string> { ["other"] = "value" };
			if (offset != null) {
				properties [name] = offset;
			}
			var log = new TaskLoggingHelper (new MockBuildEngine (TestContext.Out), "test");
			var generator = new ApplicationConfigNativeAssemblyGeneratorCLR (
				new Dictionary<string, string> (), new Dictionary<string, string> (),
				properties, log) {
				AndroidPackageName = "com.microsoft.android.timezonetest",
			};
			var module = generator.Construct ();
			var globals = module.GlobalVariables;
			Assert.IsNotNull (globals);
			Assert.That (globals.Single (v => v.Name == "init_runtime_property_names").Value,
				Is.EqualTo (new [] { "HOST_RUNTIME_CONTRACT", "RUNTIME_IDENTIFIER", "APP_CONTEXT_BASE_DIRECTORY", name, "other" }));
			Assert.That (globals.Single (v => v.Name == "init_runtime_property_values").Value,
				Is.EqualTo (new string? [] { null, null, null, null, "value" }));
			using var writer = new StringWriter ();
			LlvmIrGenerator.Create (arch, "environment.ll").Generate (writer, module);
			return writer.ToString ();
		}

		string output = Generate ("3600");
		Assert.AreEqual (output, Generate ("-18000"), "The build-time offset must not be emitted.");
		Assert.AreEqual (output, Generate (null), "The host property must be reserved even without a configured offset.");
		Assert.That (output, Does.Contain (name));
		Assert.That (output, Does.Contain ("@init_runtime_property_names ="));
		Assert.That (output, Does.Contain ("@init_runtime_property_values ="));
	}

	[TestCase (false)]
	[TestCase (true)]
	public void HaveAssemblyStoreIsEmittedForCoreCLR (bool haveAssemblyStore)
	{
		string outputRoot = Path.Combine (Root, "temp", $"{nameof (HaveAssemblyStoreIsEmittedForCoreCLR)}-{haveAssemblyStore}");
		string monoAndroidPath = Path.Combine (TestEnvironment.MonoAndroidFrameworkDirectory, "Mono.Android.dll");
		FileAssert.Exists (monoAndroidPath);

		var task = new GenerateNativeApplicationConfigSources {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			ResolvedAssemblies = [new TaskItem (monoAndroidPath)],
			EnvironmentOutputDirectory = Path.Combine (outputRoot, "android"),
			SupportedAbis = ["arm64-v8a"],
			AndroidPackageName = "com.microsoft.android.assemblystoretest",
			EnablePreloadAssembliesDefault = false,
			TargetsCLR = true,
			AndroidRuntime = "CoreCLR",
			UseAssemblyStore = haveAssemblyStore,
		};

		Assert.IsTrue (task.Execute (), "GenerateNativeApplicationConfigSources should succeed.");

		var environmentFiles = EnvironmentHelper.GatherEnvironmentFiles (
			outputRoot,
			"arm64-v8a",
			required: true,
			runtime: AndroidRuntime.CoreCLR
		);
		var config = (EnvironmentHelper.ApplicationConfig_CoreCLR)EnvironmentHelper.ReadApplicationConfig (environmentFiles, AndroidRuntime.CoreCLR);
		Assert.AreEqual (haveAssemblyStore, config.have_assembly_store);
		Assert.AreEqual (4, config.number_of_runtime_properties);
	}
}
