using System;
using System.IO;
using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture, NonParallelizable, Category ("UsesDevices")]
	public class ProfilerTests : DeviceTest
	{
		static XamarinAndroidApplicationProject proj;

		[OneTimeTearDown]
		public void FixtureTearDown ()
		{
			if (HasDevices && proj != null)
				RunAdbCommand ($"uninstall {proj.PackageName}");

			proj = null;
		}

		public static string [] ProfilerOptions () => new string [] {
			"log:heapshot", // Heapshot
			"log:sample", // Sample
			"log:nodefaults,exception,monitor,counter,sample", // Sample5_8
			"log:nodefaults,exception,monitor,counter,sample-real", // SampleReal
			"log:alloc", // Allocations
			"log:nodefaults,gc,gcalloc,gcroot,gcmove,counter", // Allocations5_8
			"log:nodefaults,gc,nogcalloc,gcroot,gcmove,counter", // LightAllocations
			"log:calls,alloc,heapshot", // All
		};

		[Test]
		public void ProfilerLogOptions_ShouldCreateMlpdFiles ([ValueSource (nameof (ProfilerOptions))] string profilerOption)
		{
			AssertHasDevices ();
			AssertCommercialBuild ();

			proj = new XamarinAndroidApplicationProject ();
			using (var builder = CreateApkBuilder ()) {
				Assert.IsTrue (builder.Install (proj), "Install should have succeeded.");
				string mlpdDestination = Path.Combine (Root, builder.ProjectDirectory, "profile.mlpd");
				if (File.Exists (mlpdDestination))
					File.Delete (mlpdDestination);

				RunAdbCommand ($"shell setprop debug.mono.profile {profilerOption}");
				Assert.True (builder.RunTarget (proj, "_Run"), "Project should have run.");
				Assert.True (WaitForActivityToStart (proj.PackageName, "MainActivity",
					Path.Combine (Root, builder.ProjectDirectory, "logcat.log"), 30), "Activity should have started.");

				// Wait for seven seconds after the activity is displayed to get profiler results
				WaitFor (7000);
				string profilerFileDir = null;
				foreach (var dir in GetOverrideDirectoryPaths (proj.PackageName)) {
					var listing = RunAdbCommand ($"shell run-as {proj.PackageName} ls {dir}");
					if (listing.Contains ("profile.mlpd")) {
						profilerFileDir = dir;
						break;
					}
				}

				Assert.IsTrue (!string.IsNullOrEmpty (profilerFileDir), $"Unable to locate 'profile.mlpd' in any override directories.");
				var profilerContent = RunAdbCommand ($"shell run-as {proj.PackageName} cat {profilerFileDir}/profile.mlpd");
				File.WriteAllText (mlpdDestination, profilerContent);
				RunAdbCommand ($"shell run-as {proj.PackageName} rm {profilerFileDir}/profile.mlpd");
				RunAdbCommand ($"shell am force-stop {proj.PackageName}");
				RunAdbCommand ("shell setprop debug.mono.profile \"\"");
				Assert.IsTrue (new FileInfo (mlpdDestination).Length > 5000,
					$"profile.mlpd file created with option '{profilerOption}' was not larger than 5 kb. The application may have crashed.");
				Assert.IsTrue (profilerContent.Contains ("String") && profilerContent.Contains ("Java"),
					$"profile.mlpd file created with option '{profilerOption}' did not contain expected data.");
			}
		}

	}
}
