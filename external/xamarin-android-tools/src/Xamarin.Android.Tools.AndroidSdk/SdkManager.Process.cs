// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Xamarin.Android.Tools;
public partial class SdkManager
{
	async Task<(int ExitCode, string Stdout, string Stderr)> RunSdkManagerAsync (
		string sdkManagerPath, string[] arguments, bool acceptLicenses = false, CancellationToken cancellationToken = default)
	{
		var argumentsStr = string.Join (" ", arguments);

		// On macOS/Linux the sdkmanager shell script uses 'save()'/'eval' which
		// concatenates individually-quoted arguments. Pass as a single Arguments
		// string so the script receives them correctly.
		var psi = OS.IsWindows
			? ProcessUtils.CreateProcessStartInfo (sdkManagerPath, arguments)
			: new ProcessStartInfo {
				FileName = sdkManagerPath,
				Arguments = argumentsStr,
				UseShellExecute = false,
				CreateNoWindow = true,
			};
		psi.RedirectStandardInput = acceptLicenses;

		var envVars = GetEnvironmentVariables ();

		using var stdout = new StringWriter ();
		using var stderr = new StringWriter ();

		Action<Process>? onStarted = null;
		if (acceptLicenses) {
			onStarted = process => {
				// Feed "y\n" continuously for license prompts
				Task.Run (async () => {
					try {
						while (!process.HasExited && !cancellationToken.IsCancellationRequested) {
							process.StandardInput.WriteLine ("y");
							await Task.Delay (StdinPollDelayMs, cancellationToken).ConfigureAwait (false);
						}
					}
					catch (Exception ex) {
						// Process may have exited or cancellation requested - expected behavior
						logger (TraceLevel.Verbose, $"Auto-accept loop ended: {ex.GetType ().Name}");
					}
				}, cancellationToken);
			};
		}

		logger (TraceLevel.Verbose, $"Running: {sdkManagerPath} {argumentsStr}");
		int exitCode;
		try {
			exitCode = await ProcessUtils.StartProcess (psi, stdout, stderr, cancellationToken, envVars, onStarted).ConfigureAwait (false);
		}
		catch (OperationCanceledException) {
			throw;
		}
		catch (Exception ex) {
			logger (TraceLevel.Error, $"Failed to run sdkmanager: {ex.Message}");
			logger (TraceLevel.Verbose, ex.ToString ());
			throw;
		}

		var stdoutStr = stdout.ToString ();
		var stderrStr = stderr.ToString ();

		if (exitCode != 0) {
			logger (TraceLevel.Warning, $"sdkmanager exited with code {exitCode}");
			logger (TraceLevel.Verbose, $"stdout: {stdoutStr}");
			logger (TraceLevel.Verbose, $"stderr: {stderrStr}");
		}

		return (exitCode, stdoutStr, stderrStr);
	}

	async Task DownloadFileAsync (string url, string destinationPath, long expectedSize, IProgress<SdkBootstrapProgress> progress, CancellationToken cancellationToken)
	{
		logger (TraceLevel.Info, $"Downloading {url}...");

		var downloadProgress = new Progress<(double percent, string message)> (p => {
				progress.Report (new SdkBootstrapProgress (SdkBootstrapPhase.Downloading, (int) p.percent, p.message));
		});

		await DownloadUtils.DownloadFileAsync (httpClient, url, destinationPath, expectedSize, downloadProgress, cancellationToken).ConfigureAwait (false);
		logger (TraceLevel.Info, $"Download complete: {destinationPath}");
	}

	Dictionary<string, string> GetEnvironmentVariables ()
	{
		var env = new Dictionary<string, string> {
			["ANDROID_USER_HOME"] = Path.Combine (
				Environment.GetFolderPath (Environment.SpecialFolder.UserProfile), ".android")
		};

		if (!string.IsNullOrEmpty (AndroidSdkPath))
			env[EnvironmentVariableNames.AndroidHome] = AndroidSdkPath!;

		if (!string.IsNullOrEmpty (JavaSdkPath))
			env[EnvironmentVariableNames.JavaHome] = JavaSdkPath!;

		return env;
	}

}
