#nullable disable
using System;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
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
		public AbsolutePath? JdkPath { get; set; }

		public async Task<Archive> GetFirstValidJdkArchiveWithVersion (HttpClient httpClient, string version, CancellationToken cancellationToken)
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
			var url = new Uri ($"https://aka.ms/download-jdk/microsoft-jdk-{version}-{os}-{arch}.{extension}");

			// aka.ms sends an unknown link to an HTML page on bing.com with status 200, so an HTML response means that this JDK version does not exist.
			using (var response = await httpClient.GetAsync (url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)) {
				if (!response.IsSuccessStatusCode || response.Content.Headers.ContentType?.MediaType == "text/html")
					return null;
			}
			return new Archive { Url = url };
		}

		public bool GetJdkRevision (out Version version)
		{
			if (JdkPath is not AbsolutePath jdkPath) {
				version = new Version (0, 0);
				return false;
			}
			var javaVersion = InstallAndroidDependencies.ReadProperty (new AbsolutePath ("release", jdkPath), "JAVA_VERSION");
			bool foundUnparsedVersion = javaVersion != null;
			javaVersion = javaVersion?.Replace ("\"", string.Empty).Replace ("'", string.Empty);
			version = InstallAndroidDependencies.ParseVersion (javaVersion) ?? new Version (0, 0);
			return foundUnparsedVersion && version != new Version (0, 0);
		}

		public bool IsJdkPathValid ()
		{
			if (JdkPath is not AbsolutePath jdkPath) {
				return false;
			}

			// If the requested path is in a known VS installation Program Files path on Windows, request a different path
			if (RuntimeInformation.IsOSPlatform (OSPlatform.Windows)) {
				// Program Files is a machine location rather than a per-project setting.
#pragma warning disable MSBuildTask0005
				if (jdkPath.Value.StartsWith (Environment.GetFolderPath (Environment.SpecialFolder.ProgramFiles), StringComparison.OrdinalIgnoreCase)
					|| jdkPath.Value.StartsWith (Environment.GetFolderPath (Environment.SpecialFolder.ProgramFilesX86), StringComparison.OrdinalIgnoreCase)) {
#pragma warning restore MSBuildTask0005
					return false;
				}
			}

			// If the requested path is not writable, request a different path
			try {
				Directory.CreateDirectory (jdkPath);
				using var _ = File.Create (new AbsolutePath ("tmp", jdkPath), 1, FileOptions.DeleteOnClose);
			} catch (Exception) {
				return false;
			}

			return true;
		}

		public async Task InstallJdk (Archive archive, CancellationToken cancellationToken)
		{
			// The original code extracted into %TEMP%. Extract next to JdkPath instead, because Directory.Move
			// fails when %TEMP% and JdkPath are on different volumes.
			var jdkPath = JdkPath ?? throw new InvalidOperationException ("JdkPath must be set before installing the JDK.");
			var downloadedFile = new AbsolutePath (archive.DownloadedFilePath);
			var unzipDestination = new AbsolutePath (jdkPath.Value.TrimEnd (Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + "." + Path.GetRandomFileName ());
			Directory.CreateDirectory (unzipDestination);

			try {
				if (RuntimeInformation.IsOSPlatform (OSPlatform.Windows)) {
					ZipFile.ExtractToDirectory (downloadedFile, unzipDestination);
				} else {
					using var file = File.OpenRead (downloadedFile);
					using var gzip = new GZipStream (file, CompressionMode.Decompress);
					await TarFile.ExtractToDirectoryAsync (gzip, unzipDestination, overwriteFiles: false, cancellationToken);
				}

				var jdkSubDir = new AbsolutePath (Directory.GetDirectories (unzipDestination).First ());
				if (RuntimeInformation.IsOSPlatform (OSPlatform.OSX)) {
					jdkSubDir = new AbsolutePath (Path.Combine ("Contents", "Home"), jdkSubDir);
				}
				// Keep the existing JDK if the build was canceled during extraction.
				cancellationToken.ThrowIfCancellationRequested ();
				RemoveJdk ();
				Directory.Move (jdkSubDir, jdkPath);
			} finally {
				if (Directory.Exists (unzipDestination))
					Directory.Delete (unzipDestination, true);
			}
		}

		public void RemoveJdk ()
		{
			if (JdkPath is AbsolutePath jdkPath && Directory.Exists (jdkPath))
				Directory.Delete (jdkPath, true);
		}

	}
}