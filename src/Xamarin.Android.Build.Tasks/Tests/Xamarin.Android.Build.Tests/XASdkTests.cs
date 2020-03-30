using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[NonParallelizable] // On MacOS, parallel /restore causes issues
	[Category ("Node-2")]
	public class XASdkTests : BaseTest
	{
		static readonly string SdkVersion = Assembly.GetExecutingAssembly ()
			.GetCustomAttributes<AssemblyMetadataAttribute> ()
			.Where (attr => attr.Key == "SdkVersion")
			.Select (attr => attr.Value)
			.FirstOrDefault () ?? "0.0.1";

		[Test]
		public void BuildXASdkProject ([Values (false, true)] bool isRelease)
		{
			var proj = new XASdkProject (SdkVersion) {
				IsRelease = isRelease
			};
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
			}
		}

		[Test]
		[Category ("SmokeTests")]
		public void DotNetPackageXASdkProject ([Values (false, true)] bool isRelease)
		{
			var proj = new XASdkProject (SdkVersion) {
				IsRelease = isRelease
			};
			var relativeProjDir = Path.Combine ("temp", TestName);
			var fullProjDir = Path.Combine (Root, relativeProjDir);
			TestOutputDirectories [TestContext.CurrentContext.Test.ID] = fullProjDir;
			var files = proj.Save ();
			proj.Populate (relativeProjDir, files);
			proj.CopyNuGetConfig (relativeProjDir);

			var dotnet = new DotNetCLI ();
			Assert.IsTrue (dotnet.Build (Path.Combine (fullProjDir, proj.ProjectFilePath), proj.Configuration, "SignAndroidPackage"));
		}
	}
}
