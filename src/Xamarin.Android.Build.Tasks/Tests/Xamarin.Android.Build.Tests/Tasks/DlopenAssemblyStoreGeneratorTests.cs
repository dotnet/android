using System.IO;

using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests.Tasks;

[TestFixture]
public class DlopenAssemblyStoreGeneratorTests : BaseTest
{
	[Test]
	public void DataOnlyStoreExportsPayloadBounds ()
	{
		string outputRoot = Path.Combine (Root, "temp", nameof (DataOnlyStoreExportsPayloadBounds));
		Directory.CreateDirectory (outputRoot);
		string payload = Path.Combine (outputRoot, "assembly-store.so");
		File.WriteAllBytes (payload, [0x58, 0x41, 0x42, 0x41]);

		string host = TestEnvironment.IsWindows ? "Windows" : TestEnvironment.IsMacOS ? "Darwin" : "Linux";
		string binUtils = Path.Combine (TestEnvironment.AndroidMSBuildDirectory, host, "binutils", "bin");
		var task = new GeneratePackageManagerJava {
			BuildEngine = new MockBuildEngine (TestContext.Out),
		};

		string library = DlopenAssemblyStoreGenerator.WrapIt (
			task.Log, binUtils, outputRoot, AndroidTargetArch.Arm64, payload, "libassembly-store.so"
		);

		FileAssert.Exists (library);
		Assert.IsTrue (ELFHelper.LibraryHasPublicSymbol (task.Log, library, DlopenAssemblyStoreGenerator.PayloadStartSymbol));
		Assert.IsTrue (ELFHelper.LibraryHasPublicSymbol (task.Log, library, DlopenAssemblyStoreGenerator.PayloadEndSymbol));
	}
}
