// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Xamarin.Android.Tools;

public partial class SdkManager
{
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

		foreach (var package in array) {
			if (package is null || package.Length == 0 || package.Any (c =>
				!IsAsciiLetterOrDigit (c) && c != '.' && c != '-' && c != '_' && c != ';'))
				throw new ArgumentException ($"Invalid Android SDK package ID: '{package}'.", nameof (packages));
		}

		return array;
	}

	static bool IsAsciiLetterOrDigit (char value)
	{
		return value >= '0' && value <= '9' ||
			value >= 'A' && value <= 'Z' ||
			value >= 'a' && value <= 'z';
	}

	internal static (IReadOnlyList<SdkPackage> Installed, IReadOnlyList<SdkPackage> Available) ParseSdkManagerList (string output)
		=> SdkPackageListParser.Parse (output);
}
