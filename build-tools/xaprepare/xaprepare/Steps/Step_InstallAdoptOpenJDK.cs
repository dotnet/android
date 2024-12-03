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
	abstract partial class Step_InstallOpenJDK : StepWithDownloadProgress, IBuildInventoryItem
	{
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

		bool AllowJIJavaHomeMatch = false;

		public Step_InstallOpenJDK (string description, bool allowJIJavaHomeMatch = false)
			: base (description)
		{
			AllowJIJavaHomeMatch = allowJIJavaHomeMatch;
		}

		protected   abstract    string  ProductName     {get;}
		protected   abstract    string  JdkInstallDir	{get;}
		protected   abstract    Version JdkVersion      {get;}
		protected   abstract    Version JdkRelease      {get;}
		protected   abstract    Uri     JdkUrl          {get;}
		protected   abstract    string  JdkCacheDir     {get;}
		protected   abstract    string  RootDirName     {get;}
		public string BuildToolName => ProductName;
		public string BuildToolVersion => JdkVersion.ToString ();

		protected override async Task<bool> Execute (Context context)
		{
			if (Directory.Exists (Configurables.Paths.OldOpenJDKInstallDir)) {
				Log.DebugLine ($"Found old OpenJDK directory at {Configurables.Paths.OldOpenJDKInstallDir}, removing");
				Utilities.DeleteDirectorySilent (Configurables.Paths.OldOpenJDKInstallDir);
			}

			AddToInventory ();

			string jdkInstallDir = JdkInstallDir;
			if (OpenJDKExistsAndIsValid (jdkInstallDir, out string? installedVersion)) {
				Log.Status ($"{ProductName} version ");
				Log.Status (installedVersion ?? "Unknown", ConsoleColor.Yellow);
				Log.StatusLine (" already installed in: ", jdkInstallDir, tailColor: ConsoleColor.Cyan);
				return true;
			}

			// Check for a JDK installed on CI with a matching major version to use for test jobs
			var jiJavaHomeVarValue = Environment.GetEnvironmentVariable ("JI_JAVA_HOME");
			if (AllowJIJavaHomeMatch && Directory.Exists (jiJavaHomeVarValue)) {
				jdkInstallDir = jiJavaHomeVarValue;
				OpenJDKExistsAndIsValid (jdkInstallDir, out string? installedJIVersion);
				if (!Version.TryParse (installedJIVersion, out Version? cversion) || cversion == null) {
					Log.DebugLine ($"Unable to parse {ProductName} version from: {installedJIVersion}");
					return false;
				}
				if (cversion.Major != JdkVersion.Major) {
					Log.DebugLine ($"Invalid {ProductName} version. Need {JdkVersion}, found {cversion}");
					return false;
				}
				Log.Status ($"{ProductName} with version ");
				Log.Status (installedVersion ?? "Unknown", ConsoleColor.Yellow);
				Log.StatusLine (" already installed in: ", jdkInstallDir, tailColor: ConsoleColor.Cyan);
				return true;
			}

			Log.StatusLine ($"{ProductName} {JdkVersion} r{JdkRelease} will be installed to {jdkInstallDir}");
			Uri jdkURL = JdkUrl;
			if (jdkURL == null)
				throw new InvalidOperationException ($"{ProductName} URL must not be null");

			string? packageName = GetPackageName (jdkURL);

			if (String.IsNullOrEmpty (packageName)) {
				Log.ErrorLine ($"Unable to extract file name from {ProductName} URL");
				return false;
			}

			string localPackagePath = Path.Combine (JdkCacheDir, packageName);
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
					Log.ErrorLine ($"${ProductName} root directory not found after unpacking: {RootDirName}");
					return false;
				}

				MoveContents (rootDir, jdkInstallDir);
				File.WriteAllText (Path.Combine (jdkInstallDir, XAVersionInfoFile), $"{JdkRelease}{Environment.NewLine}");
			} finally {
				Utilities.DeleteDirectorySilent (tempDir);
				// Clean up zip after extraction if running on a hosted azure pipelines agent.
				if (context.IsRunningOnHostedAzureAgent)
					Utilities.DeleteFileSilent (localPackagePath);
			}

			return true;
		}

		string? GetPackageName (Uri jdkURL)
		{
			string[] queryParams = jdkURL.Query.TrimStart ('?').Split (QuerySeparator, StringSplitOptions.RemoveEmptyEntries);
			if (queryParams.Length == 0) {
				if (jdkURL.Segments.Length > 0) {
					return jdkURL.Segments [jdkURL.Segments.Length-1];
				}
				Log.ErrorLine ($"Unable to extract file name from {ProductName} URL as it contains no query component");
				return null;
			}

			string? packageName = null;
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
			return packageName;
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

		bool OpenJDKExistsAndIsValid (string installDir, out string? installedVersion)
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

			string openJDKReleaseFile = Path.Combine (installDir, "release");
			if (!File.Exists (openJDKReleaseFile)) {
				Log.DebugLine ($"{ProductName} release file {openJDKReleaseFile} does not exist, cannot determine version");
				return false;
			}

			string[] lines = File.ReadAllLines (openJDKReleaseFile);
			if (lines == null || lines.Length == 0) {
				Log.DebugLine ($"{ProductName} release file {openJDKReleaseFile} is empty, cannot determine version");
				return false;
			}

			string? cv = null;
			foreach (string l in lines) {
				string line = l.Trim ();
				if (!line.StartsWith ("JAVA_VERSION=", StringComparison.Ordinal)) {
					continue;
				}

				cv = line.Substring (line.IndexOf ('=') + 1).Trim ('"');
				cv = cv.Replace ("_", ".");
				break;
			}

			if (String.IsNullOrEmpty (cv)) {
				Log.DebugLine ($"Unable to find version of {ProductName} in release file {openJDKReleaseFile}");
				return false;
			}

			installedVersion = cv;
			string xaVersionFile = Path.Combine (installDir, XAVersionInfoFile);
			if (!File.Exists (xaVersionFile)) {
				Log.DebugLine ($"Unable to find .NET for Android version file {xaVersionFile}");
				return false;
			}

			lines = File.ReadAllLines (xaVersionFile);
			if (lines == null || lines.Length == 0) {
				Log.DebugLine ($".NET for Android version file {xaVersionFile} is empty, cannot determine release version");
				return false;
			}

			string rv = lines[0].Trim ();
			if (String.IsNullOrEmpty (rv)) {
				Log.DebugLine ($".NET for Android version file {xaVersionFile} does not contain release version information");
				return false;
			}

			if (!Version.TryParse (cv, out Version? cversion) || cversion == null) {
				Log.DebugLine ($"Unable to parse {ProductName} version from: {cv}");
				return false;
			}

			if (cversion != JdkVersion) {
				Log.DebugLine ($"Invalid {ProductName} version. Need {JdkVersion}, found {cversion}");
				return false;
			}

			if (!Version.TryParse (rv, out cversion)) {
				Log.DebugLine ($"Unable to parse {ProductName} release version from: {rv}");
				return false;
			}

			if (cversion != JdkRelease) {
				Log.DebugLine ($"Invalid {ProductName} version. Need {JdkRelease}, found {cversion}");
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

		public void AddToInventory ()
		{
			if (!string.IsNullOrEmpty (BuildToolName) && !string.IsNullOrEmpty (BuildToolVersion) && !Context.Instance.BuildToolsInventory.ContainsKey (BuildToolName)) {
				Context.Instance.BuildToolsInventory.Add (BuildToolName, BuildToolVersion);
			}
		}
	}

	class Step_InstallMicrosoftOpenJDK : Step_InstallOpenJDK {

		const string _ProductName = "Microsoft OpenJDK";

		public Step_InstallMicrosoftOpenJDK (bool allowJIJavaHomeMatch = false)
			: base ($"Installing {_ProductName}", allowJIJavaHomeMatch)
		{
		}

		protected   override    string  ProductName      => _ProductName;
		protected   override    string  JdkInstallDir    => Configurables.Paths.OpenJDK17InstallDir;
		protected   override    Version JdkVersion       => Configurables.Defaults.MicrosoftOpenJDK17Version;
		protected   override    Version JdkRelease       => Configurables.Defaults.MicrosoftOpenJDK17Release;
		protected   override    Uri     JdkUrl           => Configurables.Urls.MicrosoftOpenJDK17;
		protected   override    string  JdkCacheDir      => Configurables.Paths.OpenJDK17CacheDir;
		protected   override    string  RootDirName      => Configurables.Defaults.MicrosoftOpenJDK17RootDirName;
	}
}
