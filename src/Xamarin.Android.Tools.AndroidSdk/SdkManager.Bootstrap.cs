// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Xamarin.Android.Tools;

public partial class SdkManager
{
	static readonly IProgress<SdkBootstrapProgress> NullProgress = new Progress<SdkBootstrapProgress> ();

	/// <summary>
	/// Downloads command-line tools from the manifest feed and extracts them to
	/// <c>&lt;targetPath&gt;/cmdline-tools/&lt;version&gt;/</c>.
	/// </summary>
	public async Task BootstrapAsync (string targetPath, IProgress<SdkBootstrapProgress>? progress = null, CancellationToken cancellationToken = default)
	{
		ThrowIfDisposed ();
		if (string.IsNullOrEmpty (targetPath))
			throw new ArgumentNullException (nameof (targetPath));

		progress ??= NullProgress;

		var cmdlineTools = await FindLatestCmdlineToolsAsync (progress, cancellationToken).ConfigureAwait (false);
		var tempArchivePath = Path.GetTempFileName ();

		try {
			await DownloadAndVerifyAsync (cmdlineTools, tempArchivePath, progress, cancellationToken).ConfigureAwait (false);

			var versionDir = Path.Combine (targetPath, "cmdline-tools", cmdlineTools.Revision);
			Directory.CreateDirectory (Path.Combine (targetPath, "cmdline-tools"));

			progress.Report (new SdkBootstrapProgress (SdkBootstrapPhase.Extracting, Message: "Extracting cmdline-tools..."));
			FileUtil.ExtractAndInstall (tempArchivePath, versionDir, "cmdline-tools", logger, cancellationToken);

			if (!OS.IsWindows)
				await FileUtil.SetExecutablePermissionsAsync (versionDir, logger, cancellationToken).ConfigureAwait (false);

			AndroidSdkPath = targetPath;
			progress.Report (new SdkBootstrapProgress (SdkBootstrapPhase.Complete, 100, "Bootstrap complete."));
			logger (TraceLevel.Info, "Android SDK bootstrap complete.");
		}
		finally {
			FileUtil.TryDeleteFile (tempArchivePath, logger);
		}
	}

	async Task<SdkManifestComponent> FindLatestCmdlineToolsAsync (IProgress<SdkBootstrapProgress> progress, CancellationToken cancellationToken)
	{
		progress.Report (new SdkBootstrapProgress (SdkBootstrapPhase.ReadingManifest, Message: "Reading manifest feed..."));
		logger (TraceLevel.Info, $"Reading manifest from {ManifestFeedUrl}...");

		var components = await GetManifestComponentsAsync (cancellationToken).ConfigureAwait (false);
		var cmdlineTools = components
			.Where (c => string.Equals (c.ElementName, "cmdline-tools", StringComparison.OrdinalIgnoreCase) && !c.IsObsolete)
			.OrderByDescending (c => Version.TryParse (c.Revision, out var v) ? v : new Version (0, 0))
			.FirstOrDefault ();

		if (cmdlineTools is null || string.IsNullOrEmpty (cmdlineTools.DownloadUrl))
			throw new InvalidOperationException ("Could not find command-line tools in the Android manifest feed.");

		logger (TraceLevel.Info, $"Found cmdline-tools {cmdlineTools.Revision}: {cmdlineTools.DownloadUrl}");
		return cmdlineTools;
	}

	async Task DownloadAndVerifyAsync (SdkManifestComponent component, string archivePath, IProgress<SdkBootstrapProgress> progress, CancellationToken cancellationToken)
	{
		progress.Report (new SdkBootstrapProgress (SdkBootstrapPhase.Downloading, Message: $"Downloading cmdline-tools {component.Revision}..."));
		await DownloadFileAsync (component.DownloadUrl!, archivePath, component.Size, progress, cancellationToken).ConfigureAwait (false);

		if (!string.IsNullOrEmpty (component.Checksum)) {
			progress.Report (new SdkBootstrapProgress (SdkBootstrapPhase.Verifying, Message: "Verifying checksum..."));
			DownloadUtils.VerifyChecksum (archivePath, component.Checksum!, component.ChecksumType);
			logger (TraceLevel.Info, "Checksum verification passed.");
		}
		else {
			logger (TraceLevel.Warning, "No checksum available; skipping verification.");
		}
	}
}
