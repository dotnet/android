using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	class Step_InstallGradle : StepWithDownloadProgress, IBuildInventoryItem
	{
		const string XAVersionInfoFile = "xa_gradle_version.txt";

		// Paths relative to Gradle installation root, just for a cursory check whether we have a sane instance
		static readonly List<string> gradleFiles = new List<string> {
			Path.Combine ("bin", "gradle"),
			Path.Combine ("lib", $"gradle-cli-{Configurables.Defaults.GradleVersion}.jar"),
		};

		public Step_InstallGradle ()
			: base ($"Installing Gradle {Configurables.Defaults.GradleVersion}")
		{}

		Version GradleVersion => Configurables.Defaults.GradleVersion;

		public string BuildToolName => "Gradle";
		public string BuildToolVersion => GradleVersion.ToString ();

		protected override async Task<bool> Execute (Context context)
		{
			AddToInventory ();
			
			string gradleDirName = Configurables.Defaults.GradleRootDirName;
			string gradleCacheDir = Configurables.Paths.AndroidBuildToolsCacheDir;
			string gradleInstallDir = Path.Combine (Context.Instance.Properties.GetRequiredValue (KnownProperties.AndroidToolchainDirectory), "gradle");

			if (GradleExistsAndIsValid (gradleInstallDir, out string? installedVersion)) {
				Log.Status ($"{BuildToolName} version ");
				Log.Status (installedVersion ?? "Unknown", ConsoleColor.Yellow);
				Log.StatusLine (" already installed in: ", gradleInstallDir, tailColor: ConsoleColor.Cyan);
				return true;
			}

			Log.StatusLine ($"{BuildToolName} {GradleVersion} will be installed");

			string localPackagePath = Path.Combine (gradleCacheDir, $"gradle-{GradleVersion}-bin.zip");
			if (!await DownloadGradleBin (context, localPackagePath, Configurables.Urls.GradleBinUri))
				return false;

			string tempDir = $"{gradleInstallDir}.temp";
			try {
				if (!await Utilities.Unpack (localPackagePath, tempDir, true)) {
					Log.ErrorLine ($"Failed to install {BuildToolName}");
					return false;
				}

				string rootDir = Path.Combine (tempDir, gradleDirName);
				if (!Directory.Exists (rootDir)) {
					Log.ErrorLine ($"${BuildToolName} root directory not found after unpacking: {gradleDirName}");
					return false;
				}

				Utilities.MoveDirectoryContentsRecursively  (rootDir, gradleInstallDir);
				File.WriteAllText (Path.Combine (gradleInstallDir, XAVersionInfoFile), $"{GradleVersion}{Environment.NewLine}");
			} finally {
				Utilities.DeleteDirectorySilent (tempDir);
				// Clean up zip after extraction if running on a hosted azure pipelines agent.
				if (context.IsRunningOnHostedAzureAgent)
					Utilities.DeleteFileSilent (localPackagePath);
			}

			return true;
		}

		async Task<bool> DownloadGradleBin (Context context, string localPackagePath, Uri url)
		{
			if (File.Exists (localPackagePath)) {
				Log.StatusLine ($"{BuildToolName} archive already downloaded");
				return true;
			}

			Log.StatusLine ($"Downloading {BuildToolName} from ", url.ToString (), tailColor: ConsoleColor.White);
			(bool success, ulong size, HttpStatusCode status) = await Utilities.GetDownloadSizeWithStatus (url);
			if (!success) {
				if (status == HttpStatusCode.NotFound)
					Log.ErrorLine ($"{BuildToolName} archive URL not found");
				else
					Log.ErrorLine ($"Failed to obtain {BuildToolName} size. HTTP status code: {status} ({(int)status})");
				return false;
			}

			DownloadStatus downloadStatus = Utilities.SetupDownloadStatus (context, size, context.InteractiveSession);
			Log.StatusLine ($"  {context.Characters.Link} {url}", ConsoleColor.White);
			await Download (context, url, localPackagePath, BuildToolName, Path.GetFileName (localPackagePath), downloadStatus);

			if (!File.Exists (localPackagePath)) {
				Log.ErrorLine ($"Download of {BuildToolName} from {url} failed.");
				return false;
			}

			return true;
		}

		bool GradleExistsAndIsValid (string installDir, out string? installedVersion)
		{
			installedVersion = null;
			if (!Directory.Exists (installDir)) {
				Log.DebugLine ($"{BuildToolName} directory {installDir} does not exist");
				return false;
			}

			string xaVersionFile = Path.Combine (installDir, XAVersionInfoFile);
			if (!File.Exists (xaVersionFile)) {
				Log.DebugLine ($"Unable to find .NET for Android version file {xaVersionFile}");
				return false;
			} else {
				installedVersion = File.ReadAllText (xaVersionFile).Trim ();
			}

			if (installedVersion == null || !Version.TryParse (installedVersion, out Version? currentVersion)) {
				Log.DebugLine ($"Unable to parse {BuildToolName} version from: {installedVersion}");
				return false;
			}

			if (currentVersion != GradleVersion) {
				Log.DebugLine ($"Invalid {BuildToolName} version. Need {GradleVersion}, found {currentVersion}");
				return false;
			}

			foreach (string f in gradleFiles) {
				string file = Path.Combine (installDir, f);
				if (!File.Exists (file)) {
					bool foundExe = false;
					foreach (string exe in Utilities.FindExecutable (f)) {
						file = Path.Combine (installDir, exe);
						if (File.Exists (file)) {
							foundExe = true;
							break;
						}
					}

					if (!foundExe) {
						Log.DebugLine ($"{BuildToolName} file {file} missing from {BuildToolName}");
						return false;
					}
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
