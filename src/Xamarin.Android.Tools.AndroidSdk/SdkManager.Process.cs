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
			{
				using var process = new Process { StartInfo = psi };
				process.Start ();
				using var execution = CancellationTokenSource.CreateLinkedTokenSource (cancellationToken);
				if (Array.IndexOf (arguments, "--list") >= 0)
					execution.CancelAfter (TimeSpan.FromSeconds (30));
				using var reading = CancellationTokenSource.CreateLinkedTokenSource (execution.Token);
				using var answering = CancellationTokenSource.CreateLinkedTokenSource (execution.Token);
				var exit = process.SafeHandle.WaitForExitOrKillOnCancellationAsync (execution.Token);
				var capture = CaptureAsync ();
				using var stdout = process.StandardOutput;
				using var stderr = process.StandardError;
				var answers = interactive ? AnswerLicensesAsync () : Task.CompletedTask;
				var monitor = BoundDrainAsync ();
				try {
					var status = await exit.WaitAsync (cancellationToken).ConfigureAwait (false);
					await Task.WhenAll (capture, answers).ConfigureAwait (false);
					var result = await capture.ConfigureAwait (false);
					cancellationToken.ThrowIfCancellationRequested ();
					execution.Token.ThrowIfCancellationRequested ();
					exitCode = status.ExitCode;
					stdoutStr = result.StandardOutput;
					stderrStr = result.StandardError;
				} catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
					if (answers.IsFaulted)
						await answers.ConfigureAwait (false);
					if (execution.IsCancellationRequested)
						throw new TimeoutException ("sdkmanager --list did not complete within 30 seconds.");
					throw new TimeoutException ("sdkmanager exited, but its redirected output did not close within 30 seconds.");
				} catch (OperationCanceledException ex) {
					throw new OperationCanceledException (ex.Message, ex, cancellationToken);
				} finally {
					execution.Cancel ();
					reading.Cancel ();
					answering.Cancel ();
					try {
						await Task.WhenAll (capture, answers).ConfigureAwait (false);
					} catch (OperationCanceledException) when (execution.IsCancellationRequested) {
						// Stop the owned license-input loop and native readers when the command finishes.
					} finally {
						try {
							await exit.WaitAsync (TimeSpan.FromSeconds (5)).ConfigureAwait (false);
						} finally {
							await monitor.ConfigureAwait (false);
						}
					}
				}

				async Task<(string StandardOutput, string StandardError)> CaptureAsync ()
				{
					try {
						return await process.ReadAllTextAsync (reading.Token).ConfigureAwait (false);
					} catch (OperationCanceledException) when (reading.IsCancellationRequested) {
						throw;
					} catch {
						execution.Cancel ();
						throw;
					}
				}

				async Task BoundDrainAsync ()
				{
					await exit.ConfigureAwait (false);
					answering.Cancel ();
					reading.CancelAfter (TimeSpan.FromSeconds (30));
				}

				async Task AnswerLicensesAsync ()
				{
					try {
						try {
							using var input = process.StandardInput;
							while (!exit.IsCompleted) {
								await input.WriteLineAsync ((acceptLicenses ? "y" : "n").AsMemory (), answering.Token).ConfigureAwait (false);
								await Task.Delay (StdinPollDelayMs, answering.Token).ConfigureAwait (false);
							}
						} catch (IOException ex) {
							// Closing stdin can precede exit; keep observing the command and its output.
							logger (TraceLevel.Verbose, $"sdkmanager standard input closed: {ex.Message}");
						}
					} catch (OperationCanceledException) when (answering.IsCancellationRequested) {
						// Root completion stops feeding, not output capture.
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
