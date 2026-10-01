#nullable enable
using System.IO;

using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests.Tasks;

[TestFixture]
public class GenerateCompressedAssembliesNativeSourceFilesTests : BaseTest
{
	[Test]
	public void EmitsEmptyDescriptorsForAllAbis ()
	{
		string outputDirectory = Path.Combine (Root, "temp", nameof (EmitsEmptyDescriptorsForAllAbis));
		var task = new GenerateCompressedAssembliesNativeSourceFiles {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			EnvironmentOutputDirectory = outputDirectory,
			SupportedAbis = ["armeabi-v7a", "arm64-v8a", "x86", "x86_64"],
			ProjectFullPath = Path.Combine (Root, "test.csproj"),
			Debug = true,
			EnableCompression = false,
		};

		Assert.IsTrue (task.Execute (), "GenerateCompressedAssembliesNativeSourceFiles should succeed.");

		foreach (string abi in task.SupportedAbis) {
			string source = File.ReadAllText (Path.Combine (outputDirectory, $"compressed_assemblies.{abi}.ll"));
			Assert.That (source, Does.Contain ("%struct.CompressedAssemblyDescriptor = type"), abi);
			Assert.That (source, Does.Contain ("@compressed_assembly_count = "), abi);
			Assert.That (source, Does.Contain ("@compressed_assembly_descriptors = "), abi);
			Assert.That (source, Does.Contain ("@uncompressed_assemblies_data_size = "), abi);
			Assert.That (source, Does.Contain ("@uncompressed_assemblies_data_buffer = "), abi);
			Assert.That (source, Does.Contain ("zeroinitializer"), abi);
		}
	}
}
