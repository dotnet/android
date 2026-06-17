using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;

using Kajabity.Tools.Java;
using Xamarin.Installer.AndroidSDK.Properties;
using Xamarin.Installer.Common;
using Xamarin.Installer.AndroidSDK.Common;
using System.Text;

namespace Xamarin.Installer.AndroidSDK
{
	/// <summary>
	/// A collection of Android SDK utilities
	/// </summary>
	public static class AndroidUtilities
	{
		const string PLACEHOLDER_DELIMITER = "|";

		static readonly Regex ndkNameRegex = new Regex (@"android-ndk-r(?<ver>\d+)(?<tag>\w*)-\w*", RegexOptions.Compiled);

		/// <summary>
		/// Parses a string with embedded placeholders to render a full directory path. Placeholders are
		/// placed between pipe characters and their names come from members of the <see cref="Environment.SpecialFolder"/>
		/// enumeration.
		/// </summary>
		/// <param name="dirPattern">pattern to parse</param>
		/// <returns></returns>
		public static string ParseDirectoryWithSpecialFolderPlaceholders (string dirPattern)
		{
			dirPattern = dirPattern.SafeTrim ();
			if (String.IsNullOrEmpty (dirPattern))
				return String.Empty;

			string ret = dirPattern;

			// A dumb way, but it's all we need
			foreach (string name in Enum.GetNames (typeof (Environment.SpecialFolder))) {
				string ph = PLACEHOLDER_DELIMITER + name + PLACEHOLDER_DELIMITER;
				// We only replace the placeholder at the start of the value, since that's the only place where it makes sense
				if (ret.IndexOf (ph, StringComparison.Ordinal) != 0)
					continue;
				ret = ret.Replace (ph, Environment.GetFolderPath ((Environment.SpecialFolder)Enum.Parse (typeof (Environment.SpecialFolder), name)));
			}

			return ret;
		}

		/// <summary>
		/// Reads Java properties from the sepcified file
		/// </summary>
		/// <param name="sourcePropertiesPath">path to the properties file</param>
		/// <returns>collection of properties, an instance of the <see cref="JavaProperties"/> class.</returns>
		public static JavaProperties ReadAndroidProperties (string sourcePropertiesPath)
		{
			if (String.IsNullOrEmpty (sourcePropertiesPath) || !File.Exists (sourcePropertiesPath))
				return new JavaProperties ();
			
			Encoding.RegisterProvider (CodePagesEncodingProvider.Instance);
			var ret = new JavaProperties ();
			try {
				using (var fs = File.Open (sourcePropertiesPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
					ret.Load (fs);
				}
			} catch (Exception ex) {
				Logger.Exception ("Failed to read Android properties from {0}", ex, sourcePropertiesPath);
				throw new InvalidOperationException (Resources.FailedToLoadSourceProperties, ex);
			}

			return ret;
		}

		/// <summary>
		/// Installs (unpacks) the specified Android SDK ZIP archive (<paramref name="archivePath"/>) in the directory
		/// specified in the <paramref name="targetBasePath"/> parameter. You can pass more path segments following the
		/// <paramref name="targetBasePath"/> parameter, they will be used to construct the final path rooted at <paramref name="targetBasePath"/>
		/// </summary>
		/// <param name="name">name of the archive/component, for informational purposes</param>
		/// <param name="archivePath">path to the ZIP archive</param>
		/// <param name="targetBasePath">top directory in which to install the archive</param>
		/// <param name="targetPaths">optional path segments to append to <paramref name="targetBasePath"/> to form the archive installation path</param>
		/// <returns></returns>
		public static bool InstallArchive (string name, string archivePath, string targetBasePath, params string[] targetPaths)
		{
			return InstallArchive (name, archivePath, false, targetBasePath, targetPaths);
		}

		/// <summary>
		/// Installs (unpacks) the specified Android SDK ZIP archive (<paramref name="archivePath"/>) in the directory
		/// specified in the <paramref name="targetBasePath"/> parameter. You can pass more path segments following the
		/// <paramref name="targetBasePath"/> parameter, they will be used to construct the final path rooted at <paramref name="targetBasePath"/> 
		/// If <paramref name="mainArchive"/> is set to true, the archive is unzipped without first creating a backup of the possibly existing 
		/// target directory. This is used to install the main (distribution) archive of the Android SDK.
		/// </summary>
		/// <param name="name">name of the archive/component, for informational purposes</param>
		/// <param name="archivePath">path to the ZIP archive</param>
		/// <param name="mainArchive">archive is the main/root archive of the Android SDK</param>
		/// <param name="targetBasePath">top directory in which to install the archive</param>
		/// <param name="targetPaths">optional path segments to append to <paramref name="targetBasePath"/> to form the archive installation path</param>
		/// <returns></returns>
		public static bool InstallArchive (string name, string archivePath, bool mainArchive, string targetBasePath, params string[] targetPaths)
		{
			string destDir;
			return InstallArchive (out destDir, name, archivePath, mainArchive, false, targetBasePath, targetPaths);
		}

		/// <summary>
		/// Installs (unpacks) the specified Android SDK ZIP archive (<paramref name="archivePath"/>) in the directory
		/// specified in the <paramref name="targetBasePath"/> parameter. You can pass more path segments following the
		/// <paramref name="targetBasePath"/> parameter, they will be used to construct the final path rooted at <paramref name="targetBasePath"/> 
		/// If <paramref name="mainArchive"/> is set to true, the archive is unzipped without first creating a backup of the possibly existing 
		/// target directory. This is used to install the main (distribution) archive of the Android SDK.
		/// If <paramref name="includeZipRootInCopy"/> is <c>false</c> then the method copies only the contents of the toplevel directory of the archive, instead of the
		/// entire contents of the archive. This is used with Android SDK archives who contain a single root directory and their target directory on disk is named differently.
		/// </summary>
		/// <param name="destinationDirectory">set to the destination directory on exit</param>
		/// <param name="name">name of the archive/component, for informational purposes</param>
		/// <param name="archivePath">path to the ZIP archive</param>
		/// <param name="mainArchive">archive is the main/root archive of the Android SDK</param>
		/// <param name="includeZipRootInCopy">include the archive's top-level directory in the copy if <c>true</c>, copy just its contents otherwise</param>
		/// <param name="targetBasePath">top directory in which to install the archive</param>
		/// <param name="targetPaths">optional path segments to append to <paramref name="targetBasePath"/> to form the archive installation path</param>
		/// <returns><c>true</c> if copy was successful, <c>false</c> otherwise</returns>
		public static bool InstallArchive (out string destinationDirectory, string name, string archivePath, bool mainArchive, bool includeZipRootInCopy, string targetBasePath, params string[] targetPaths)
		{
			destinationDirectory = null;
			Logger.Info ("Installing Android archive '{0}'", name);
			if (String.IsNullOrEmpty ("archivePath"))
				throw new ArgumentNullException ("archivePath");
			if (String.IsNullOrEmpty (targetBasePath))
				throw new ArgumentNullException ("targetBasePath");

			var rnd = new Random ();
			string componentUnzippedPath = null;

			// A small race here but I think we can accept the risks :)
			string temporaryPath = Path.Combine (Path.GetTempPath (), Path.GetRandomFileName ());
			try {
				componentUnzippedPath = CommonUtilities.Helpers.Unzip (temporaryPath, archivePath, AndroidSDKContext.Instance.UserName).RemoveTrailingDirectorySeparator ();
			} catch (Exception ex) {
				Logger.Exception (String.Format ("Exception caught while unzipping '{0}' in '{1}'", archivePath, temporaryPath), ex);
				componentUnzippedPath = null;
			}

			if (String.IsNullOrEmpty (componentUnzippedPath)) {
				Logger.SetOperationStatus (OperationStatus.Failure);
				Logger.Error ("Attempt to unzip Android archive '{0}' found in {1} failed.", name, archivePath);
				return false;
			}

			Logger.Debug ("Android archive '{0}' unpacked in directory '{1}'", name, componentUnzippedPath);
			destinationDirectory = CombinePaths (targetBasePath, targetPaths);
			if (includeZipRootInCopy)
				destinationDirectory = Path.Combine (destinationDirectory, Path.GetFileName (componentUnzippedPath));
			Logger.Debug ("Archive's destination path: {0}", destinationDirectory);
			string oldDestinationPath = null;

			if (!mainArchive && Directory.Exists (destinationDirectory)) {
				oldDestinationPath = destinationDirectory + ".old" + rnd.Next (Int32.MaxValue);
				Logger.Info ("Moving old directory '{0}' to '{1}'", destinationDirectory, oldDestinationPath);
				CommonUtilities.MoveDirectory (destinationDirectory, oldDestinationPath);
			}

			bool ret = true;
			try {
				int idx = destinationDirectory.LastIndexOf (Path.DirectorySeparatorChar);
				string leadingPath;
				if (idx > 1)
					leadingPath = destinationDirectory.Substring (0, idx);
				else
					leadingPath = destinationDirectory;

				if (!Directory.Exists (leadingPath)) {
					Logger.Info ("Creating Android component's parent path '{0}'", leadingPath);
					Directory.CreateDirectory (leadingPath);
				}
				Logger.Info ("Moving component to its destination path.");

				CommonUtilities.MoveDirectory (componentUnzippedPath, destinationDirectory, overwrite: true);
			} catch (Exception e) {
				ret = false;
				Logger.Exception ("Failure to move component to its destination path. Exception was thrown.", e);
				if (!String.IsNullOrEmpty (oldDestinationPath) && Directory.Exists (oldDestinationPath)) {
					Logger.Error ("Attempting to clean up.");
					Logger.Error ("Trying to remove directory '{0}'", destinationDirectory);
					CommonUtilities.DeleteDirectoryRecursively (destinationDirectory);

					try {
						CommonUtilities.MoveDirectory (oldDestinationPath, destinationDirectory);
					} catch (Exception ex) {
						// ignore
						Logger.Exception ("Failed to rename directory '{0}' to '{1}'", ex, oldDestinationPath, destinationDirectory);
					}
				}
			}

			bool logError = false;
			Exception rex = null;
			try {
				CommonUtilities.DeleteDirectoryRecursively (temporaryPath);
				if (!String.IsNullOrEmpty (oldDestinationPath))
					logError = !CommonUtilities.DeleteDirectoryRecursively (oldDestinationPath);
			} catch (Exception e) {
				logError = true;
				ret = false;
				rex = e;
				// ignore
			}
			if (logError) {
				string message = String.Format ("Failed to remove old destination directory '{0}'. User will have to remove it manually.", oldDestinationPath);
				if (rex != null)
					Logger.Exception (message, rex);
				else
					Logger.Error (message);
			}

			return ret;
		}

		internal static string CombinePaths (string targetBasePath, params string[] targetPaths)
		{
			if (targetPaths == null || targetPaths.Length == 0)
				return targetBasePath;
			else {
				var allPaths = new List<string> ();
				allPaths.Add (targetBasePath);
				allPaths.AddRange (targetPaths);
				return Path.Combine (allPaths.ToArray ());
			}
		}

		internal static bool ParsePlatformVersion (string source, out Version version, out string versionTag)
		{
			version = null;
			versionTag = String.Empty;

			source = (source ?? String.Empty).Trim ();
			if (String.IsNullOrEmpty (source))
				return false;

			try {
				if (source.Length >= 3) { // The minimum valid version is X.Y
					version = Version.Parse (source);
					return true;
				}
			} catch (FormatException) {
				// ignore - google introduced version numbers with letters in them, we deal with it below
			}

			string validVersion = null;
			for (int i = 0; i < source.Length; i++) {
				char ch = source[i];
				if (ch == '.' || Char.IsDigit (ch))
					continue;

				validVersion = source.Substring (0, i);
				versionTag = source.Substring (i);
			}

			if (!String.IsNullOrEmpty (validVersion))
				version = Version.Parse (validVersion);

			return true;
		}

		public static string GetPlatformOS ()
		{
			return PlatformToString (AndroidSDKContext.Instance.Platform);
		}

		public static string PlatformToString (AndroidSDKPlatform platform)
		{
			switch (platform) {
				case AndroidSDKPlatform.Linux:
					return "linux";

				case AndroidSDKPlatform.Mac:
					return "macosx";

				case AndroidSDKPlatform.Windows:
					return "windows";

				case AndroidSDKPlatform.Unknown:
				case AndroidSDKPlatform.Any:
					return String.Empty;

				default:
					throw new InvalidOperationException ($"Unknown SDK platform: {platform}");
			}
		}

		internal static AndroidSDKPlatform GetPlatformFromOS (string osName)
		{
			osName = osName?.Trim ();
			if (String.IsNullOrEmpty (osName))  // Some archives aren't OS-specific
				return AndroidSDKPlatform.Any;

			if (String.Compare (osName, "linux", StringComparison.OrdinalIgnoreCase) == 0)
				return AndroidSDKPlatform.Linux;

			if (String.Compare (osName, "macosx", StringComparison.OrdinalIgnoreCase) == 0)
				return AndroidSDKPlatform.Mac;

			if (String.Compare (osName, "windows", StringComparison.OrdinalIgnoreCase) == 0)
				return AndroidSDKPlatform.Windows;

			return AndroidSDKPlatform.Unknown;
		}

		internal static bool CheckWheterFilesExist (string root, string ownerName, List<string> paths, out double percentFound)
		{
			percentFound = 0;
			int found = 0;
			if (paths == null || paths.Count == 0)
				return false;

			foreach (string p in paths) {
				string path = p.SafeTrim ();
				if (String.IsNullOrEmpty (path))
					continue;
				path = Path.Combine (root, path);
				if (File.Exists (path))
					found++;
				else
					Logger.Info ("File '{0}' not found for '{1}'", path, ownerName);
			}

			if (found == 0)
				return false;

			percentFound = ((double)found * 100.0) / (double)paths.Count;

			return true;
		}

		internal static AndroidSystemImageAbi StringToAbi (string abi)
		{
			abi = abi.SafeTrim ();
			if (String.IsNullOrEmpty (abi))
				return AndroidComponentInfoSystemImage.DefaultAbi;

			if (String.Compare ("arm64-v8a", abi, StringComparison.OrdinalIgnoreCase) == 0)
				return AndroidSystemImageAbi.ARM64V8a;

			if (String.Compare ("armeabi-v7a", abi, StringComparison.OrdinalIgnoreCase) == 0)
				return AndroidSystemImageAbi.ARMV7a;

			if (String.Compare ("mips", abi, StringComparison.OrdinalIgnoreCase) == 0)
				return AndroidSystemImageAbi.Mips;

			if (String.Compare ("x86", abi, StringComparison.OrdinalIgnoreCase) == 0)
				return AndroidSystemImageAbi.X86;

			if (String.Compare ("x86_64", abi, StringComparison.OrdinalIgnoreCase) == 0)
				return AndroidSystemImageAbi.X86_64;

			throw new ArgumentOutOfRangeException (String.Format ("Unknown ABI '{0}'", abi));
		}

		/// <summary>
		/// Utility method to work around Google's decision not to include NDK version in the Android SDK repository manifest v11. Parsing is
		/// performed with the assumption that the NDK archive name follows a certain convention that matches the 
		/// <code>android-ndk-r(?&lt;ver&gt;\d+)(?&lt;tag&gt;\w*)-\w*</code> regex.
		/// </summary>
		/// <param name="element">manifest XML element for the NDK node</param>
		/// <param name="nsmgr">namespace manager initialized for the repository manifest</param>
		/// <param name="baseURL">Base URL to use when the NDK element has only relative URLs</param>
		/// <param name="release">receives the NDK release, if parse is successful (just the number)</param>
		/// <param name="releaseTag">receives the NDK release tag, if parse is successful (just the trailing non-digit characters)</param>
		public static void ParseNDKVersionFromGoogleManifest (XmlElement element, XmlNamespaceManager nsmgr, Uri baseURL, out string release, out string releaseTag)
		{
			// Conveniently, google don't include NDK version in the manifest, why bother? We'll need to try to parse
			// the URL for name...
			XmlNodeList urls = element.SelectNodes ("sdk:archives/sdk:archive/sdk:url", nsmgr);
			if (urls == null || urls.Count == 0)
				throw new InvalidOperationException ("Android SDK manifest has NDK element without URLs, unable to determine NDK version");

			Uri url = null;
			foreach (XmlNode n in urls) {
				string uri;
				var urlElement = n as XmlElement;
				if (n == null)
					continue;
				uri = urlElement.InnerXml.SafeTrim ();
				if (String.IsNullOrEmpty (uri))
					continue;
				if (!Uri.TryCreate (uri, UriKind.RelativeOrAbsolute, out url))
					continue;
				break;
			}

			if (url == null)
				throw new InvalidOperationException ("Android SDK manifest has NDK element without valid URLs, unable to determine NDK version");

			if (!url.IsAbsoluteUri)
				url = new Uri (baseURL, url);

			string ndkName = Path.GetFileName (url.LocalPath).SafeTrim ();
			if (String.IsNullOrEmpty (ndkName))
				throw new InvalidOperationException ("Android SDK has NDK element without valid archive name in the URL, unable to determine NDK version");

			Match match = ndkNameRegex.Match (ndkName);
			if (!match.Success)
				throw new InvalidOperationException ("Android SDK has NDK element with URLs in unsupported format, unable to determine NDK version");

			release = match.Groups["ver"].Value;
			releaseTag = match.Groups["tag"].Value;
		}
	}
}
