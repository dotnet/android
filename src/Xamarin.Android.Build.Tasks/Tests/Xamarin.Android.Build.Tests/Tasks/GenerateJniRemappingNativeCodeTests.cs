#nullable enable
using System.IO;

using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests.Tasks;

[TestFixture]
public class GenerateJniRemappingNativeCodeTests : BaseTest
{
	[TestCase (false)]
	[TestCase (true)]
	public void EmitsRemappingForAllAbis (bool generateEmptyCode)
	{
		string outputDirectory = Path.Combine (Root, "temp", $"{nameof (EmitsRemappingForAllAbis)}-{generateEmptyCode}");
		Directory.CreateDirectory (outputDirectory);

		string remappingXml = Path.Combine (outputDirectory, "remapping.xml");
		if (!generateEmptyCode) {
			File.WriteAllText (remappingXml, """
				<replacements>
				  <replace-type from="a/B" to="c/D&quot;\path&#xE9;" />
				  <replace-method source-type="a/B" source-method-name="m" source-method-signature="()V"
				                  target-type="c/D" target-method-name="n" target-method-instance-to-static="false" />
				</replacements>
				""");
		}

		var task = new GenerateJniRemappingNativeCode {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			OutputDirectory = outputDirectory,
			SupportedAbis = ["armeabi-v7a", "arm64-v8a", "x86", "x86_64"],
			GenerateEmptyCode = generateEmptyCode,
			RemappingXmlFilePath = generateEmptyCode ? null : new TaskItem (remappingXml),
		};

		Assert.IsTrue (task.Execute (), "GenerateJniRemappingNativeCode should succeed.");

		foreach (string abi in task.SupportedAbis) {
			string source = File.ReadAllText (Path.Combine (outputDirectory, $"jni_remap.{abi}.ll"));
			Assert.That (source, Does.Contain ("@jni_remapping_type_replacements = "), abi);
			Assert.That (source, Does.Contain ("@jni_remapping_method_replacement_index = "), abi);
			Assert.That (source, Does.Contain ("%struct.JniRemappingIndexTypeEntry = type"), abi);
			if (generateEmptyCode) {
				Assert.That (source, Does.Not.Contain ("@mm_a_B = "), abi);
			} else {
				Assert.That (source, Does.Contain ("@mm_a_B = "), abi);
				Assert.That (source, Does.Contain ("c\"a/B\\00\""), abi);
				Assert.That (source, Does.Contain ("c\"()V\\00\""), abi);
				Assert.That (source, Does.Contain ("c\"c/D\\22\\5Cpath\\C3\\A9\\00\""), abi);
			}
		}
	}
}
