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
		[Category ("SmokeTests")]
		public void DotNetBuild ([Values (false, true)] bool isRelease)
		{
			var proj = new XASdkProject (SdkVersion) {
				IsRelease = isRelease
			};
			var dotnet = CreateDotNetBuilder (proj);
			Assert.IsTrue (dotnet.Build (), "`dotnet build` should succeed");
		}

		[Test]
		[Category ("SmokeTests")]
		public void DotNetPublish ([Values (false, true)] bool isRelease)
		{
			var proj = new XASdkProject (SdkVersion) {
				IsRelease = isRelease
			};
			proj.SetProperty (KnownProperties.AndroidLinkMode, AndroidLinkMode.None.ToString ());
			var dotnet = CreateDotNetBuilder (proj);
			Assert.IsTrue (dotnet.Publish (), "`dotnet publish` should succeed");
		}

		DotNetCLI CreateDotNetBuilder (XASdkProject project)
		{
			var relativeProjDir = Path.Combine ("temp", TestName);
			var fullProjDir = Path.Combine (Root, relativeProjDir);
			TestOutputDirectories [TestContext.CurrentContext.Test.ID] = fullProjDir;
			var files = project.Save ();
			project.Populate (relativeProjDir, files);
			project.CopyNuGetConfig (relativeProjDir);
			return new DotNetCLI (project, Path.Combine (fullProjDir, project.ProjectFilePath));
		}
	}
}
