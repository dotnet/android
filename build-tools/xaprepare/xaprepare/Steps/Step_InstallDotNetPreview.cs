using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
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

			// Always delete the bin/$(Configuration)/dotnet/ directory
			Utilities.DeleteDirectory (dotnetPath);

			if (!await InstallDotNetAsync (context, dotnetPath, BuildToolVersion)) {
				Log.ErrorLine ($"Installation of dotnet SDK {BuildToolVersion} failed.");
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
			var logPath = Path.Combine (Configurables.Paths.BuildBinDir, $"msbuild-{context.BuildTimeStamp}-download-runtime-packs.binlog");
			if (!Utilities.RunCommand (Configurables.Paths.DotNetPreviewTool, new string [] { "restore", ProcessRunner.QuoteArgument (packageDownloadProj), ProcessRunner.QuoteArgument ($"-bl:{logPath}") })) {
				Log.ErrorLine ($"dotnet restore {packageDownloadProj} failed.");
				return false;
			}

			var sdk_manifests = Path.Combine (dotnetPath, "sdk-manifests", context.Properties.GetRequiredValue (KnownProperties.DotNetSdkManifestsFolder));

			// Copy the WorkloadManifest.* files from the latest Microsoft.NET.Workload.* listed in package-download.proj
			var destination = Path.Combine (sdk_manifests, "microsoft.net.workload.mono.toolchain");
			foreach (var file in Directory.GetFiles (Configurables.Paths.MicrosoftNETWorkloadMonoToolChainDir, "WorkloadManifest.*")) {
				Utilities.CopyFileToDir (file, destination);
			}
			destination = Path.Combine (sdk_manifests, "microsoft.net.workload.emscripten");
			foreach (var file in Directory.GetFiles (Configurables.Paths.MicrosoftNETWorkloadEmscriptenDir, "WorkloadManifest.*")) {
				Utilities.CopyFileToDir (file, destination);
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

		public void AddToInventory ()
		{
			if (!string.IsNullOrEmpty (BuildToolName) && !string.IsNullOrEmpty (BuildToolVersion) && !Context.Instance.BuildToolsInventory.ContainsKey (BuildToolName)) {
				Context.Instance.BuildToolsInventory.Add (BuildToolName, BuildToolVersion);
			}
		}

	}
}
