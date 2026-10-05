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
		cancellationToken.ThrowIfCancellationRequested ();
		var interactive = acceptLicenses || Array.IndexOf (arguments, "--licenses") >= 0;
		var psi = new ProcessStartInfo (sdkManagerPath) {
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardInput = interactive,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
		};
		foreach (var argument in arguments)
			psi.ArgumentList.Add (argument);
		var envVars = AndroidEnvironmentHelper.GetEnvironmentVariables (AndroidSdkPath, JavaSdkPath);
		foreach (var variable in envVars)
			psi.Environment [variable.Key] = variable.Value;

		logger (TraceLevel.Verbose, $"Running: {sdkManagerPath} {argumentsStr}");
		int exitCode;
		string stdoutStr, stderrStr;
		try {
			if (!interactive) {
				var result = await Process.RunAndCaptureTextAsync (psi, cancellationToken).ConfigureAwait (false);
				cancellationToken.ThrowIfCancellationRequested ();
				exitCode = result.ExitStatus.ExitCode;
				stdoutStr = result.StandardOutput;
				stderrStr = result.StandardError;
			} else {
				using var process = new Process { StartInfo = psi };
				process.Start ();
				using var execution = CancellationTokenSource.CreateLinkedTokenSource (cancellationToken);
				var exit = process.SafeHandle.WaitForExitOrKillOnCancellationAsync (execution.Token);
				var capture = process.ReadAllTextAsync (execution.Token);
				using var stdout = process.StandardOutput;
				using var stderr = process.StandardError;
				using var input = process.StandardInput;
				var answers = AnswerLicensesAsync ();
				try {
					var status = await exit.WaitAsync (cancellationToken).ConfigureAwait (false);
					execution.CancelAfter (TimeSpan.FromSeconds (30));
					var result = await capture.ConfigureAwait (false);
					cancellationToken.ThrowIfCancellationRequested ();
					exitCode = status.ExitCode;
					stdoutStr = result.StandardOutput;
					stderrStr = result.StandardError;
				} catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
					if (answers.IsFaulted)
						await answers.ConfigureAwait (false);
					throw new TimeoutException ("sdkmanager exited, but its redirected output did not close within 30 seconds.");
				} catch (OperationCanceledException ex) {
					throw new OperationCanceledException (ex.Message, ex, cancellationToken);
				} finally {
					execution.Cancel ();
					try {
						await answers.ConfigureAwait (false);
						await capture.ConfigureAwait (false);
					} catch (OperationCanceledException) when (execution.IsCancellationRequested) {
						// Stop the owned license-input loop and native readers when the command finishes.
					}
					await exit.WaitAsync (TimeSpan.FromSeconds (5)).ConfigureAwait (false);
				}

				async Task AnswerLicensesAsync ()
				{
					try {
						while (!exit.IsCompleted) {
							await input.WriteLineAsync ((acceptLicenses ? "y" : "n").AsMemory (), execution.Token).ConfigureAwait (false);
							await Task.Delay (StdinPollDelayMs, execution.Token).ConfigureAwait (false);
						}
					} catch (IOException) when (process.HasExited) {
						// The child can close stdin just before its native exit wait completes.
					} catch {
						execution.Cancel ();
						throw;
					}
				}
			}
		}
		catch (OperationCanceledException) {
			throw;
		}
		catch (Exception ex) {
			logger (TraceLevel.Error, $"Failed to run sdkmanager: {ex.Message}");
			logger (TraceLevel.Verbose, ex.ToString ());
			throw;
		}

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
}
