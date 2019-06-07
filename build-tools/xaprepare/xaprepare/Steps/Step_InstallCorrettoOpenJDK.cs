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
	partial class Step_InstallCorrettoOpenJDK : StepWithDownloadProgress
	{
		// Paths relative to JDK installation root, just for a cursory check whether we have a sane JDK instance
		// NOTE: file extensions are not necessary here
		static readonly List<string> jdkFiles = new List<string> {
			Path.Combine ("bin", "java"),
			Path.Combine ("bin", "javac"),
			Path.Combine ("include", "jni.h"),
		};

		public Step_InstallCorrettoOpenJDK ()
			: base ("Installing OpenJDK (Amazon Corretto 8)")
		{}

		protected override async Task<bool> Execute (Context context)
		{
			string corettoInstallDir = Configurables.Paths.CorrettoInstallDir;
			if (CorrettoExistsAndIsValid (corettoInstallDir, out string installedVersion)) {
				Log.Status ("Corretto version ");
				Log.Status (installedVersion, ConsoleColor.Yellow);
				Log.StatusLine (" already installed in: ", corettoInstallDir, tailColor: ConsoleColor.Cyan);
				return true;
			}

			Log.StatusLine ($"Corretto JDK {Configurables.Defaults.CorrettoVersion} will be installed");
			Uri correttoURL = Configurables.Urls.Corretto;
			if (correttoURL == null)
				throw new InvalidOperationException ("Corretto URL must not be null");

			string packageName = Path.GetFileName (correttoURL.LocalPath);
			string localPackagePath = Path.Combine (Configurables.Paths.CorrettoCacheDir, packageName);
			if (!await DownloadCorretto (context, localPackagePath, correttoURL))
				return false;

			string tempDir = $"{corettoInstallDir}.temp";
			try {
				if (!await Utilities.Unpack (localPackagePath, tempDir, cleanDestinatioBeforeUnpacking: true)) {
					Log.ErrorLine ("Failed to install Corretto");
					return false;
				}

				string rootDirName = GetArchiveRootDirectoryName ();
				string rootDir = Path.Combine (tempDir, rootDirName);
				Log.DebugLine ($"{context.OS.Type} root directory name of Corretto OpenJDK package is: {rootDirName}");
				if (!Directory.Exists (rootDir)) {
					Log.ErrorLine ($"Corretto root directory not found after unpacking: {rootDirName}");
					return false;
				}

				Utilities.MoveDirectoryContentsRecursively (rootDir, corettoInstallDir);
			} finally {
				Utilities.DeleteDirectorySilent (tempDir);
			}

			return true;
		}

		async Task<bool> DownloadCorretto (Context context, string localPackagePath, Uri url)
		{
			if (File.Exists (localPackagePath)) {
				if (await Utilities.VerifyArchive (localPackagePath)) {
					Log.StatusLine ("Corretto archive already downloaded and valid");
					return true;
				}
				Utilities.DeleteFileSilent (localPackagePath);
			}

			Log.StatusLine ("Downloading Corretto from ", url.ToString (), tailColor: ConsoleColor.White);
			(bool success, ulong size, HttpStatusCode status) = await Utilities.GetDownloadSizeWithStatus (url);
			if (!success) {
				if (status == HttpStatusCode.NotFound)
					Log.ErrorLine ("Corretto archive URL not found");
				else
					Log.ErrorLine ($"Failed to obtain Corretto size. HTTP status code: {status} ({(int)status})");
				return false;
			}

			DownloadStatus downloadStatus = Utilities.SetupDownloadStatus (context, size, context.InteractiveSession);
			Log.StatusLine ($"  {context.Characters.Link} {url}", ConsoleColor.White);
			await Download (context, url, localPackagePath, "Corretto", Path.GetFileName (localPackagePath), downloadStatus);

			if (!File.Exists (localPackagePath)) {
				Log.ErrorLine ($"Download of Corretto from {url} failed.");
				return false;
			}

			return true;
		}

		bool CorrettoExistsAndIsValid (string installDir, out string installedVersion)
		{
			installedVersion = null;
			if (!Directory.Exists (installDir)) {
				Log.DebugLine ($"Corretto directory {installDir} does not exist");
				return false;
			}

			string corettoVersionFile = Path.Combine (installDir, "version.txt");
			if (!File.Exists (corettoVersionFile)) {
				Log.DebugLine ($"Corretto version file {corettoVersionFile} does not exist");
				return false;
			}

			string[] lines = File.ReadAllLines (corettoVersionFile);
			if (lines == null || lines.Length == 0) {
				Log.DebugLine ($"Corretto version file {corettoVersionFile} is empty, cannot determine version");
				return false;
			}

			string cv = lines [0].Trim ();
			if (String.IsNullOrEmpty (cv)) {
				Log.DebugLine ($"Corretto version is empty");
				return false;
			}
			installedVersion = cv;

			if (!Version.TryParse (cv, out Version cversion)) {
				Log.DebugLine ($"Unable to parse Corretto version from: {cv}");
				return false;
			}

			if (cversion != Configurables.Defaults.CorrettoVersion) {
				Log.DebugLine ($"Invalid Corretto version. Need {Configurables.Defaults.CorrettoVersion}, found {cversion}");
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
						Log.DebugLine ($"JDK file {file} missing from Corretto");
						return false;
					}
				}
			}

			return true;
		}
	}
}
