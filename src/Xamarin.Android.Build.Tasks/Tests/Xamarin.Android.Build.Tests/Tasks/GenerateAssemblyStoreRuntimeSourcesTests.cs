#nullable enable
using System.IO;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests.Tasks;

[TestFixture]
public class GenerateAssemblyStoreRuntimeSourcesTests : BaseTest
{
	[Test]
	public void KeepsOnlyAssemblyStoreStateInEnvironmentObjects ()
	{
		string outputRoot = Path.Combine (Root, "temp", TestName);
		var typeMap = new TaskItem (Path.Combine (outputRoot, "typemap", "_Test.TypeMap.dll"));
		typeMap.SetMetadata ("Abi", "arm64-v8a");
		var task = new GenerateNativeApplicationConfigSources {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			ResolvedAssemblies = [new TaskItem ("linked/Mono.Android.dll"), typeMap],
			AdditionalResolvedAssemblies = [new TaskItem (typeMap.ItemSpec)],
			EnvironmentOutputDirectory = outputRoot,
			SupportedAbis = ["arm64-v8a", "armeabi-v7a", "x86_64", "x86"],
			EmitLlvmIrComments = true,
		};
		Assert.IsTrue (task.Execute ());
		foreach (string abi in task.SupportedAbis) {
			string source = File.ReadAllText (Path.Combine (outputRoot, $"environment.{abi}.ll"));
			Assert.That (source, Does.Contain ("assembly_store_bundled_assemblies"));
			Assert.That (source, Does.Contain ("[2 x %struct.AssemblyStoreSingleAssemblyRuntimeData] zeroinitializer"));
			Assert.That (source, Does.Contain ("assembly_store"));
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
