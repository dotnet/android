// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Xamarin.Android.Tools
{
	/// <summary>
	/// Provides JDK installation capabilities using the Microsoft Build of OpenJDK.
	/// Downloads from https://aka.ms/download-jdk with SHA-256 verification.
	/// See https://www.microsoft.com/openjdk for more information.
	/// </summary>
	public class JdkInstaller : IDisposable
	{
		const string DownloadUrlBase = "https://aka.ms/download-jdk";

		/// <summary>Gets the recommended JDK major version for .NET Android development.</summary>
		public static int RecommendedMajorVersion => 21;

		/// <summary>Gets the supported JDK major versions available for installation.</summary>
		public static IReadOnlyList<int> SupportedVersions { get; } = [ RecommendedMajorVersion ];

		static readonly IProgress<JdkInstallProgress> NullProgress = new Progress<JdkInstallProgress> ();

		readonly HttpClient httpClient = new();
		readonly Action<TraceLevel, string> logger;

		public JdkInstaller (Action<TraceLevel, string>? logger = null)
		{
			this.logger = logger ?? AndroidSdkInfo.DefaultConsoleLogger;
		}

		public void Dispose () => httpClient.Dispose ();

		/// <summary>Discovers available Microsoft OpenJDK versions for the current platform.</summary>
		public async Task<IReadOnlyList<JdkVersionInfo>> DiscoverAsync (CancellationToken cancellationToken = default)
		{
			var results = new List<JdkVersionInfo> ();

			foreach (var version in SupportedVersions) {
				cancellationToken.ThrowIfCancellationRequested ();
				try {
					var info = BuildVersionInfo (version);

					// Verify the download URL is valid with a HEAD request
					using var request = new HttpRequestMessage (HttpMethod.Head, info.DownloadUrl);
					using var response = await httpClient.SendAsync (request, cancellationToken).ConfigureAwait (false);

					if (!response.IsSuccessStatusCode) {
						logger (TraceLevel.Warning, $"JDK {version} not available: HEAD {info.DownloadUrl} returned {(int) response.StatusCode}");
						continue;
					}

					if (response.Content.Headers.ContentLength.HasValue)
						info.Size = response.Content.Headers.ContentLength.Value;

					if (response.RequestMessage?.RequestUri is not null)
						info.ResolvedUrl = response.RequestMessage.RequestUri.ToString ();

					info.Checksum = await DownloadUtils.FetchChecksumAsync (httpClient, info.ChecksumUrl, $"JDK {version}", logger, cancellationToken).ConfigureAwait (false);
					if (string.IsNullOrEmpty (info.Checksum))
						logger (TraceLevel.Warning, $"Could not fetch checksum for JDK {version}, integrity verification will be skipped.");

					results.Add (info);
					logger (TraceLevel.Info, $"Discovered {info.DisplayName} (size={info.Size})");
				}
				catch (OperationCanceledException) {
					throw;
				}
				catch (Exception ex) {
					logger (TraceLevel.Warning, $"Failed to discover JDK {version}: {ex.Message}");
					logger (TraceLevel.Verbose, ex.ToString ());
				}
			}

			return results.AsReadOnly ();
		}

		/// <summary>
		/// Downloads and installs a Microsoft OpenJDK.
		/// When running elevated, uses the platform installer (.msi/.pkg) which chooses its own install location;
		/// <paramref name="targetPath"/> is ignored in that case. When non-elevated, extracts to <paramref name="targetPath"/>.
		/// </summary>
		public async Task InstallAsync (int majorVersion, string targetPath, IProgress<JdkInstallProgress>? progress = null, CancellationToken cancellationToken = default)
		{
			progress ??= NullProgress;

			if (!SupportedVersions.Contains (majorVersion))
				throw new ArgumentException ($"JDK version {majorVersion} is not supported. Supported versions: {string.Join (", ", SupportedVersions)}", nameof (majorVersion));

			if (string.IsNullOrEmpty (targetPath))
				throw new ArgumentNullException (nameof (targetPath));

			// When elevated and a platform installer is available, use it and let the installer handle paths
			if (ProcessUtils.IsElevated () && FileUtil.GetInstallerExtension () is not null) {
				logger (TraceLevel.Info, "Running elevated — using platform installer (.msi/.pkg).");
				await InstallWithPlatformInstallerAsync (majorVersion, progress, cancellationToken).ConfigureAwait (false);
				return;
			}

			if (!FileUtil.IsTargetPathWritable (targetPath, logger)) {
				logger (TraceLevel.Error, $"Target path '{targetPath}' is not writable or is in a restricted location.");
				throw new ArgumentException ($"Target path '{targetPath}' is not writable or is in a restricted location.", nameof (targetPath));
			}

			var versionInfo = BuildVersionInfo (majorVersion);

			// Fetch checksum - required for supply-chain integrity
			var checksum = await DownloadUtils.FetchChecksumAsync (httpClient, versionInfo.ChecksumUrl, $"JDK {majorVersion}", logger, cancellationToken).ConfigureAwait (false);
			if (string.IsNullOrEmpty (checksum))
				throw new InvalidOperationException ($"Failed to fetch SHA-256 checksum for JDK {majorVersion}. Cannot verify download integrity.");
			versionInfo.Checksum = checksum;

			var tempArchivePath = Path.Combine (Path.GetTempPath (), $"microsoft-jdk-{majorVersion}-{Guid.NewGuid ()}{FileUtil.GetArchiveExtension ()}");

			try {
				// Download
				logger (TraceLevel.Info, $"Downloading Microsoft OpenJDK {majorVersion} from {versionInfo.DownloadUrl}");
				progress.Report (new JdkInstallProgress (JdkInstallPhase.Downloading, 0, $"Downloading Microsoft OpenJDK {majorVersion}..."));
				await DownloadUtils.DownloadFileAsync (httpClient, versionInfo.DownloadUrl, tempArchivePath, versionInfo.Size,
					new Progress<(double pct, string msg)> (p => progress.Report (new JdkInstallProgress (JdkInstallPhase.Downloading, p.pct, p.msg))),
					cancellationToken).ConfigureAwait (false);
				logger (TraceLevel.Info, $"Download complete: {tempArchivePath}");

				// Verify checksum
				progress.Report (new JdkInstallProgress (JdkInstallPhase.Verifying, 0, "Verifying SHA-256 checksum..."));
				DownloadUtils.VerifyChecksum (tempArchivePath, versionInfo.Checksum!);
				logger (TraceLevel.Info, "Checksum verified.");
				progress.Report (new JdkInstallProgress (JdkInstallPhase.Verifying, 100, "Checksum verified."));

				// Extract
				logger (TraceLevel.Info, $"Extracting JDK to {targetPath}");
				progress.Report (new JdkInstallProgress (JdkInstallPhase.Extracting, 0, "Extracting JDK..."));
				await ExtractArchiveAsync (tempArchivePath, targetPath, cancellationToken).ConfigureAwait (false);
				logger (TraceLevel.Info, "Extraction complete.");
				progress.Report (new JdkInstallProgress (JdkInstallPhase.Extracting, 100, "Extraction complete."));

				// Validate
				progress.Report (new JdkInstallProgress (JdkInstallPhase.Validating, 0, "Validating installation..."));
				if (!IsValid (targetPath)) {
					logger (TraceLevel.Error, $"JDK installation at '{targetPath}' failed validation.");
					FileUtil.TryDeleteDirectory (targetPath, "invalid installation", logger);
					throw new InvalidOperationException ($"JDK installation at '{targetPath}' failed validation. The extracted files may be corrupted.");
				}

				// Validation passed — commit the move by cleaning up any backup
				FileUtil.CommitMove (targetPath, logger);
				logger (TraceLevel.Info, $"Microsoft OpenJDK {majorVersion} installed successfully at {targetPath}");
				progress.Report (new JdkInstallProgress (JdkInstallPhase.Validating, 100, "Validation complete."));

				progress.Report (new JdkInstallProgress (JdkInstallPhase.Complete, 100, $"Microsoft OpenJDK {majorVersion} installed successfully."));
			}
			catch (OperationCanceledException) {
				throw;
			}
			catch (Exception ex) when (ex is not ArgumentException and not ArgumentNullException) {
				logger (TraceLevel.Error, $"JDK installation failed: {ex.Message}");
				logger (TraceLevel.Verbose, ex.ToString ());
				throw;
			}
			finally {
				FileUtil.TryDeleteFile (tempArchivePath, logger);
			}
		}

		/// <summary>Validates whether the path contains a valid JDK installation.</summary>
		public bool IsValid (string jdkPath)
		{
			if (string.IsNullOrEmpty (jdkPath) || !Directory.Exists (jdkPath))
				return false;

			try {
				var jdk = new JdkInfo (jdkPath, logger: logger);
				return jdk.Version is not null;
			}
			catch (Exception ex) {
				logger (TraceLevel.Warning, $"JDK validation failed for '{jdkPath}': {ex.Message}");
				logger (TraceLevel.Verbose, ex.ToString ());
				return false;
			}
		}

		async Task InstallWithPlatformInstallerAsync (int majorVersion, IProgress<JdkInstallProgress> progress, CancellationToken cancellationToken)
		{
			var installerExt = FileUtil.GetInstallerExtension ()!;
			var info = BuildVersionInfo (majorVersion, installerExt);

			// Fetch checksum before download for supply-chain integrity
			var checksum = await DownloadUtils.FetchChecksumAsync (httpClient, info.ChecksumUrl, $"JDK {majorVersion} installer", logger, cancellationToken).ConfigureAwait (false);
			if (string.IsNullOrEmpty (checksum))
				throw new InvalidOperationException ($"Failed to fetch SHA-256 checksum for JDK {majorVersion} installer. Cannot verify download integrity.");
			info.Checksum = checksum;

			var tempInstallerPath = Path.Combine (Path.GetTempPath (), $"microsoft-jdk-{majorVersion}-{Guid.NewGuid ()}{installerExt}");

			try {
				// Download installer
				logger (TraceLevel.Info, $"Downloading installer from {info.DownloadUrl}");
				progress.Report (new JdkInstallProgress (JdkInstallPhase.Downloading, 0, $"Downloading Microsoft OpenJDK {majorVersion} installer..."));
				await DownloadUtils.DownloadFileAsync (httpClient, info.DownloadUrl, tempInstallerPath, info.Size,
					new Progress<(double pct, string msg)> (p => progress.Report (new JdkInstallProgress (JdkInstallPhase.Downloading, p.pct, p.msg))),
					cancellationToken).ConfigureAwait (false);

				progress.Report (new JdkInstallProgress (JdkInstallPhase.Verifying, 0, "Verifying SHA-256 checksum..."));
				DownloadUtils.VerifyChecksum (tempInstallerPath, info.Checksum!);
				progress.Report (new JdkInstallProgress (JdkInstallPhase.Verifying, 100, "Checksum verified."));

				// Run the installer silently
				progress.Report (new JdkInstallProgress (JdkInstallPhase.Extracting, 0, "Running platform installer..."));
				await RunPlatformInstallerAsync (tempInstallerPath, cancellationToken).ConfigureAwait (false);

				logger (TraceLevel.Info, $"Microsoft OpenJDK {majorVersion} installed successfully via platform installer.");
				progress.Report (new JdkInstallProgress (JdkInstallPhase.Complete, 100, $"Microsoft OpenJDK {majorVersion} installed successfully."));
			}
			finally {
				FileUtil.TryDeleteFile (tempInstallerPath, logger);
			}
		}

		async Task RunPlatformInstallerAsync (string installerPath, CancellationToken cancellationToken)
		{
			var psi = OS.IsWindows
				? ProcessUtils.CreateProcessStartInfo ("msiexec", "/i", installerPath, "/quiet", "/norestart")
				: ProcessUtils.CreateProcessStartInfo ("/usr/sbin/installer", "-pkg", installerPath, "-target", "/");

			using var stdout = new StringWriter ();
			using var stderr = new StringWriter ();
			var exitCode = await ProcessUtils.StartProcess (psi, stdout: stdout, stderr: stderr, cancellationToken).ConfigureAwait (false);

			if (exitCode != 0) {
				var errorOutput = stderr.ToString ();
				logger (TraceLevel.Error, $"Installer failed (exit code {exitCode}): {errorOutput}");
				throw new InvalidOperationException ($"Platform installer failed with exit code {exitCode}: {errorOutput}");
			}
		}

		/// <summary>Removes a JDK installation at the specified path.</summary>
		public bool Remove (string jdkPath)
		{
			if (string.IsNullOrEmpty (jdkPath) || !Directory.Exists (jdkPath))
				return false;

			try {
				Directory.Delete (jdkPath, recursive: true);
				logger (TraceLevel.Info, $"Removed JDK at '{jdkPath}'.");
				return true;
			}
			catch (Exception ex) {
				logger (TraceLevel.Error, $"Failed to remove JDK at '{jdkPath}': {ex.Message}");
				logger (TraceLevel.Verbose, ex.ToString ());
				return false;
			}
		}

		static JdkVersionInfo BuildVersionInfo (int majorVersion, string? extensionOverride = null)
		{
			var os = GetMicrosoftOpenJDKOSName ();
			var arch = GetArchitectureName ();
			var ext = extensionOverride ?? FileUtil.GetArchiveExtension ();

			var filename = $"microsoft-jdk-{majorVersion}-{os}-{arch}{ext}";
			var downloadUrl = $"{DownloadUrlBase}/{filename}";
			var checksumUrl = $"{downloadUrl}.sha256sum.txt";
			var displayName = extensionOverride is not null
				? $"Microsoft OpenJDK {majorVersion} ({ext})"
				: $"Microsoft OpenJDK {majorVersion}";

			return new JdkVersionInfo (
				majorVersion: majorVersion,
				displayName: displayName,
				downloadUrl: downloadUrl,
				checksumUrl: checksumUrl
			);
		}

		async Task ExtractArchiveAsync (string archivePath, string targetPath, CancellationToken cancellationToken)
		{
			var targetParent = Path.GetDirectoryName (targetPath);
			if (string.IsNullOrEmpty (targetParent))
				targetParent = Path.GetTempPath ();
			else
				Directory.CreateDirectory (targetParent);
			var tempExtractPath = Path.Combine (targetParent, $".jdk-extract-{Guid.NewGuid ()}");

			try {
				Directory.CreateDirectory (tempExtractPath);

				if (OS.IsWindows)
					DownloadUtils.ExtractZipSafe (archivePath, tempExtractPath, cancellationToken);
				else
					await DownloadUtils.ExtractTarGzAsync (archivePath, tempExtractPath, logger, cancellationToken).ConfigureAwait (false);

				// Find the actual JDK root (archives contain a single top-level directory)
				var extractedDirs = Directory.GetDirectories (tempExtractPath);
				var jdkRoot = extractedDirs.Length == 1 ? extractedDirs [0] : tempExtractPath;

				// On macOS, the JDK is inside Contents/Home
				if (OS.IsMac) {
					var contentsHome = Path.Combine (jdkRoot, "Contents", "Home");
					if (Directory.Exists (contentsHome))
						jdkRoot = contentsHome;
				}

				FileUtil.MoveWithRollback (jdkRoot, targetPath, logger);
			}
			finally {
				FileUtil.TryDeleteDirectory (tempExtractPath, "temp extract directory", logger);
			}
		}

		static string GetMicrosoftOpenJDKOSName ()
		{
			if (OS.IsMac) return "macOS";
			if (OS.IsWindows) return "windows";
			if (OS.IsLinux) return "linux";
			throw new PlatformNotSupportedException ("Unsupported platform");
		}

		static string GetArchitectureName ()
		{
			return RuntimeInformation.OSArchitecture switch {
				Architecture.X64   => "x64",
				Architecture.Arm64 => "aarch64",
				_ => throw new PlatformNotSupportedException ($"Unsupported architecture: {RuntimeInformation.OSArchitecture}"),
			};
		}
	}
}
