using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Xamarin.Android.Prepare
{
	partial class Step_InstallJetBrainsOpenJDK : StepWithDownloadProgress
	{
		const string ProductName = "JetBrains OpenJDK";
		const string RootDirName = "jbrsdk";
		const string XAVersionInfoFile = "xa_jdk_version.txt";
		const string URLQueryFilePathField = "file_path";

		static readonly char[] QuerySeparator = new char[] { ';', '&' };

		// Paths relative to JDK installation root, just for a cursory check whether we have a sane JDK instance
		// NOTE: file extensions are not necessary here
		static readonly List<string> jdkFiles = new List<string> {
			Path.Combine ("bin", "java"),
			Path.Combine ("bin", "javac"),
			Path.Combine ("include", "jni.h"),
		};

		public Step_InstallJetBrainsOpenJDK ()
			: base ($"Installing {ProductName} 11")
		{}

		protected override async Task<bool> Execute (Context context)
		{
			string jdkInstallDir = Configurables.Paths.OpenJDKInstallDir;
			if (OpenJDKExistsAndIsValid (jdkInstallDir, out string installedVersion)) {
				Log.Status ($"{ProductName} version ");
				Log.Status (installedVersion, ConsoleColor.Yellow);
				Log.StatusLine (" already installed in: ", jdkInstallDir, tailColor: ConsoleColor.Cyan);
				return true;
			}

			Log.StatusLine ($"JetBrains JDK {Configurables.Defaults.JetBrainsOpenJDKVersion} r{Configurables.Defaults.JetBrainsOpenJDKRelease} will be installed");
			Uri jdkURL = Configurables.Urls.JetBrainsOpenJDK;
			if (jdkURL == null)
				throw new InvalidOperationException ($"{ProductName} URL must not be null");

			string[] queryParams = jdkURL.Query.TrimStart ('?').Split (QuerySeparator, StringSplitOptions.RemoveEmptyEntries);
			if (queryParams.Length == 0) {
				Log.ErrorLine ($"Unable to extract file name from {ProductName} URL as it contains no query component");
				return false;
			}

			string packageName = null;
			foreach (string p in queryParams) {
				if (!p.StartsWith (URLQueryFilePathField, StringComparison.Ordinal)) {
					continue;
				}

				int idx = p.IndexOf ('=');
				if (idx < 0) {
					Log.DebugLine ($"{ProductName} URL query field '{URLQueryFilePathField}' has no value, unable to detect file name");
					break;
				}

				packageName = p.Substring (idx + 1).Trim ();
			}

			if (String.IsNullOrEmpty (packageName)) {
				Log.ErrorLine ($"Unable to extract file name from {ProductName} URL");
				return false;
			}

			string localPackagePath = Path.Combine (Configurables.Paths.OpenJDKCacheDir, packageName);
			if (!await DownloadOpenJDK (context, localPackagePath, jdkURL))
				return false;

			string tempDir = $"{jdkInstallDir}.temp";
			try {
				if (!await Unpack (localPackagePath, tempDir, cleanDestinationBeforeUnpacking: true)) {
					Log.ErrorLine ($"Failed to install {ProductName}");
					return false;
				}

				string rootDir = Path.Combine (tempDir, RootDirName);
				if (!Directory.Exists (rootDir)) {
					Log.ErrorLine ($"JetBrains root directory not found after unpacking: {RootDirName}");
					return false;
				}

				MoveContents (rootDir, jdkInstallDir);
				File.WriteAllText (Path.Combine (jdkInstallDir, XAVersionInfoFile), $"{Configurables.Defaults.JetBrainsOpenJDKRelease}{Environment.NewLine}");
			} finally {
				Utilities.DeleteDirectorySilent (tempDir);
				// Clean up zip after extraction if running on a hosted azure pipelines agent.
				if (context.IsRunningOnHostedAzureAgent)
					Utilities.DeleteFileSilent (localPackagePath);
			}

			return true;
		}

		async Task<bool> DownloadOpenJDK (Context context, string localPackagePath, Uri url)
		{
			if (File.Exists (localPackagePath)) {
				Log.StatusLine ($"{ProductName} archive already downloaded");
				return true;
			}

			Log.StatusLine ($"Downloading {ProductName} from ", url.ToString (), tailColor: ConsoleColor.White);
			(bool success, ulong size, HttpStatusCode status) = await Utilities.GetDownloadSizeWithStatus (url);
			if (!success) {
				if (status == HttpStatusCode.NotFound)
					Log.ErrorLine ($"{ProductName} archive URL not found");
				else
					Log.ErrorLine ($"Failed to obtain {ProductName} size. HTTP status code: {status} ({(int)status})");
				return false;
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

		bool OpenJDKExistsAndIsValid (string installDir, out string installedVersion)
		{
			installedVersion = null;
			if (!Directory.Exists (installDir)) {
				Log.DebugLine ($"{ProductName} directory {installDir} does not exist");
				return false;
			}

			string corettoVersionFile = Path.Combine (installDir, "version.txt");
			if (File.Exists (corettoVersionFile)) {
				Log.DebugLine ($"Corretto version file {corettoVersionFile} found, will replace Corretto with {ProductName}");
				return false;
			}

			string jetBrainsReleaseFile = Path.Combine (installDir, "release");
			if (!File.Exists (jetBrainsReleaseFile)) {
				Log.DebugLine ($"{ProductName} release file {jetBrainsReleaseFile} does not exist, cannot determine version");
				return false;
			}

			string[] lines = File.ReadAllLines (jetBrainsReleaseFile);
			if (lines == null || lines.Length == 0) {
				Log.DebugLine ($"{ProductName} release file {jetBrainsReleaseFile} is empty, cannot determine version");
				return false;
			}

			string cv = null;
			foreach (string l in lines) {
				string line = l.Trim ();
				if (!line.StartsWith ("JAVA_VERSION=", StringComparison.Ordinal)) {
					continue;
				}

				cv = line.Substring (line.IndexOf ('=') + 1).Trim ('"');
				break;
			}

			if (String.IsNullOrEmpty (cv)) {
				Log.DebugLine ($"Unable to find version of {ProductName} in release file {jetBrainsReleaseFile}");
				return false;
			}

			string xaVersionFile = Path.Combine (installDir, XAVersionInfoFile);
			if (!File.Exists (xaVersionFile)) {
				installedVersion = cv;
				Log.DebugLine ($"Unable to find Xamarin.Android version file {xaVersionFile}");
				return false;
			}

			lines = File.ReadAllLines (xaVersionFile);
			if (lines == null || lines.Length == 0) {
				Log.DebugLine ($"Xamarin.Android version file {xaVersionFile} is empty, cannot determine release version");
				return false;
			}

			string rv = lines[0].Trim ();
			if (String.IsNullOrEmpty (rv)) {
				Log.DebugLine ($"Xamarin.Android version file {xaVersionFile} does not contain release version information");
				return false;
			}

			installedVersion = $"{cv} r{rv}";

			if (!Version.TryParse (cv, out Version cversion)) {
				Log.DebugLine ($"Unable to parse {ProductName} version from: {cv}");
				return false;
			}

			if (cversion != Configurables.Defaults.JetBrainsOpenJDKVersion) {
				Log.DebugLine ($"Invalid {ProductName} version. Need {Configurables.Defaults.JetBrainsOpenJDKVersion}, found {cversion}");
				return false;
			}

			if (!Version.TryParse (rv, out cversion)) {
				Log.DebugLine ($"Unable to parse {ProductName} release version from: {rv}");
				return false;
			}

			if (cversion != Configurables.Defaults.JetBrainsOpenJDKRelease) {
				Log.DebugLine ($"Invalid {ProductName} version. Need {Configurables.Defaults.JetBrainsOpenJDKRelease}, found {cversion}");
				return false;
			}

			foreach (string f in jdkFiles) {
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
						Log.DebugLine ($"JDK file {file} missing from {ProductName}");
						return false;
					}
				}
			}

			return true;
		}
	}
}
