// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Xamarin.Android.Tools;

public partial class SdkManager
{
	const string LatestCommandLineToolsPackage = "cmdline-tools;latest";

	/// <summary>
	/// Finds the installed <c>sdkmanager</c> from the highest command-line tools
	/// revision reported by <c>source.properties</c>.
	/// </summary>
	/// <returns>
	/// The selected executable and revision, or <see langword="null"/> when no compatible
	/// <c>sdkmanager</c> is installed.
	/// </returns>
	/// <remarks>
	/// The legacy <c>tools/bin/sdkmanager</c> location remains supported because it uses
	/// the same package-management command contract. Other command-line tools do not use
	/// this legacy fallback.
	/// </remarks>
	public CommandLineTool? FindSdkManager ()
	{
		var sdkPath = AndroidSdkPath;
		if (sdkPath is null || sdkPath.Length == 0)
			return null;

		var extension = OS.IsWindows ? ".bat" : "";
		return CommandLineToolsResolver.Find (
			sdkPath,
			"sdkmanager",
			extension,
			includeLegacy: true,
			logger: logger);
	}

	/// <summary>
	/// Finds the installed <c>sdkmanager</c> executable path.
	/// </summary>
	/// <returns>The selected executable path, or <see langword="null"/> when none is installed.</returns>
	public string? FindSdkManagerPath ()
	{
		return FindSdkManager ()?.Path;
	}

	/// <summary>
	/// Ensures the Android SDK contains the current Google <c>cmdline-tools;latest</c> package.
	/// </summary>
	/// <param name="targetPath">The Android SDK root path.</param>
	/// <param name="progress">Optional progress callback for bootstrap, catalog checking, and installation.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The selected <c>sdkmanager</c> executable and installed revision.</returns>
	public Task<CommandLineTool> EnsureLatestCommandLineToolsAsync (
		string targetPath,
		IProgress<SdkBootstrapProgress>? progress = null,
		CancellationToken cancellationToken = default)
	{
		ThrowIfDisposed ();
		if (string.IsNullOrEmpty (targetPath))
			throw new ArgumentNullException (nameof (targetPath));

		progress ??= NullProgress;
		return EnsureLatestCommandLineToolsAsync (
			targetPath,
			progress,
			cancellationToken,
			BootstrapAsync,
			ListAsync,
			(packages, acceptLicenses, token) => InstallAsync (packages, acceptLicenses, token));
	}

	internal async Task<CommandLineTool> EnsureLatestCommandLineToolsAsync (
		string targetPath,
		IProgress<SdkBootstrapProgress> progress,
		CancellationToken cancellationToken,
		Func<string, IProgress<SdkBootstrapProgress>?, CancellationToken, Task> bootstrapAsync,
		Func<CancellationToken, Task<(IReadOnlyList<SdkPackage> Installed, IReadOnlyList<SdkPackage> Available)>> listAsync,
		Func<IEnumerable<string>, bool, CancellationToken, Task> installAsync)
	{
		ThrowIfDisposed ();
		cancellationToken.ThrowIfCancellationRequested ();
		AndroidSdkPath = targetPath;

		var selected = FindSdkManager ();
		if (selected is null) {
			logger (System.Diagnostics.TraceLevel.Info, "No sdkmanager found; bootstrapping command-line tools.");
			await bootstrapAsync (
				targetPath,
				new BootstrapProgressForwarder (progress),
				cancellationToken).ConfigureAwait (false);
			AndroidSdkPath = targetPath;
			selected = FindSdkManager ();
			if (selected is null)
				throw new InvalidOperationException ("Android SDK bootstrap completed without installing sdkmanager.");
		}

		progress.Report (new SdkBootstrapProgress (
			SdkBootstrapPhase.CheckingForUpdates,
			Message: "Checking the latest command-line tools revision..."));
		var (installed, available) = await listAsync (cancellationToken).ConfigureAwait (false);
		SdkPackage? latestPackage = null;
		CommandLineToolsResolver.ParsedRevision? latestRevision = null;
		foreach (var package in available.Concat (installed)) {
			if (!string.Equals (package.Path, LatestCommandLineToolsPackage, StringComparison.Ordinal) ||
				!CommandLineToolsResolver.TryParseRevision (package.Version, out var packageRevision))
				continue;
			if (latestRevision.HasValue && packageRevision.CompareTo (latestRevision.Value) <= 0)
				continue;

			latestPackage = package;
			latestRevision = packageRevision;
		}
		if (latestPackage is null || !latestRevision.HasValue)
			throw new InvalidOperationException ("Could not determine the latest command-line tools revision from sdkmanager.");
		var catalogRevision = latestRevision.Value;

		var shouldInstall =
			!CommandLineToolsResolver.TryParseRevision (selected.Revision, out var selectedRevision) ||
			selectedRevision.CompareTo (catalogRevision) < 0;

		if (shouldInstall) {
			progress.Report (new SdkBootstrapProgress (
				SdkBootstrapPhase.Installing,
				Message: $"Installing {LatestCommandLineToolsPackage} {latestPackage.Version}..."));
			await installAsync ([LatestCommandLineToolsPackage], true, cancellationToken).ConfigureAwait (false);

			selected = FindSdkManager ();
			if (selected is null)
				throw new InvalidOperationException ("The latest command-line tools package was installed, but sdkmanager could not be found.");
			if (!CommandLineToolsResolver.TryParseRevision (selected.Revision, out selectedRevision))
				throw new InvalidOperationException ($"The installed sdkmanager revision '{selected.Revision ?? "unknown"}' could not be parsed.");
			if (selectedRevision.CompareTo (catalogRevision) < 0)
				throw new InvalidOperationException (
					$"The resolved command-line tools revision '{selected.Revision}' is older than the catalog revision '{latestPackage.Version}'.");
		} else {
			logger (System.Diagnostics.TraceLevel.Info, $"Command-line tools {selected.Revision} is already current.");
		}

		progress.Report (new SdkBootstrapProgress (
			SdkBootstrapPhase.Complete,
			100,
			$"Command-line tools {selected.Revision} are ready."));
		return selected;
	}

	sealed class BootstrapProgressForwarder : IProgress<SdkBootstrapProgress>
	{
		readonly IProgress<SdkBootstrapProgress> progress;

		public BootstrapProgressForwarder (IProgress<SdkBootstrapProgress> progress)
		{
			this.progress = progress;
		}

		public void Report (SdkBootstrapProgress value)
		{
			if (value.Phase != SdkBootstrapPhase.Complete)
				progress.Report (value);
		}
	}
}
