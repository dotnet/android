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

			if (!await InstallDotNetAsync (context, dotnetPath, BuildToolVersion)) {
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
			var logPath = Path.Combine (Configurables.Paths.BuildBinDir, $"msbuild-{context.BuildTimeStamp}-download-runtime-packs.binlog");
			var restoreArgs = new string [] { "restore",
				ProcessRunner.QuoteArgument (packageDownloadProj),
				"--configfile", Path.Combine (BuildPaths.XamarinAndroidSourceRoot, "NuGet.config"),
				ProcessRunner.QuoteArgument ($"-bl:{logPath}"),
			};
			if (!Utilities.RunCommand (Configurables.Paths.DotNetPreviewTool, restoreArgs)) {
				Log.ErrorLine ($"Failed to restore runtime packs using '{packageDownloadProj}'.");
				return false;
			}

			var sdk_manifests = Path.Combine (dotnetPath, "sdk-manifests");

			// Copy the WorkloadManifest.* files from the latest Microsoft.NET.Workload.* listed in package-download.proj
			var dotnets = new [] { "net6", "net7", "current" };
			foreach (var dotnet in dotnets) {
				var destination = Path.Combine (sdk_manifests, context.Properties.GetRequiredValue (KnownProperties.DotNetMonoManifestVersionBand), $"microsoft.net.workload.mono.toolchain.{dotnet}");
				foreach (var file in Directory.GetFiles (string.Format (Configurables.Paths.MicrosoftNETWorkloadMonoToolChainDir, dotnet))) {
					Utilities.CopyFileToDir (file, destination);
				}
				destination = Path.Combine (sdk_manifests, context.Properties.GetRequiredValue (KnownProperties.DotNetEmscriptenManifestVersionBand), $"microsoft.net.workload.emscripten.{dotnet}");
				foreach (var file in Directory.GetFiles (string.Format (Configurables.Paths.MicrosoftNETWorkloadEmscriptenDir, dotnet))) {
					Utilities.CopyFileToDir (file, destination);
				}
			}

			return true;
		}

		async Task<bool> DownloadDotNetInstallScript (Context context, string dotnetScriptPath, Uri dotnetScriptUrl)
		{
			string tempDotnetScriptPath = dotnetScriptPath + "-tmp";
			Utilities.DeleteFile (tempDotnetScriptPath);

			Log.StatusLine ("Downloading dotnet-install script...");

			(bool success, ulong size, HttpStatusCode status) = await Utilities.GetDownloadSizeWithStatus (dotnetScriptUrl);
			if (!success) {
				if (status == HttpStatusCode.NotFound) {
					Log.WarningLine ($"dotnet-install URL '{dotnetScriptUrl}' not found.");
				} else {
					Log.WarningLine ($"Failed to obtain dotnet-install script size from URL '{dotnetScriptUrl}'. HTTP status code: {status} ({(int) status})");
				}

				if (File.Exists (dotnetScriptPath)) {
					Log.WarningLine ($"Using cached installation script found in '{dotnetScriptPath}'");
					return true;
				}
			}

			DownloadStatus downloadStatus = Utilities.SetupDownloadStatus (context, size, context.InteractiveSession);
			Log.StatusLine ($"  {context.Characters.Link} {dotnetScriptUrl}", ConsoleColor.White);
			await Download (context, dotnetScriptUrl, tempDotnetScriptPath, "dotnet-install", Path.GetFileName (dotnetScriptUrl.LocalPath), downloadStatus);

			if (File.Exists (tempDotnetScriptPath)) {
				Utilities.CopyFile (tempDotnetScriptPath, dotnetScriptPath);
				Utilities.DeleteFile (tempDotnetScriptPath);
				return true;
			}

			if (File.Exists (dotnetScriptPath)) {
				Log.WarningLine ($"Download of dotnet-install from '{dotnetScriptUrl}' failed");
				Log.WarningLine ($"Using cached installation script found in '{dotnetScriptPath}'");
				return true;
			} else {
				Log.ErrorLine ($"Download of dotnet-install from '{dotnetScriptUrl}' failed");
				return false;
			}
		}

		async Task<bool> DownloadDotNetArchive (Context context, string archiveDestinationPath, Uri archiveUrl)
		{
			Log.StatusLine ("Downloading dotnet archive...");

			(bool success, ulong size, HttpStatusCode status) = await Utilities.GetDownloadSizeWithStatus (archiveUrl);
			if (!success) {
				if (status == HttpStatusCode.NotFound) {
					Log.WarningLine ($"dotnet archive URL {archiveUrl} not found");
					return false;
				} else {
					Log.WarningLine ($"Failed to obtain dotnet archive size. HTTP status code: {status} ({(int)status})");
				}

				return false;
			}

			string tempArchiveDestinationPath = archiveDestinationPath + "-tmp";
			Utilities.DeleteFile (tempArchiveDestinationPath);

			DownloadStatus downloadStatus = Utilities.SetupDownloadStatus (context, size, context.InteractiveSession);
			Log.StatusLine ($"  {context.Characters.Link} {archiveUrl}", ConsoleColor.White);
			await Download (context, archiveUrl, tempArchiveDestinationPath, "dotnet archive", Path.GetFileName (archiveUrl.LocalPath), downloadStatus);

			if (!File.Exists (tempArchiveDestinationPath)) {
				return false;
			}

			Utilities.CopyFile (tempArchiveDestinationPath, archiveDestinationPath);
			Utilities.DeleteFile (tempArchiveDestinationPath);

			return true;
		}

		string[] GetInstallationScriptArgs (string version, string dotnetPath, string dotnetScriptPath, bool onlyGetUrls, bool runtimeOnly)
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
				if (onlyGetUrls) {
					args.Add ("-DryRun");
				}

				return args.ToArray ();
			}

			args = new List<string> {
				dotnetScriptPath, "--version", version, "--install-dir", dotnetPath, "--verbose"
			};

			if (runtimeOnly) {
				args.AddRange (new string [] { "-Runtime", "dotnet" });
			}
			if (onlyGetUrls) {
				args.Add ("--dry-run");
			}

			return args.ToArray ();
		}

		async Task<bool> InstallDotNetAsync (Context context, string dotnetPath, string version, bool runtimeOnly = false)
		{
			string cacheDir = context.Properties.GetRequiredValue (KnownProperties.AndroidToolchainCacheDirectory);

			// Always delete the bin/$(Configuration)/dotnet/ directory
			Utilities.DeleteDirectory (dotnetPath);

			Uri dotnetScriptUrl = Configurables.Urls.DotNetInstallScript;
			string scriptFileName = Path.GetFileName (dotnetScriptUrl.LocalPath);
			string cachedDotnetScriptPath = Path.Combine (cacheDir, scriptFileName);
			if (!await DownloadDotNetInstallScript (context, cachedDotnetScriptPath, dotnetScriptUrl)) {
				return false;
			}

			string dotnetScriptPath = Path.Combine (dotnetPath, scriptFileName);
			Utilities.CopyFile (cachedDotnetScriptPath, dotnetScriptPath);

			var type = runtimeOnly ? "runtime" : "SDK";

			Log.StatusLine ($"Discovering download URLs for dotnet {type} '{version}'...");
			string scriptCommand = Context.IsWindows ? "powershell.exe" : "bash";
			string[] scriptArgs = GetInstallationScriptArgs (version, dotnetPath, dotnetScriptPath, onlyGetUrls: true, runtimeOnly: runtimeOnly);
			string scriptReply = Utilities.GetStringFromStdout (scriptCommand, scriptArgs);
			var archiveUrls = new List<string> ();

			char[] fieldSplitChars = new char[] { ':' };
			foreach (string l in scriptReply.Split (new char[] { '\n' })) {
				string line = l.Trim ();

				if (!line.StartsWith ("dotnet-install: URL #", StringComparison.OrdinalIgnoreCase)) {
					continue;
				}

				string[] parts = line.Split (fieldSplitChars, 3);
				if (parts.Length < 3) {
					Log.WarningLine ($"dotnet-install URL line has unexpected number of parts. Expected 3, got {parts.Length}");
					Log.WarningLine ($"Line: {line}");
					continue;
				}

				archiveUrls.Add (parts[2].Trim ());
			}

			if (archiveUrls.Count == 0) {
				Log.WarningLine ("No dotnet archive URLs discovered, attempting to run the installation script");
				scriptArgs = GetInstallationScriptArgs (version, dotnetPath, dotnetScriptPath, onlyGetUrls: false, runtimeOnly: runtimeOnly);
				return Utilities.RunCommand (scriptCommand, scriptArgs);
			}

			string? archivePath = null;
			foreach (string url in archiveUrls) {
				var archiveUrl = new Uri (url);
				string archiveDestinationPath = Path.Combine (cacheDir, Path.GetFileName (archiveUrl.LocalPath));

				if (File.Exists (archiveDestinationPath)) {
					archivePath = archiveDestinationPath;
					break;
				}

				if (await DownloadDotNetArchive (context, archiveDestinationPath, archiveUrl)) {
					archivePath = archiveDestinationPath;
					break;
				}
			}

			if (String.IsNullOrEmpty (archivePath)) {
				return false;
			}

			Log.StatusLine ($"Installing dotnet {type} '{version}'...");
			return await Utilities.Unpack (archivePath, dotnetPath);
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
