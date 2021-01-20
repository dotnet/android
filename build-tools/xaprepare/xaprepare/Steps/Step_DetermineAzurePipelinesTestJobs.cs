using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	class Step_DetermineAzurePipelinesTestJobs : Step
	{
		public Step_DetermineAzurePipelinesTestJobs ()
			: base ("Determine Azure Pipelines test jobs to run from a merge commit")
		{}

		protected override async Task<bool> Execute (Context context)
		{
			Log.StatusLine ("Determining test jobs to run...");

			string commitRevision = Environment.GetEnvironmentVariable ("BUILD_SOURCEVERSION");
			string commitMessage = Environment.GetEnvironmentVariable ("BUILD_SOURCEVERSIONMESSAGE");
			if (string.IsNullOrEmpty (commitRevision) || string.IsNullOrEmpty (commitMessage)) {
				Log.ErrorLine ("One or more source version variable values were empty:");
				Log.ErrorLine ($"BUILD_SOURCEVERSION='{commitRevision}' BUILD_SOURCEVERSIONMESSAGE='{commitMessage}'.");
				return false;
			}

			// Assume we're building a merge commit as part of an Azure Pipelines PR build. Otherwise, this step will fail.
			//  Example: Merge 0b66502c8b9f33cbb8d21b2dab7c100629aec081 into 0bef8aa5cd74d83d77c4e2b3f63975a0deb804b3
			var commitMessagePieces = commitMessage.Split (new string [] { " " }, StringSplitOptions.RemoveEmptyEntries);
			if (string.IsNullOrEmpty (commitMessagePieces [3])) {
				Log.ErrorLine ($"Unable to parse merge commit message from: '{commitMessage}'.");
				return false;
			}

			var git = new GitRunner (context);
			var filesChanged = await git.RunCommandForOutputAsync (BuildPaths.XamarinAndroidSourceRoot, "diff", "--name-only", commitRevision, commitMessagePieces [3]);
			if (filesChanged == null || filesChanged.Count < 1) {
				Log.ErrorLine ($"Unable to determine if any files were changed in this PR.");
				return false;
			}

			var testAreas = new HashSet<string> ();
			foreach (string file in filesChanged) {
				Log.InfoLine ($"Detected change in file: '{file}'.");
				// Compare files changed against common areas requiring additional test scope.
				// MSBuild: Runs all legacy and One .NET Xamarin.Android.Build.Task tests
				// MSBuildDevice: Runs all MSBuildDeviceIntegration tests
				// BCL: Runs BCL tests on emulator
				// TimeZone: Runs timezone unit tests on emulator
				// Designer: Runs designer integration tests
				if (file == ".external" || file == "Configuration.props" || file.Contains ("build-tools/")) {
					testAreas.Add ("MSBuild");
					testAreas.Add ("MSBuildDevice");
					testAreas.Add ("BCL");
					testAreas.Add ("TimeZone");
					testAreas.Add ("Designer");
				}

				if (file.Contains ("external/Java.Interop")) {
					testAreas.Add ("MSBuild");
					testAreas.Add ("MSBuildDevice");
					testAreas.Add ("Designer");
				}

				if (file.Contains ("external/proguard")) {
					testAreas.Add ("MSBuild");
					testAreas.Add ("MSBuildDevice");
				}

				if (file.Contains ("external/sqlite")) {
					testAreas.Add ("MSBuildDevice");
				}

				if (file.Contains ("src/aapt2")) {
					testAreas.Add ("MSBuild");
				}

				if (file.Contains ("src/apksigner")) {
					testAreas.Add ("MSBuild");
					testAreas.Add ("MSBuildDevice");
				}

				if (file.Contains ("src/bundletool")) {
					testAreas.Add ("MSBuild");
					testAreas.Add ("MSBuildDevice");
				}

				if (file.Contains ("src/java-runtime")) {
					testAreas.Add ("MSBuildDevice");
				}

				if (file.Contains ("src/manifestmerger")) {
					testAreas.Add ("MSBuild");
					testAreas.Add ("MSBuildDevice");
				}

				if (file.Contains ("src/Microsoft.Android.Sdk.ILLink")) {
					testAreas.Add ("MSBuild");
				}

				if (file.Contains ("src/Mono.Android")) {
					testAreas.Add ("Designer");
				}

				if (file.Contains ("src/monodroid")) {
					testAreas.Add ("MSBuildDevice");
					testAreas.Add ("Designer");
				}

				if (file.Contains ("src/proguard")) {
					testAreas.Add ("MSBuild");
					testAreas.Add ("MSBuildDevice");
				}

				if (file.Contains ("src/r8")) {
					testAreas.Add ("MSBuild");
					testAreas.Add ("MSBuildDevice");
				}

				if (file.Contains ("src/Xamarin.Android.Build.Tasks")) {
					testAreas.Add ("MSBuild");
					testAreas.Add ("MSBuildDevice");
					testAreas.Add ("Designer");
				}

				if (file.Contains ("src/Xamarin.Android.Tools.Aidl")) {
					testAreas.Add ("MSBuild");
				}

				if (file.Contains ("src/Xamarin.Android.Tools.JavadocImporter")) {
					testAreas.Add ("MSBuild");
				}

				if (file.Contains ("src-ThirdParty/android-platform-tools-base")) {
					testAreas.Add ("MSBuild");
					testAreas.Add ("MSBuildDevice");
				}

				if (file.Contains ("src-ThirdParty/bazel")) {
					testAreas.Add ("MSBuildDevice");
				}

				if (file.Contains ("tests/BCL-Tests")) {
					testAreas.Add ("BCL");
				}

				if (file.Contains ("tests/MSBuildDeviceIntegration")) {
					testAreas.Add ("MSBuildDevice");
					testAreas.Add ("TimeZone");
				}

				if (file.Contains ("tests/msbuild-times-reference")) {
					testAreas.Add ("MSBuildDevice");
				}

			}

			Log.MessageLine ($"##vso[task.setvariable variable=TestAreas;isOutput=true]{string.Join (",", testAreas)}");
			return true;
		}

	}
}
