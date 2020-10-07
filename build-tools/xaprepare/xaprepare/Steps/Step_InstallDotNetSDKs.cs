using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	class Step_InstallDotNetSDKs : StepWithDownloadProgress
	{
		public Step_InstallDotNetSDKs ()
			: base ("Install required .NET Core SDKs locally")
		{ }

		protected override async Task<bool> Execute (Context context)
		{
			if (context == null)
				throw new ArgumentNullException (nameof (context));

			var dotnetPath = context.Properties.GetRequiredValue (KnownProperties.DotNetPath);
			dotnetPath = dotnetPath.TrimEnd (new char [] { Path.DirectorySeparatorChar });
			var dotnetVersion = Configurables.Defaults.DotNetVersion;
			var dotnetPreviewVersion = Configurables.Defaults.DotNetPreviewVersion;

			// Delete any custom Microsoft.Android packs that may have been installed by test runs. Other ref/runtime packs will be ignored.
			var packsPath = Path.Combine (dotnetPath, "packs");
			if (Directory.Exists (packsPath)) {
				foreach (var packToRemove in Directory.EnumerateDirectories (packsPath).Where (p => new DirectoryInfo (p).Name.Contains ("Android")))
					Utilities.DeleteDirectory (packToRemove);
			}

			// Delete any unnecessary SDKs if they exist.
			var sdkPath = Path.Combine (dotnetPath, "sdk");
			if (Directory.Exists (sdkPath)) {
				var installedSdks = Directory.EnumerateDirectories (sdkPath).Select (d => new DirectoryInfo (d));
				foreach (var sdkToRemove in installedSdks.Where (s => s.Name != dotnetVersion && s.Name != dotnetPreviewVersion))
					Utilities.DeleteDirectory (sdkToRemove.FullName);
			}

			if (!await InstallDotNetAsync (context, dotnetPath, dotnetVersion)) {
				Log.ErrorLine ($"Installation of dotnet SDK {dotnetVersion} failed.");
				return false;
			}

			if (!await InstallDotNetAsync (context, dotnetPath, dotnetPreviewVersion)) {
				Log.ErrorLine ($"Installation of dotnet SDK {dotnetPreviewVersion} failed.");
				return false;
			}

			SetVariables (dotnetPath);
			return true;
		}

		async Task<bool> InstallDotNetAsync (Context context, string dotnetPath, string version)
		{
			if (Directory.Exists (Path.Combine (dotnetPath, "sdk", version))) {
				Log.Status ($"dotnet SDK version ");
				Log.Status (version, ConsoleColor.Yellow);
				Log.StatusLine (" already installed in: ", Path.Combine (dotnetPath, "sdk", version), tailColor: ConsoleColor.Cyan);
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

			Log.StatusLine ($"Installing dotnet SDK '{version}'...");


			if (Context.IsWindows) {
				return Utilities.RunCommand ("powershell.exe", new string [] {
					"-NoProfile", "-ExecutionPolicy", "unrestricted", "-file", dotnetScriptPath,
					"-Version", version, "-InstallDir", dotnetPath, "-Verbose"
				});
			} else {
				return Utilities.RunCommand ("bash", new string [] {
					dotnetScriptPath, "--version", version, "--install-dir", dotnetPath, "--verbose"
				});
			}
		}

		void SetVariables (string dotnetPath)
		{
			var newPath = $"{dotnetPath}{Path.PathSeparator}{Environment.GetEnvironmentVariable ("PATH")}";
			Environment.SetEnvironmentVariable ("PATH", newPath);
			Environment.SetEnvironmentVariable ("DOTNET_ROOT", dotnetPath);
			Log.MessageLine ($"##vso[task.setvariable variable=PATH]{newPath}");
			Log.MessageLine ($"##vso[task.setvariable variable=DOTNET_ROOT]{dotnetPath}");
		}

	}
}
