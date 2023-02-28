using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Category ("DotNetIgnore")] // .csproj files are legacy projects that won't build under dotnet
	public class DeleteBinObjTest : DeviceTest
	{
		const string BaseUrl = "https://github.com/dellis1972/xamarin-android-unittest-files/blob/main/";
		readonly DownloadedCache Cache = new DownloadedCache ();

		string HostOS => IsWindows ? "Windows" : "Darwin";
		void RunTest (string name, string sln, string csproj, string version, string revision, string packageName, string javaPackageName, bool isRelease)
		{
			var configuration = isRelease ? "Release" : "Debug";
			var zipPath = Cache.GetAsFile ($"{BaseUrl}{name}-{version}-{HostOS}-{revision}.7z?raw=true");
			using (var builder = CreateApkBuilder (Path.Combine ("temp", TestName)))
			using (var zip = SevenZipHelper.Open (zipPath, FileMode.Open)) {
				builder.AutomaticNuGetRestore = false;

				if (!builder.TargetFrameworkExists ("v9.0")) {
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
				var parameters = new List<string> {
					"Configuration=" + configuration,
					// Move the $(IntermediateOutputPath) directory to match zips
					"IntermediateOutputPath=" + Path.Combine ("obj", isRelease ? "Release" : "Debug", "90") + Path.DirectorySeparatorChar
				};
				if (isRelease || !CommercialBuildAvailable) {
					parameters.Add (KnownProperties.AndroidSupportedAbis + "=\"armeabi-v7a;x86;x86_64\"");
				} else {
					parameters.Add (KnownProperties.AndroidSupportedAbis + "=\"armeabi-v7a;arm64-v8a;x86;x86_64\"");
				}
				if (HasDevices) {
					Assert.IsTrue (builder.Install (project, doNotCleanupOnUpdate: true, parameters: parameters.ToArray (), saveProject: false),
						"Install should have succeeded.");
					ClearAdbLogcat ();
					if (CommercialBuildAvailable)
						Assert.True (builder.RunTarget (project, "_Run", doNotCleanupOnUpdate: true, parameters: parameters.ToArray ()), "Project should have run.");
					else
						AdbStartActivity ($"{packageName}/{javaPackageName}.MainActivity");
					Assert.True (WaitForActivityToStart (packageName, "MainActivity",
						Path.Combine (Root, builder.ProjectDirectory, "logcat.log"), 30), "Activity should have started.");
				} else {
					Assert.IsTrue (builder.Build (project, doNotCleanupOnUpdate: true, parameters: parameters.ToArray (), saveProject: false),
						"Build should have succeeded.");
				}
			}
		}

		[Test, Category ("UsesDevice")]
		public void HelloForms15_9 ([Values (false, true)] bool isRelease)
		{
			RunTest ("HelloForms",
				"HelloForms.sln",
				Path.Combine ("HelloForms.Android", "HelloForms.Android.csproj"),
				"15.9",
				"ecb13a9",
				"com.companyname",
				"crc6450e568c951913723",
				isRelease);
		}

		[Test, Category ("UsesDevice")]
		public void HelloForms16_4 ([Values (false, true)] bool isRelease)
		{
			RunTest ("HelloForms",
				"HelloForms.sln",
				Path.Combine ("HelloForms.Android", "HelloForms.Android.csproj"),
				"16.4",
				"dea8b8d",
				"com.companyname",
				"crc6450e568c951913723",
				isRelease);
		}
	}
}
