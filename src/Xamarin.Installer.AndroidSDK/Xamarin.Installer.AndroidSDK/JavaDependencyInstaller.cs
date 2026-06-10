using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Kajabity.Tools.Java;

using Xamarin.Installer.Common;
using Xamarin.Installer.AndroidSDK.Common;
using Xamarin.Installer.AndroidSDK.Manager;
using Xamarin.Installer.AndroidSDK.Xamarin;

namespace Xamarin.Installer.AndroidSDK
{
	public class JavaDependencyInstaller
	{
		public Repository Repository { get; private set; }

		public string JdkPath { get; set; }

		public JavaDependencyInstaller (IHelpers helpers = null, Uri manifestURL = null, bool useManifestCaching = false)
		{
			CommonUtilities.Helpers = helpers ?? new Helper ();
			Repository = new XamarinRepository (manifestURL, useManifestCaching);
		}

		public void Discover ()
		{
			if (!Repository.Parsed) {
				Repository.Parse ();
			}
		}

		public IEnumerable<JdkArchive> GetJdkArchivesWithVersion (AndroidRevision version)
		{
			return Repository.JdkComponents.Where (j => j.Revision == version).SelectMany (j => j.Archives);
		}

		public JdkArchive GetFirstValidJdkArchiveWithVersion (AndroidRevision version)
		{
			var jdks = GetJdkArchivesWithVersion (version);
			return jdks.FirstOrDefault (j => j.IsValidForSystem ());
		}

		public bool GetJdkRevision (out AndroidRevision version)
		{
			if (string.IsNullOrEmpty (JdkPath)) {
				version = new AndroidRevision (0);
				return false;
			}
			JavaProperties props = AndroidUtilities.ReadAndroidProperties (Path.Combine (JdkPath, "release"));
			bool foundUnparsedVersion = props.GetProperty ("JAVA_VERSION", out string javaVersion);
			javaVersion = javaVersion?.Replace ("\"", string.Empty).Replace ("'", string.Empty);
			version = new AndroidRevision (javaVersion);
			return foundUnparsedVersion && version != new AndroidRevision (0);
		}

		public bool IsJdkPathValid ()
		{
			if (string.IsNullOrEmpty (JdkPath)) {
				return false;
			}

			// If the requested path is in a known VS installation Program Files path on Windows, request a different path
			if (Platform.IsWindows) {
				if (JdkPath.StartsWith (Environment.GetFolderPath (Environment.SpecialFolder.ProgramFiles), StringComparison.OrdinalIgnoreCase)
					|| JdkPath.StartsWith (Environment.GetFolderPath (Environment.SpecialFolder.ProgramFilesX86), StringComparison.OrdinalIgnoreCase)) {
					return false;
				}
			}

			// If the requested path is not writable, request a different path
			try {
				Directory.CreateDirectory (JdkPath);
#pragma warning disable CS0642 // Possible mistaken empty statement
				using (File.Create (Path.Combine (JdkPath, "tmp"), 1, FileOptions.DeleteOnClose));
#pragma warning restore CS0642
			} catch (Exception) {
				return false;
			}

			return true;
		}

		public void InstallJdk (JdkArchive archive)
		{
			var unzipDestination = Path.Combine (Path.GetTempPath (), Path.GetRandomFileName ());
			Directory.CreateDirectory (unzipDestination);

			if (Platform.IsWindows) {
				CommonUtilities.Helpers.Unzip (unzipDestination, archive.DownloadedFilePath, null, null);
			} else {
				var extractResult = CommonUtilities.RunCommand (
					workingDirectory: unzipDestination,
					command: "/usr/bin/tar",
					exitCode: out int exitCode,
					arguments: new [] { "-xzf", archive.DownloadedFilePath });
				if (exitCode != 0)
					throw new Exception ($"Failed to extract temp Java SDK archive {archive.DownloadedFilePath}:\n{extractResult}");
			}

			var jdkSubDir = Directory.GetDirectories (unzipDestination).FirstOrDefault ();
			if (Platform.IsMac) {
				jdkSubDir = Path.Combine (jdkSubDir, "Contents", "Home");
			}
			RemoveJdk ();
			CommonUtilities.MoveDirectory (jdkSubDir, JdkPath);
			if (Directory.Exists (unzipDestination))
				Directory.Delete (unzipDestination, true);
		}

		public void RemoveJdk ()
		{
			if (Directory.Exists (JdkPath))
				Directory.Delete (JdkPath, true);
		}

	}
}
