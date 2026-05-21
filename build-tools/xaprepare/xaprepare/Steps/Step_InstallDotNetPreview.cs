using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	class Step_InstallDotNetPreview : StepWithDownloadProgress, IBuildInventoryItem
	{
		public string BuildToolName => "dotnet-preview";
		public string BuildToolVersion => Context.Instance.Properties.GetRequiredValue (KnownProperties.MicrosoftDotnetSdkInternalPackageVersion);

		public Step_InstallDotNetPreview ()
			: base ("Install required .NET Preview SDK locally")
		{ }

		protected override async Task<bool> Execute (Context context)
		{
			var dotnetPath = Configurables.Paths.DotNetPreviewPath;
			dotnetPath = dotnetPath.TrimEnd (new char [] { Path.DirectorySeparatorChar });

			// Check if a local SDK archive was specified
			if (!String.IsNullOrEmpty (context.LocalDotNetSdkArchive)) {
				if (!await InstallDotNetFromLocalArchiveAsync (context, dotnetPath, context.LocalDotNetSdkArchive!)) {
					Log.ErrorLine ($"Installation of dotnet SDK from local archive '{context.LocalDotNetSdkArchive}' failed.");
					return false;
				}
			} else if (!await InstallDotNetAsync (context, dotnetPath, BuildToolVersion, useCachedInstallScript: true) &&
					!await InstallDotNetAsync (context, dotnetPath, BuildToolVersion, useCachedInstallScript: false)) {
				Log.ErrorLine ($"Installation of dotnet SDK '{BuildToolVersion}' failed.");
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
				var runner = new ProcessRunner (Configurables.Paths.DotNetPreviewTool, "restore",
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

		async Task<bool> DownloadDotNetInstallScript (Context context, string dotnetScriptPath, Uri dotnetScriptUrl, bool useCachedInstallScript)
		{
			string tempDotnetScriptPath = dotnetScriptPath + "-tmp";
			Utilities.DeleteFile (tempDotnetScriptPath);

			Log.StatusLine ("Downloading dotnet-install script...");

			if (useCachedInstallScript && File.Exists (dotnetScriptPath)) {
				Log.StatusLine ($"Using cached installation script found in '{dotnetScriptPath}'");
				return true;
			}
			Utilities.DeleteFile (dotnetScriptPath);

			Log.StatusLine ($"  {context.Characters.Link} {dotnetScriptUrl}", ConsoleColor.White);
			await Utilities.Download (dotnetScriptUrl, tempDotnetScriptPath, DownloadStatus.Empty);

			if (File.Exists (tempDotnetScriptPath)) {
				Utilities.CopyFile (tempDotnetScriptPath, dotnetScriptPath);
				Utilities.DeleteFile (tempDotnetScriptPath);
				return true;
			}

			if (File.Exists (dotnetScriptPath)) {
				Log.WarningLine ($"Download of dotnet-install from '{dotnetScriptUrl}' failed");
				Log.StatusLine ($"Using cached installation script found in '{dotnetScriptPath}'");
				return true;
			} else {
				Log.ErrorLine ($"Download of dotnet-install from '{dotnetScriptUrl}' failed");
				return false;
			}
		}

		string[] GetInstallationScriptArgs (string version, string dotnetPath, string dotnetScriptPath, bool runtimeOnly)
		{
			List<string> args;
			if (Context.IsWindows) {
				args = new List<string> {
					"-NoProfile", "-ExecutionPolicy", "unrestricted", "-file", dotnetScriptPath,
					"-Version", version, "-InstallDir", dotnetPath, "-Verbose"
				};
				if (runtimeOnly) {
					args.AddRange (new string [] { "-Runtime", "dotnet" });
				}
				return args.ToArray ();
			}

			args = new List<string> {
				dotnetScriptPath, "--version", version, "--install-dir", dotnetPath, "--verbose"
			};

			if (runtimeOnly) {
				args.AddRange (new string [] { "--runtime", "dotnet" });
			}
			return args.ToArray ();
		}

		async Task<bool> InstallDotNetFromLocalArchiveAsync (Context context, string dotnetPath, string archivePath)
		{
			if (!File.Exists (archivePath)) {
				Log.ErrorLine ($"Local .NET SDK archive not found: '{archivePath}'");
				return false;
			}

			Log.StatusLine ($"Installing .NET SDK from local archive: {archivePath}");

			// Always delete the bin/$(Configuration)/dotnet/ directory
			Utilities.DeleteDirectory (dotnetPath);

			return await Utilities.Unpack (archivePath, dotnetPath);
		}

		// Standardize on dotnet/arcade's usage of the dotnet-install scripts:
		// invoke dotnet-install.{sh,ps1} directly with --version/--install-dir
		// rather than harvesting URLs and downloading/extracting the archive
		// ourselves. This matches Arcade's eng/common/tools.sh and our CI
		// (build-tools/automation/yaml-templates/use-dot-net.yaml), lets the
		// script perform its built-in SHA-512 verification of the archive, and
		// relies on the script's own "already installed" check for incremental
		// re-runs (no need to re-download when the requested version is present).
		async Task<bool> InstallDotNetAsync (Context context, string dotnetPath, string version, bool useCachedInstallScript, bool runtimeOnly = false)
		{
			string cacheDir = context.Properties.GetRequiredValue (KnownProperties.AndroidToolchainCacheDirectory);

			Uri dotnetScriptUrl = Configurables.Urls.DotNetInstallScript;
			string scriptFileName = Path.GetFileName (dotnetScriptUrl.LocalPath);
			string cachedDotnetScriptPath = Path.Combine (cacheDir, scriptFileName);
			if (!await DownloadDotNetInstallScript (context, cachedDotnetScriptPath, dotnetScriptUrl, useCachedInstallScript)) {
				return false;
			}

			Directory.CreateDirectory (dotnetPath);
			string dotnetScriptPath = Path.Combine (dotnetPath, scriptFileName);
			Utilities.CopyFile (cachedDotnetScriptPath, dotnetScriptPath);

			var type = runtimeOnly ? "runtime" : "SDK";
			Log.StatusLine ($"Installing dotnet {type} '{version}'...");

			string scriptCommand = Context.IsWindows ? "powershell.exe" : "bash";
			string[] scriptArgs = GetInstallationScriptArgs (version, dotnetPath, dotnetScriptPath, runtimeOnly);
			return Utilities.RunCommand (scriptCommand, scriptArgs);
		}

		bool TestDotNetSdk (string dotnetTool)
		{
			return Utilities.RunCommand (dotnetTool, new string [] { "--version" });
		}

		public void AddToInventory ()
		{
			if (!string.IsNullOrEmpty (BuildToolName) && !string.IsNullOrEmpty (BuildToolVersion) && !Context.Instance.BuildToolsInventory.ContainsKey (BuildToolName)) {
				Context.Instance.BuildToolsInventory.Add (BuildToolName, BuildToolVersion);
			}
		}

	}
}
