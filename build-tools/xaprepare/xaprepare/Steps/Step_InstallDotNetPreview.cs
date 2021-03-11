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
			var dotnetPreviewVersion = context.Properties.GetRequiredValue (KnownProperties.MicrosoftNETSdkPackageVersion);
			var dotnetTestRuntimeVersion = Configurables.Defaults.DotNetTestRuntimeVersion;

			// Delete any custom Microsoft.Android packs that may have been installed by test runs. Other ref/runtime packs will be ignored.
			var packsPath = Path.Combine (dotnetPath, "packs");
			if (Directory.Exists (packsPath)) {
				foreach (var packToRemove in Directory.EnumerateDirectories (packsPath).Where (p => new DirectoryInfo (p).Name.Contains ("Android"))) {
					Log.StatusLine ($"Removing Android pack: {packToRemove}");
					Utilities.DeleteDirectory (packToRemove);
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

			if (Directory.Exists (dotnetPath)) {
				if (!TestDotNetSdk (dotnetPath)) {
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

		bool TestDotNetSdk (string dotnetPath)
		{
			return Utilities.RunCommand (Path.Combine (dotnetPath, "dotnet"), new string [] { "--version" });
		}

	}
}
