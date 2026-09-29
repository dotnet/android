using System.IO;

using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class GeneratePackageManagerJavaTests : BaseTest
	{
		[Test]
		public void CheckPackageManagerAssemblyOrder ()
		{
			string outputDirectory = Path.Combine (Root, "temp", nameof (CheckPackageManagerAssemblyOrder), "mono");
			Directory.CreateDirectory (outputDirectory);

			var task = new GeneratePackageManagerJava {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				MainAssembly = "linked/HelloAndroid.dll",
				OutputDirectory = outputDirectory,
			};

			Assert.IsTrue (task.Execute ());
			AssertFileContentsMatch (
				Path.Combine (XABuildPaths.TestAssemblyOutputDirectory, "Expected", "CheckPackageManagerAssemblyOrder.java"),
				Path.Combine (outputDirectory, "MonoPackageManager_Resources.java"));
		}
	}
}
