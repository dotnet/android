#nullable enable
using System.IO;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests.Tasks;

[TestFixture]
public class GenerateNativeApplicationConfigSourcesTests : BaseTest
{
	[Test]
	public void KeepsOnlyFormatMarkerInEnvironmentObjects ()
	{
		string outputRoot = Path.Combine (Root, "temp", TestName);
		var task = new GenerateNativeApplicationConfigSources {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			EnvironmentOutputDirectory = outputRoot,
			SupportedAbis = ["arm64-v8a", "armeabi-v7a", "x86_64", "x86"],
			EmitLlvmIrComments = true,
		};
		Assert.IsTrue (task.Execute ());
		foreach (string abi in task.SupportedAbis) {
			string source = File.ReadAllText (Path.Combine (outputRoot, $"environment.{abi}.ll"));
			Assert.That (source, Does.Not.Contain ("assembly_store"));
			Assert.That (source, Does.Not.Contain ("compressed_assembly"));
			Assert.That (source, Does.Contain ("format_tag"));
			Assert.That (source, Does.Not.Contain ("application_config"));
			Assert.That (source, Does.Not.Contain ("app_environment_variables"));
			Assert.That (source, Does.Not.Contain ("dso_cache"));
			Assert.That (source, Does.Not.Contain ("init_runtime_property_names"));
			Assert.That (source, Does.Not.Contain ("jni_remapping_method_replacement_index"));
			Assert.That (source, Does.Not.Contain ("marshal_methods_enabled"));
		}
	}
}
