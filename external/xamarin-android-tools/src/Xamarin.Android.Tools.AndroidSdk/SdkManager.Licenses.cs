// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Xamarin.Android.Tools;

public partial class SdkManager
{
	/// <summary>
	/// Accepts all SDK licenses using <c>sdkmanager --licenses</c>.
	/// </summary>
	public Task AcceptLicensesAsync () => AcceptLicensesAsync (CancellationToken.None);

	/// <summary>
	/// Accepts all SDK licenses using <c>sdkmanager --licenses</c>.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	public async Task AcceptLicensesAsync (CancellationToken cancellationToken)
	{
		var sdkManagerPath = RequireSdkManagerPath ();
		logger (TraceLevel.Info, "Accepting SDK licenses...");
		var (exitCode, stdout, stderr) = await RunSdkManagerAsync (
			sdkManagerPath, new[] { "--licenses" }, acceptLicenses: true, cancellationToken: cancellationToken).ConfigureAwait (false);

		logger (TraceLevel.Verbose, $"sdkmanager --licenses exited with code {exitCode}.");

		if (exitCode != 0) {
			string output = (stdout ?? string.Empty) + Environment.NewLine + (stderr ?? string.Empty);

			bool alreadyAccepted =
				output.IndexOf ("all sdk package licenses accepted", StringComparison.OrdinalIgnoreCase) >= 0 ||
				output.IndexOf ("licenses have already been accepted", StringComparison.OrdinalIgnoreCase) >= 0 ||
				output.IndexOf ("no sdk licenses to review", StringComparison.OrdinalIgnoreCase) >= 0 ||
				output.IndexOf ("no licenses to review", StringComparison.OrdinalIgnoreCase) >= 0;

			if (!alreadyAccepted) {
				logger (TraceLevel.Error, $"sdkmanager --licenses failed with exit code {exitCode}. stderr: {stderr}");
				throw new InvalidOperationException ($"sdkmanager --licenses failed with exit code {exitCode}. stderr: {stderr}");
			}

			logger (TraceLevel.Info, "SDK licenses are already accepted.");
			return;
		}

		logger (TraceLevel.Info, "License acceptance complete.");
	}

	/// <summary>
	/// Gets pending licenses that need to be accepted, along with their full text.
	/// This allows IDEs and CLI tools to present licenses to the user before accepting.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A list of pending licenses with their ID and full text content.</returns>
	public async Task<IReadOnlyList<SdkLicense>> GetPendingLicensesAsync (CancellationToken cancellationToken = default)
	{
		var sdkManagerPath = RequireSdkManagerPath ();

		logger (TraceLevel.Verbose, "Checking for pending licenses...");

		var envVars = AndroidEnvironmentHelper.GetEnvironmentVariables (AndroidSdkPath, JavaSdkPath);

		// Run --licenses without auto-accept to get the license text
		var psi = ProcessUtils.CreateProcessStartInfo (sdkManagerPath, "--licenses");
		psi.RedirectStandardInput = true;

		using var stdout = new StringWriter ();
		using var stderr = new StringWriter ();

		// Send 'n' to decline all licenses so we just get the text
		Action<Process> onStarted = process => {
			Task.Run (async () => {
				try {
					while (!process.HasExited && !cancellationToken.IsCancellationRequested) {
						process.StandardInput.WriteLine ("n");
						await Task.Delay (StdinPollDelayMs, cancellationToken).ConfigureAwait (false);
					}
				}
				catch (Exception ex) {
					// Process may have exited - expected behavior when process completes
					logger (TraceLevel.Verbose, $"License check loop ended: {ex.GetType ().Name}");
				}
			}, cancellationToken);
		};

		try {
			await ProcessUtils.StartProcess (psi, stdout, stderr, cancellationToken, envVars, onStarted).ConfigureAwait (false);
		}
		catch (OperationCanceledException) {
			throw;
		}
		catch (Exception ex) {
			// sdkmanager may exit with non-zero when declining licenses - that's expected
			logger (TraceLevel.Verbose, $"License check exited non-zero (expected): {ex.GetType ().Name}");
		}

		return ParseLicenseOutput (stdout.ToString ());
	}

	/// <summary>
	/// Accepts specific licenses by ID.
	/// </summary>
	/// <param name="licenseIds">The license IDs to accept (e.g., "android-sdk-license").</param>
	public Task AcceptLicensesAsync (IEnumerable<string> licenseIds) => AcceptLicensesAsync (licenseIds, CancellationToken.None);

	/// <summary>
	/// Accepts specific licenses by ID.
	/// </summary>
	/// <param name="licenseIds">The license IDs to accept (e.g., "android-sdk-license").</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	public async Task AcceptLicensesAsync (IEnumerable<string> licenseIds, CancellationToken cancellationToken)
	{
		ThrowIfDisposed ();
		if (licenseIds is null || !licenseIds.Any ())
			return;

		if (string.IsNullOrEmpty (AndroidSdkPath))
			throw new InvalidOperationException ("AndroidSdkPath must be set before accepting individual licenses.");

		// Accept licenses by writing the hash to the licenses directory
		var licensesDir = Path.Combine (AndroidSdkPath!, "licenses");
		Directory.CreateDirectory (licensesDir);

		// Get pending licenses to find their hashes
		var pendingLicenses = await GetPendingLicensesAsync (cancellationToken).ConfigureAwait (false);
		var licenseIdSet = new HashSet<string> (licenseIds, StringComparer.OrdinalIgnoreCase);

		foreach (var license in pendingLicenses) {
			if (licenseIdSet.Contains (license.Id)) {
				var licensePath = Path.Combine (licensesDir, license.Id);
				// Compute hash of license text and write it
				var hash = ComputeLicenseHash (license.Text);
				File.WriteAllText (licensePath, $"\n{hash}");
				logger (TraceLevel.Info, $"Accepted license: {license.Id}");
			}
		}
	}

	/// <summary>
	/// Checks whether SDK licenses have been accepted by checking the licenses directory.
	/// </summary>
	/// <returns><c>true</c> if at least one license file exists; otherwise <c>false</c>.</returns>
	public bool AreLicensesAccepted ()
	{
		if (string.IsNullOrEmpty (AndroidSdkPath))
			return false;

		var licensesPath = Path.Combine (AndroidSdkPath, "licenses");
		if (!Directory.Exists (licensesPath))
			return false;

		return Directory.EnumerateFiles (licensesPath).Any ();
	}

	/// <summary>
	/// Parses the output of <c>sdkmanager --licenses</c> to extract license information.
	/// </summary>
	internal static IReadOnlyList<SdkLicense> ParseLicenseOutput (string output)
	{
		var licenses = new List<SdkLicense> ();
		var lines = output.Split (new[] { '\n' }, StringSplitOptions.None);

		string? currentLicenseId = null;
		var currentLicenseText = new StringBuilder ();
		bool inLicenseText = false;

		foreach (var rawLine in lines) {
			var line = rawLine.TrimEnd ('\r');

			// License header: "License android-sdk-license:"
			if (line.StartsWith ("License ", StringComparison.OrdinalIgnoreCase) && line.TrimEnd ().EndsWith (":")) {
				// Save previous license if any
				if (currentLicenseId is not null && currentLicenseText.Length > 0) {
					licenses.Add (new SdkLicense (currentLicenseId, currentLicenseText.ToString ().Trim ()));
				}

				var trimmedLine = line.TrimEnd ();
				currentLicenseId = trimmedLine.Substring (8, trimmedLine.Length - 9).Trim ();
				currentLicenseText.Clear ();
				inLicenseText = true;
				continue;
			}

			// End of license text when we see the accept prompt
			if (line.Contains ("Accept?") || line.Contains ("(y/N)")) {
				if (currentLicenseId is not null && currentLicenseText.Length > 0) {
					licenses.Add (new SdkLicense (currentLicenseId, currentLicenseText.ToString ().Trim ()));
				}
				currentLicenseId = null;
				currentLicenseText.Clear ();
				inLicenseText = false;
				continue;
			}

			// Accumulate license text
			if (inLicenseText && currentLicenseId is not null) {
				// Skip separator lines
				if (!line.TrimStart ().StartsWith ("-------", StringComparison.Ordinal)) {
					currentLicenseText.AppendLine (line);
				}
			}
		}

		// Add last license if not yet added
		if (currentLicenseId is not null && currentLicenseText.Length > 0) {
			licenses.Add (new SdkLicense (currentLicenseId, currentLicenseText.ToString ().Trim ()));
		}

		return licenses.AsReadOnly ();
	}

	static string ComputeLicenseHash (string licenseText)
	{
		var bytes = Encoding.UTF8.GetBytes (licenseText.Replace ("\r\n", "\n").Trim ());
		return DownloadUtils.ComputeHashString (ChecksumType.Sha1, bytes);
	}

}
