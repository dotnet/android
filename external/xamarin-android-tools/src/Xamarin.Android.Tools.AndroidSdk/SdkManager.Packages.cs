// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Xamarin.Android.Tools;

public partial class SdkManager
{
	public string? FindSdkManagerPath ()
	{
		if (string.IsNullOrEmpty (AndroidSdkPath))
			return null;

		var ext = OS.IsWindows ? ".bat" : string.Empty;
		var cmdlineToolsDir = Path.Combine (AndroidSdkPath, "cmdline-tools");

		if (Directory.Exists (cmdlineToolsDir)) {
			try {
				// Versioned dirs sorted descending, then "latest" as fallback
				var searchDirs = Directory.GetDirectories (cmdlineToolsDir)
					.Select (Path.GetFileName)
					.Where (n => n != "latest" && !string.IsNullOrEmpty (n))
					.OrderByDescending (n => Version.TryParse (n, out var v) ? v : new Version (0, 0))
					.Append ("latest");

				foreach (var dir in searchDirs) {
					var toolPath = Path.Combine (cmdlineToolsDir, dir!, "bin", "sdkmanager" + ext);
					if (File.Exists (toolPath))
						return toolPath;
				}
			} catch (Exception ex) {
				logger (TraceLevel.Verbose, $"Error enumerating cmdline-tools directories: {ex.Message}");
			}
		}

		// Legacy fallback: tools/bin/sdkmanager
		var legacyPath = Path.Combine (AndroidSdkPath, "tools", "bin", "sdkmanager" + ext);
		return File.Exists (legacyPath) ? legacyPath : null;
	}

	public async Task<(IReadOnlyList<SdkPackage> Installed, IReadOnlyList<SdkPackage> Available)> ListAsync (CancellationToken cancellationToken = default)
	{
		var sdkManagerPath = RequireSdkManagerPath ();
		logger (TraceLevel.Info, "Running sdkmanager --list...");
		var (exitCode, stdout, stderr) = await RunSdkManagerAsync (sdkManagerPath, new[] { "--list" }, cancellationToken: cancellationToken).ConfigureAwait (false);
		ThrowOnSdkManagerFailure (exitCode, "sdkmanager --list", stderr);
		return ParseSdkManagerList (stdout);
	}

	public async Task InstallAsync (IEnumerable<string> packages, bool acceptLicenses = true, CancellationToken cancellationToken = default)
	{
		var packageArray = ValidatePackages (packages);
		var sdkManagerPath = RequireSdkManagerPath ();
		logger (TraceLevel.Info, $"Installing packages: {string.Join (", ", packageArray)}");

		// Install one at a time to work around sdkmanager shell script quoting issues
		foreach (var package in packageArray) {
			logger (TraceLevel.Info, $"Installing package: {package}");
			var (exitCode, _, stderr) = await RunSdkManagerAsync (
				sdkManagerPath, new[] { package }, acceptLicenses, cancellationToken).ConfigureAwait (false);
			ThrowOnSdkManagerFailure (exitCode, $"Package installation ({package})", stderr);
		}
		logger (TraceLevel.Info, "Packages installed successfully.");
	}

	public async Task UninstallAsync (IEnumerable<string> packages, CancellationToken cancellationToken = default)
	{
		var packageArray = ValidatePackages (packages);
		var sdkManagerPath = RequireSdkManagerPath ();
		logger (TraceLevel.Info, $"Uninstalling packages: {string.Join (", ", packageArray)}");

		var args = new[] { "--uninstall" }.Concat (packageArray).ToArray ();
		var (exitCode, _, stderr) = await RunSdkManagerAsync (
			sdkManagerPath, args, cancellationToken: cancellationToken).ConfigureAwait (false);
		ThrowOnSdkManagerFailure (exitCode, "Package uninstall", stderr);
		logger (TraceLevel.Info, "Packages uninstalled successfully.");
	}

	public async Task UpdateAsync (CancellationToken cancellationToken = default)
	{
		var sdkManagerPath = RequireSdkManagerPath ();
		logger (TraceLevel.Info, "Updating all installed packages...");
		var (exitCode, _, stderr) = await RunSdkManagerAsync (
			sdkManagerPath, new[] { "--update" }, acceptLicenses: true, cancellationToken: cancellationToken).ConfigureAwait (false);
		ThrowOnSdkManagerFailure (exitCode, "Package update", stderr);
		logger (TraceLevel.Info, "All packages updated successfully.");
	}

	static string[] ValidatePackages (IEnumerable<string> packages)
	{
		if (packages is null)
			throw new ArgumentException ("At least one package must be specified.", nameof (packages));

		var array = packages.ToArray ();
		if (array.Length == 0)
			throw new ArgumentException ("At least one package must be specified.", nameof (packages));

		return array;
	}

	internal static (IReadOnlyList<SdkPackage> Installed, IReadOnlyList<SdkPackage> Available) ParseSdkManagerList (string output)
	{
		var installed = new List<SdkPackage> ();
		var available = new List<SdkPackage> ();
		List<SdkPackage>? target = null;

		foreach (var line in output.Split (new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)) {
			var trimmed = line.Trim ();

			if (trimmed.Contains ("Installed packages:")) { target = installed; continue; }
			if (trimmed.Contains ("Available Packages:")) { target = available; continue; }
			if (trimmed.Contains ("Available Updates:")) { target = null; continue; }

			if (target is null || trimmed.StartsWith ("Path", StringComparison.Ordinal) || trimmed.StartsWith ("---", StringComparison.Ordinal))
				continue;

			var parts = trimmed.Split ('|');
			if (parts.Length < 2)
				continue;

			var path = parts[0].Trim ();
			if (string.IsNullOrEmpty (path))
				continue;

			target.Add (new SdkPackage (
				path,
				Version: parts[1].Trim (),
				Description: parts.Length > 2 ? parts[2].Trim () : null,
				IsInstalled: target == installed
			));
		}

		return (installed.AsReadOnly (), available.AsReadOnly ());
	}
}
