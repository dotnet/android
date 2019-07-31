using NUnit.Framework;
using System.IO;
using Xamarin.ProjectTools;
using Xamarin.Tools.Zip;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Parallelizable (ParallelScope.Children)]
	public class DeleteBinObjTest : BaseTest
	{
		const string BaseUrl = "https://xamjenkinsartifact.azureedge.net/mono-jenkins/xamarin-android-test/";
		readonly DownloadedCache Cache = new DownloadedCache ();

		string HostOS => IsWindows ? "Windows" : "Darwin";

		void RunTest (string name, string sln, string csproj, string version, string revision, bool isRelease)
		{
			var configuration = isRelease ? "Release" : "Debug";
			var zipPath = Cache.GetAsFile ($"{BaseUrl}{name}-{version}-{HostOS}-{revision}.zip");
			using (var builder = CreateApkBuilder (Path.Combine ("temp", TestName)))
			using (var zip = ZipArchive.Open (zipPath, FileMode.Open)) {
				builder.AutomaticNuGetRestore = false;

				if (!builder.TargetFrameworkExists("v9.0"))  {
					Assert.Ignore ("TargetFrameworkVersion=v9.0 required for this test.");
					return;
				}

				var projectDir = Path.Combine (Root, builder.ProjectDirectory);
				if (Directory.Exists (projectDir))
					Directory.Delete (projectDir, recursive: true);
				zip.ExtractAll (projectDir);

				var solution = new ExistingProject {
					IsRelease = isRelease,
					ProjectFilePath = Path.Combine (projectDir, sln),
				};
				// RestoreNoCache will bypass a global cache on CI machines
				Assert.IsTrue (builder.Restore (solution, doNotCleanupOnUpdate: true, parameters: new [] { "RestoreNoCache=True" }), "Restore should have succeeded.");

				var project = new ExistingProject {
					IsRelease = isRelease,
					ProjectFilePath = Path.Combine (projectDir, csproj),
				};
				var parameters = new [] { "Configuration=" + configuration };
				if (HasDevices) {
					Assert.IsTrue (builder.Install (project, doNotCleanupOnUpdate: true, parameters: parameters, saveProject: false),
						"Install should have succeeded.");
				} else {
					Assert.IsTrue (builder.Build (project, doNotCleanupOnUpdate: true, parameters: parameters, saveProject: false),
						"Build should have succeeded.");
				}
			}
		}

		[Test]
		public void HelloForms ([Values (false, true)] bool isRelease)
		{
			RunTest (nameof (HelloForms), "HelloForms.sln", Path.Combine ("HelloForms.Android", "HelloForms.Android.csproj"), "15.9", "ecb13a9", isRelease);
		}
	}
}
