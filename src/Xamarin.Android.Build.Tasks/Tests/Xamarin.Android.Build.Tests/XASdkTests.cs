using System.IO;
using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[NonParallelizable] // On MacOS, parallel /restore causes issues
	public class XASdkTests : BaseTest
	{
		[Test]
		public void BuildXASdkProject ([Values (false, true)] bool isRelease)
		{
			var proj = new XASdkProject ("0.0.1") {
				IsRelease = isRelease
			};
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
			}
		}

		[Test]
		public void DotNetPackageXASdkProject ([Values (false, true)] bool isRelease)
		{
			var proj = new XASdkProject ("0.0.1") {
				IsRelease = isRelease
			};
			var relativeProjDir = Path.Combine ("temp", TestName);
			TestOutputDirectories [TestContext.CurrentContext.Test.ID] = relativeProjDir;
			var files = proj.Save ();
			proj.Populate (relativeProjDir, files);
			proj.CopyNuGetConfig (relativeProjDir);

			var dotnet = new DotNetCLI ();
			Assert.IsTrue (dotnet.Build (Path.Combine (Root, relativeProjDir, proj.ProjectFilePath), proj.Configuration, "SignAndroidPackage"));
		}
	}
}
