using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	// Prepares the Android-specific workloads against the .NET SDK that
	// `eng/install-dotnet.{sh,ps1}` (Arcade's `eng/common/tools.{sh,ps1}`)
	// has already provisioned at `bin/$(Configuration)/dotnet/`:
	//   * Cleans stale runtime/workload NuGet directories.
	//   * Restores `package-download.proj` to pull down the Mono Android
	//     runtime packs and the Mono/Emscripten workload manifest packages.
	//   * Copies the workload manifests into the local SDK's `sdk-manifests/`
	//     so the SDK can resolve the workloads against the locally installed
	//     runtime packs.
	//
	// The SDK itself is installed by Arcade's `eng/common/tools.{sh,ps1}`
	// via `eng/install-dotnet.{sh,ps1}` invoked from the `Prepare` MSBuild
	// target (Windows) and `make prepare` (Unix). This step assumes the SDK
	// already exists at `Configurables.Paths.DotNetPreviewPath`.
	class Step_PrepareDotNetWorkloads : StepWithDownloadProgress, IBuildInventoryItem
	{
		public string BuildToolName => "dotnet-preview";
		public string BuildToolVersion => Context.Instance.Properties.GetRequiredValue (KnownProperties.MicrosoftDotnetSdkInternalPackageVersion);

		public Step_PrepareDotNetWorkloads ()
			: base ("Prepare .NET workloads against the locally installed SDK")
		{ }

		protected override async Task<bool> Execute (Context context)
		{
			var dotnetPath = Configurables.Paths.DotNetPreviewPath;
			dotnetPath = dotnetPath.TrimEnd (new char [] { Path.DirectorySeparatorChar });

			var dotnetTool = Configurables.Paths.DotNetPreviewTool;
			if (!Directory.Exists (dotnetPath)) {
				Log.ErrorLine ($"Expected .NET SDK at '{dotnetPath}' but the directory does not exist. Run `eng/install-dotnet.{{sh,ps1}}` (or `make prepare`) first.");
				return false;
			}

			AddToInventory ();

			// Delete all relevant NuGet package install directories, as we could possibly be using a new runtime commit with a previously installed version (6.0.0)
			var runtimeDirs = Directory.GetDirectories (Configurables.Paths.XAPackagesDir, "microsoft.netcore.app.runtime.mono.android*");
			var packageDirsToRemove = new List<string> (runtimeDirs);
			packageDirsToRemove.Add (Configurables.Paths.MicrosoftNETWorkloadMonoPackageDir);
			foreach (var packageDir in packageDirsToRemove) {
				if (Directory.Exists (packageDir)) {
					Utilities.DeleteDirectory (packageDir);
				}
			}

			// Install runtime packs associated with the SDK previously installed.
			var packageDownloadProj = Path.Combine (BuildPaths.XamarinAndroidSourceRoot, "build-tools", "xaprepare", "xaprepare", "package-download.proj");
			var logPathBase = Path.Combine (Configurables.Paths.BuildBinDir, $"msbuild-{context.BuildTimeStamp}-download-runtime-packs");

			const int maxAttempts = 3;
			const int initialBackoffDelayMilliseconds = 2000;
			bool restoreSucceeded = false;
			for (int attempt = 1; attempt <= maxAttempts; attempt++) {
				var logPath = $"{logPathBase}-attempt{attempt}.binlog";
				var runner = new ProcessRunner (dotnetTool, "restore",
					packageDownloadProj,
					"--configfile", Path.Combine (BuildPaths.XamarinAndroidSourceRoot, "NuGet.config"),
					$"-bl:{logPath}",
					"--verbosity", "normal"
				) {
					EchoStandardOutput = true,
					EchoStandardError = true,
				};
				if (runner.Run ()) {
					restoreSucceeded = true;
					break;
				}
				if (attempt < maxAttempts) {
					Log.WarningLine ($"Failed to restore runtime packs (attempt {attempt}/{maxAttempts}), retrying...");
					var delayMilliseconds = initialBackoffDelayMilliseconds * (1 << (attempt - 1));
					await Task.Delay (delayMilliseconds);
				}
			}
			if (!restoreSucceeded) {
				Log.ErrorLine ($"Failed to restore runtime packs using '{packageDownloadProj}' after {maxAttempts} attempts.");
				return false;
			}

			var sdk_manifests = Path.Combine (dotnetPath, "sdk-manifests");

			// Copy the WorkloadManifest.* files from the latest Microsoft.NET.Workload.* listed in package-download.proj
			var dotnets = new [] { "net6", "net7", "net8", "net9", "net10", "current" };
			foreach (var dotnet in dotnets) {
				var destination = Path.Combine (sdk_manifests,
					context.Properties.GetRequiredValue (KnownProperties.DotNetMonoManifestVersionBand),
					$"microsoft.net.workload.mono.toolchain.{dotnet}",
					context.Properties.GetRequiredValue (KnownProperties.MicrosoftNETWorkloadMonoToolChainPackageVersion));
				Utilities.DeleteDirectory (destination, recurse: true);
				foreach (var file in Directory.GetFiles (string.Format (Configurables.Paths.MicrosoftNETWorkloadMonoToolChainDir, dotnet), "*")) {
					Utilities.CopyFileToDir (file, destination);
				}
				destination = Path.Combine (sdk_manifests,
					context.Properties.GetRequiredValue (KnownProperties.DotNetEmscriptenManifestVersionBand),
					$"microsoft.net.workload.emscripten.{dotnet}",
					context.Properties.GetRequiredValue (KnownProperties.MicrosoftNETWorkloadEmscriptenPackageVersion));
				Utilities.DeleteDirectory (destination, recurse: true);
				foreach (var file in Directory.GetFiles (string.Format (Configurables.Paths.MicrosoftNETWorkloadEmscriptenDir, dotnet), "*")) {
					Utilities.CopyFileToDir (file, destination);
				}
			}

			return true;
		}

		public void AddToInventory ()
		{
			if (!string.IsNullOrEmpty (BuildToolName) && !string.IsNullOrEmpty (BuildToolVersion) && !Context.Instance.BuildToolsInventory.ContainsKey (BuildToolName)) {
				Context.Instance.BuildToolsInventory.Add (BuildToolName, BuildToolVersion);
			}
		}
	}
}
