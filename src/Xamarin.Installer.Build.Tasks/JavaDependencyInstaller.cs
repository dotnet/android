using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.Android.Tools;

namespace Xamarin.Installer.Build.Tasks
{
	/// <summary>
	/// A simplified copy of JavaDependencyInstaller from the removed Xamarin.Installer.AndroidSDK project.
	/// It downloads the JDK from aka.ms/download-jdk in place of the Xamarin manifest, and it extracts the
	/// archive next to JdkPath in place of %TEMP%.
	/// </summary>
	public class JavaDependencyInstaller
	{
		public string JdkPath { get; set; }

		public Archive GetFirstValidJdkArchiveWithVersion (string version)
		{
			string os = RuntimeInformation.IsOSPlatform (OSPlatform.Windows) ? "windows" : RuntimeInformation.IsOSPlatform (OSPlatform.OSX) ? "macOS" : RuntimeInformation.IsOSPlatform (OSPlatform.Linux) ? "linux" : null;
			string arch = RuntimeInformation.OSArchitecture switch {
				Architecture.X64 => "x64",
				Architecture.Arm64 => "aarch64",
				_ => null,
			};
			if (os == null || arch == null)
				return null;
			string extension = RuntimeInformation.IsOSPlatform (OSPlatform.Windows) ? "zip" : "tar.gz";
			return new Archive { Url = new Uri ($"https://aka.ms/download-jdk/microsoft-jdk-{version}-{os}-{arch}.{extension}") };
		}

		public bool GetJdkRevision (out Version version)
		{
			if (string.IsNullOrEmpty (JdkPath)) {
				version = new Version (0, 0);
				return false;
			}
			var javaVersion = InstallAndroidDependencies.ReadProperty (Path.Combine (JdkPath, "release"), "JAVA_VERSION");
			bool foundUnparsedVersion = javaVersion != null;
			javaVersion = javaVersion?.Replace ("\"", string.Empty).Replace ("'", string.Empty);
			version = InstallAndroidDependencies.ParseVersion (javaVersion) ?? new Version (0, 0);
			return foundUnparsedVersion && version != new Version (0, 0);
		}

		public bool IsJdkPathValid ()
		{
			if (string.IsNullOrEmpty (JdkPath)) {
				return false;
			}

			// If the requested path is in a known VS installation Program Files path on Windows, request a different path
			if (RuntimeInformation.IsOSPlatform (OSPlatform.Windows)) {
				if (JdkPath.StartsWith (Environment.GetFolderPath (Environment.SpecialFolder.ProgramFiles), StringComparison.OrdinalIgnoreCase)
					|| JdkPath.StartsWith (Environment.GetFolderPath (Environment.SpecialFolder.ProgramFilesX86), StringComparison.OrdinalIgnoreCase)) {
					return false;
				}
			}

			// If the requested path is not writable, request a different path
			try {
				Directory.CreateDirectory (JdkPath);
				using var _ = File.Create (Path.Combine (JdkPath, "tmp"), 1, FileOptions.DeleteOnClose);
			} catch (Exception) {
				return false;
			}

			return true;
		}

		public async Task InstallJdk (Archive archive, CancellationToken cancellationToken)
		{
			// Extract next to JdkPath, so that Directory.Move does not move across volumes.
			var unzipDestination = Path.GetFullPath (JdkPath).TrimEnd (Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + "." + Path.GetRandomFileName ();
			Directory.CreateDirectory (unzipDestination);

			try {
				if (RuntimeInformation.IsOSPlatform (OSPlatform.Windows)) {
					ZipFile.ExtractToDirectory (archive.DownloadedFilePath, unzipDestination);
				} else {
					var tar = ProcessUtils.CreateProcessStartInfo ("/usr/bin/tar", "-xzf", archive.DownloadedFilePath);
					tar.WorkingDirectory = unzipDestination;
					var extractResult = new InstallAndroidDependencies.OutputTail ();
					int exitCode = await ProcessUtils.StartProcess (tar, extractResult, extractResult, cancellationToken);
					if (exitCode != 0)
						throw new Exception ($"Failed to extract temp Java SDK archive {archive.DownloadedFilePath}:\n{extractResult}");
				}

				var jdkSubDir = Directory.GetDirectories (unzipDestination).FirstOrDefault ();
				if (RuntimeInformation.IsOSPlatform (OSPlatform.OSX)) {
					jdkSubDir = Path.Combine (jdkSubDir, "Contents", "Home");
				}
				RemoveJdk ();
				Directory.Move (jdkSubDir, JdkPath);
			} finally {
				if (Directory.Exists (unzipDestination))
					Directory.Delete (unzipDestination, true);
			}
		}

		public void RemoveJdk ()
		{
			if (Directory.Exists (JdkPath))
				Directory.Delete (JdkPath, true);
		}

	}
}