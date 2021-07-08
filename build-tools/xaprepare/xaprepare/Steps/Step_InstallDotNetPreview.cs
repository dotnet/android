using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	class Step_InstallDotNetPreview : StepWithDownloadProgress
	{
		public Step_InstallDotNetPreview ()
			: base ("Install required .NET Preview SDK locally")
		{ }

		protected override async Task<bool> Execute (Context context)
		{
			var dotnetPath = context.Properties.GetRequiredValue (KnownProperties.DotNetPreviewPath);
			dotnetPath = dotnetPath.TrimEnd (new char [] { Path.DirectorySeparatorChar });
			var dotnetTool = Path.Combine (dotnetPath, "dotnet");
			var dotnetPreviewVersion = context.Properties.GetRequiredValue (KnownProperties.MicrosoftDotnetSdkInternalPackageVersion);
			var dotnetTestRuntimeVersion = Configurables.Defaults.DotNetTestRuntimeVersion;

			// Delete any custom Microsoft.Android packs that may have been installed by test runs. Other ref/runtime packs will be ignored.
			var packsPath = Path.Combine (dotnetPath, "packs");
			if (Directory.Exists (packsPath)) {
				foreach (var packToRemove in Directory.EnumerateDirectories (packsPath)) {
					var info = new DirectoryInfo (packToRemove);
					if (info.Name.IndexOf ("Android", StringComparison.OrdinalIgnoreCase) != -1) {
						Log.StatusLine ($"Removing Android pack: {packToRemove}");
						Utilities.DeleteDirectory (packToRemove);
					}
				}
			}

			// Delete Workload manifests, such as sdk-manifests/6.0.100/Microsoft.NET.Sdk.Android
			var sdkManifestsPath = Path.Combine (dotnetPath, "sdk-manifests");
			if (Directory.Exists (sdkManifestsPath)) {
				foreach (var versionBand in Directory.EnumerateDirectories (sdkManifestsPath)) {
					foreach (var workloadManifestDirectory in Directory.EnumerateDirectories (versionBand)) {
						var info = new DirectoryInfo (workloadManifestDirectory);
						if (info.Name.IndexOf ("Android", StringComparison.OrdinalIgnoreCase) != -1) {
							Log.StatusLine ($"Removing Android manifest directory: {workloadManifestDirectory}");
							Utilities.DeleteDirectory (workloadManifestDirectory);
						}
					}
				}
			}

			// Delete any unnecessary SDKs if they exist.
			var sdkPath = Path.Combine (dotnetPath, "sdk");
			if (Directory.Exists (sdkPath)) {
				foreach (var sdkToRemove in Directory.EnumerateDirectories (sdkPath).Where (s => new DirectoryInfo (s).Name != dotnetPreviewVersion)) {
					Log.StatusLine ($"Removing out of date SDK: {sdkToRemove}");
					Utilities.DeleteDirectory (sdkToRemove);
				}
			}

			// Delete Android template-packs
			var templatePacksPath = Path.Combine (dotnetPath, "template-packs");
			if (Directory.Exists (templatePacksPath)) {
				foreach (var templateToRemove in Directory.EnumerateFiles (templatePacksPath)) {
					var name = Path.GetFileName (templateToRemove);
					if (name.IndexOf ("Android", StringComparison.OrdinalIgnoreCase) != -1) {
						Log.StatusLine ($"Removing Android template: {templateToRemove}");
						Utilities.DeleteFile (templateToRemove);
					}
				}
			}

			// Delete the metadata folder, which contains old workload data
			var metadataPath = Path.Combine (dotnetPath, "metadata");
			if (Directory.Exists (metadataPath)) {
				Utilities.DeleteDirectory (metadataPath);
			}

			if (File.Exists (dotnetTool)) {
				if (!TestDotNetSdk (dotnetTool)) {
					Log.WarningLine ($"Attempt to run `dotnet --version` failed, reinstalling the SDK.");
					Utilities.DeleteDirectory (dotnetPath);
				}
			}

			if (!await InstallDotNetAsync (context, dotnetPath, dotnetPreviewVersion)) {
				Log.ErrorLine ($"Installation of dotnet SDK {dotnetPreviewVersion} failed.");
				return false;
			}

			if (!await InstallDotNetAsync (context, dotnetPath, dotnetTestRuntimeVersion, runtimeOnly: true)) {
				Log.ErrorLine ($"Installation of dotnet runtime {dotnetTestRuntimeVersion} failed.");
				return false;
			}

			// Install runtime packs associated with the SDK previously installed.
			var packageDownloadProj = Path.Combine (BuildPaths.XamarinAndroidSourceRoot, "build-tools", "xaprepare", "xaprepare", "package-download.proj");
			var logPath = Path.Combine (Configurables.Paths.BuildBinDir, $"msbuild-{context.BuildTimeStamp}-download-runtime-packs.binlog");
			if (!Utilities.RunCommand (dotnetTool, new string [] { "restore", ProcessRunner.QuoteArgument (packageDownloadProj), ProcessRunner.QuoteArgument ($"-bl:{logPath}") })) {
				Log.ErrorLine ($"dotnet restore {packageDownloadProj} failed.");
				return false;
			}

			// Copy the WorkloadManifest.* files from the latest Microsoft.NET.Workload.Mono.ToolChain listed in package-download.proj
			var destination = Path.Combine (dotnetPath, "sdk-manifests",
				context.Properties.GetRequiredValue (KnownProperties.DotNetPreviewVersionBand),
				"microsoft.net.workload.mono.toolchain"
			);
			foreach (var file in Directory.GetFiles (Configurables.Paths.MicrosoftNETWorkloadMonoToolChainDir, "WorkloadManifest.*")) {
				Utilities.CopyFileToDir (file, destination);
			}

			// Install the microsoft-net-runtime-android workload
			if (!Utilities.RunCommand (dotnetTool, BuildPaths.XamarinAndroidSourceRoot, ignoreEmptyArguments: false, new [] { "workload", "install", "microsoft-net-runtime-android", "--skip-manifest-update", "--verbosity", "diag" })) {
				Log.ErrorLine ($"dotnet workload install failed.");
				return false;
			}

			return true;
		}

		async Task<bool> InstallDotNetAsync (Context context, string dotnetPath, string version, bool runtimeOnly = false)
		{
			if (Directory.Exists (Path.Combine (dotnetPath, "sdk", version)) && !runtimeOnly) {
				Log.Status ($"dotnet SDK version ");
				Log.Status (version, ConsoleColor.Yellow);
				Log.StatusLine (" already installed in: ", Path.Combine (dotnetPath, "sdk", version), tailColor: ConsoleColor.Cyan);
				return true;
			}

			if (Directory.Exists (Path.Combine (dotnetPath, "shared", "Microsoft.NETCore.App", version)) && runtimeOnly) {
				Log.Status ($"dotnet runtime version ");
				Log.Status (version, ConsoleColor.Yellow);
				Log.StatusLine (" already installed in: ", Path.Combine (dotnetPath, "shared", "Microsoft.NETCore.App", version), tailColor: ConsoleColor.Cyan);
				return true;
			}

			Uri dotnetScriptUrl = Configurables.Urls.DotNetInstallScript;
			string dotnetScriptPath = Path.Combine (dotnetPath, Path.GetFileName (dotnetScriptUrl.LocalPath));
			if (File.Exists (dotnetScriptPath))
				Utilities.DeleteFile (dotnetScriptPath);

			Log.StatusLine ("Downloading dotnet-install...");

			(bool success, ulong size, HttpStatusCode status) = await Utilities.GetDownloadSizeWithStatus (dotnetScriptUrl);
			if (!success) {
				if (status == HttpStatusCode.NotFound)
					Log.ErrorLine ("dotnet-install URL not found");
				else
					Log.ErrorLine ("Failed to obtain dotnet-install size. HTTP status code: {status} ({(int)status})");
				return false;
			}

			DownloadStatus downloadStatus = Utilities.SetupDownloadStatus (context, size, context.InteractiveSession);
			Log.StatusLine ($"  {context.Characters.Link} {dotnetScriptUrl}", ConsoleColor.White);
			await Download (context, dotnetScriptUrl, dotnetScriptPath, "dotnet-install", Path.GetFileName (dotnetScriptUrl.LocalPath), downloadStatus);

			if (!File.Exists (dotnetScriptPath)) {
				Log.ErrorLine ($"Download of dotnet-install from {dotnetScriptUrl} failed");
				return false;
			}

			var type = runtimeOnly ? "runtime" : "SDK";
			Log.StatusLine ($"Installing dotnet {type} '{version}'...");

			if (Context.IsWindows) {
				var args = new List<string> {
					"-NoProfile", "-ExecutionPolicy", "unrestricted", "-file", dotnetScriptPath,
					"-Version", version, "-InstallDir", dotnetPath, "-Verbose"
				};
				if (runtimeOnly)
					args.AddRange (new string [] { "-Runtime", "dotnet" });

				return Utilities.RunCommand ("powershell.exe", args.ToArray ());
			} else {
				var args = new List<string> {
					dotnetScriptPath, "--version", version, "--install-dir", dotnetPath, "--verbose"
				};
				if (runtimeOnly)
					args.AddRange (new string [] { "-Runtime", "dotnet" });

				return Utilities.RunCommand ("bash", args.ToArray ());
			}
		}

		bool TestDotNetSdk (string dotnetTool)
		{
			return Utilities.RunCommand (dotnetTool, new string [] { "--version" });
		}

	}
}
