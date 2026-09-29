using System.Collections.Generic;
using System.IO;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests.Tasks;

[TestFixture]
public class CollectCompressedAssemblyInfoTests : BaseTest
{
	[TestCase (true, true)]
	[TestCase (false, false)]
	[TestCase (false, true)]
	public void RegistersCompressionMetadataWithoutNativeSources (bool debug, bool enableCompression)
	{
		string outputRoot = Path.Combine (Root, "temp", nameof (RegistersCompressionMetadataWithoutNativeSources), $"{debug}-{enableCompression}");
		Directory.CreateDirectory (outputRoot);
		string assemblyPath = Path.Combine (outputRoot, "Sample.dll");
		string projectPath = Path.Combine (outputRoot, "Sample.csproj");
		File.WriteAllBytes (assemblyPath, [1, 2, 3, 4]);
		var assembly = new TaskItem (assemblyPath);
		assembly.SetMetadata ("Abi", "arm64-v8a");

		var engine = new MockBuildEngine (TestContext.Out);
		var task = new CollectCompressedAssemblyInfo {
			BuildEngine = engine,
			ResolvedAssemblies = [assembly],
			SupportedAbis = ["arm64-v8a"],
			ProjectFullPath = projectPath,
			Debug = debug,
			EnableCompression = enableCompression,
		};

		Assert.IsTrue (task.Execute ());
		var info = ((IBuildEngine4)engine).GetRegisteredTaskObjectAssemblyLocal<
			Dictionary<AndroidTargetArch, Dictionary<string, CompressedAssemblyInfo>>> (
				CompressedAssemblyInfo.GetKey (projectPath), RegisteredTaskObjectLifetime.Build);
		if (!debug && enableCompression) {
			Assert.IsNotNull (info);
			Assert.AreEqual (0, info [AndroidTargetArch.Arm64] ["Sample.dll"].DescriptorIndex);
		} else {
			Assert.IsNull (info);
		}
		Assert.IsEmpty (Directory.GetFiles (outputRoot, "*.ll", SearchOption.AllDirectories));
	}
}
