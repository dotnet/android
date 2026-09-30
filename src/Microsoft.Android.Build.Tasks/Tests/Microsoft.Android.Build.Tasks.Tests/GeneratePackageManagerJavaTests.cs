using System.IO;

using NUnit.Framework;
using Microsoft.Android.Tasks;

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
			Assert.AreEqual (
				File.ReadAllText (Path.Combine (TestContext.CurrentContext.TestDirectory, "Expected", "CheckPackageManagerAssemblyOrder.java")).Replace ("\r\n", "\n"),
				File.ReadAllText (Path.Combine (outputDirectory, "MonoPackageManager_Resources.java")).Replace ("\r\n", "\n"));
		}
	}
}
