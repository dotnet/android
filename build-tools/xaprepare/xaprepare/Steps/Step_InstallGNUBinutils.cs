using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	partial class Step_InstallGNUBinutils : StepWithDownloadProgress
	{
		static readonly string ProductName = $"GNU Binutils {Configurables.Defaults.BinutilsVersion}";

		public Step_InstallGNUBinutils ()
			: base ("Install GNU Binutils")
		{}

		protected override async Task<bool> Execute (Context context)
		{
			string hostDestinationDirectory = Configurables.Paths.HostBinutilsInstallDir;
			string windowsDestinationDirectory = Configurables.Paths.WindowsBinutilsInstallDir;

			bool hostHaveAll = HaveAllBinutils (hostDestinationDirectory);
			bool windowsHaveAll = HaveAllBinutils (windowsDestinationDirectory, ".exe");

			if (hostHaveAll && windowsHaveAll) {
				Log.StatusLine ("All Binutils are already installed");
				return true;
			}

			string packageName = Path.GetFileName (Configurables.Urls.BinutilsArchive.LocalPath);
			string localArchivePath = Path.Combine (Configurables.Paths.BinutilsCacheDir, packageName);

			if (!await DownloadBinutils (context, localArchivePath, Configurables.Urls.BinutilsArchive)) {
				return false;
			}

			string tempDir = Path.Combine (Path.GetTempPath (), "xaprepare-binutils");
			Utilities.DeleteDirectorySilent (tempDir);
			Utilities.CreateDirectory (tempDir);

			Log.DebugLine ($"Unpacking {ProductName} archive {localArchivePath} into {tempDir}");
			if (!await Utilities.Unpack (localArchivePath, tempDir, cleanDestinatioBeforeUnpacking: true)) {
				return false;
			}

			if (!hostHaveAll) {
				CopyToDestination (context, "Host", tempDir, hostDestinationDirectory, executableExtension: ExecutableExtension);
			}

			if (!windowsHaveAll) {
				CopyToDestination (context, "Windows", tempDir, windowsDestinationDirectory, "windows", ".exe");
			}

			return true;
		}

		bool CopyToDestination (Context context, string label, string sourceDir, string destinationDir, string osName = HostName, string? executableExtension = null)
		{
			Log.StatusLine ();
			Log.StatusLine ($"Installing for {label}:");

			string sourcePath = Path.Combine (sourceDir, osName);
			foreach (var kvp in Configurables.Defaults.AndroidToolchainPrefixes) {
				string prefix = kvp.Value;

				foreach (NDKTool tool in Configurables.Defaults.NDKTools) {
					string toolName = GetToolName (prefix, tool, executableExtension);
					string toolSourcePath = Path.Combine (sourcePath, toolName);
					string toolDestinationPath = Path.Combine (destinationDir, toolName);
					string versionMarkerPath = GetVersionMarker (toolDestinationPath);

					Log.StatusLine ($"  {context.Characters.Bullet} Installing ", toolName, tailColor: ConsoleColor.White);
					Utilities.CopyFile (toolSourcePath, toolDestinationPath);
					File.WriteAllText (versionMarkerPath, DateTime.UtcNow.ToString ());
				}
			}

			return true;
		}

		async Task<bool> DownloadBinutils (Context context, string localPackagePath, Uri url)
		{
			if (Utilities.FileExists (localPackagePath)) {
				Log.StatusLine ($"{ProductName} archive already downloaded");
				return true;
			}

			Log.StatusLine ($"Downloading {ProductName} from ", url.ToString (), tailColor: ConsoleColor.White);
			(bool success, ulong size, HttpStatusCode status) = await Utilities.GetDownloadSizeWithStatus (url);
			if (!success) {
				if (status == HttpStatusCode.NotFound) {
					Log.ErrorLine ($"{ProductName} archive URL not found");
					return false;
				}
				Log.WarningLine ($"Failed to obtain {ProductName} size. HTTP status code: {status} ({(int)status})");
			}

			DownloadStatus downloadStatus = Utilities.SetupDownloadStatus (context, size, context.InteractiveSession);
			Log.StatusLine ($"  {context.Characters.Link} {url}", ConsoleColor.White);
			await Download (context, url, localPackagePath, ProductName, Path.GetFileName (localPackagePath), downloadStatus);

			if (!File.Exists (localPackagePath)) {
				Log.ErrorLine ($"Download of {ProductName} from {url} failed.");
				return false;
			}

			return true;
		}

		bool HaveAllBinutils (string dir, string? executableExtension = null)
		{
			Log.DebugLine ("Checking if all binutils are installed in {dir}");
			string extension = executableExtension ?? String.Empty;
			foreach (var kvp in Configurables.Defaults.AndroidToolchainPrefixes) {
				string prefix = kvp.Value;

				foreach (NDKTool tool in Configurables.Defaults.NDKTools) {
					string toolName = GetToolName (prefix, tool, executableExtension);
					string toolPath = Path.Combine (dir, toolName);
					string versionMarkerPath = GetVersionMarker (toolPath);

					Log.DebugLine ($"Checking {toolName}");
					if (!Utilities.FileExists (toolPath)) {
						Log.DebugLine ($"Binutils tool {toolPath} does not exist");
						return false;
					}

					if (!Utilities.FileExists (versionMarkerPath)) {
						Log.DebugLine ($"Binutils tool {toolPath} exists, but its version is incorrect");
						return false;
					}
				}
			}

			return true;
		}

		string GetToolName (string prefix, NDKTool tool, string? executableExtension = null)
		{
			return $"{prefix}-{(String.IsNullOrEmpty (tool.DestinationName) ? tool.Name : tool.DestinationName)}{executableExtension}";
		}

		string GetVersionMarker (string toolPath)
		{
			return $"{toolPath}.{Configurables.Defaults.BinutilsVersion}";
		}
	}
}
