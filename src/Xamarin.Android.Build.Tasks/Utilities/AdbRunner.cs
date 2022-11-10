using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	class AdbRunner : ToolRunner
	{
		class AdbOutputSink : ToolOutputSink
		{
			public Action<string>? LineCallback { get; set; }

			public AdbOutputSink (TaskLoggingHelper logger)
				: base (logger)
			{}

			public override void WriteLine (string? value)
			{
				base.WriteLine (value);
				LineCallback?.Invoke (value ?? String.Empty);
			}
		}

		string[]? initialParams;

		public int ExitCode { get; private set; }

		public AdbRunner (TaskLoggingHelper logger, string adbPath, string? deviceSerial = null)
			: base (logger, adbPath)
		{
			if (!String.IsNullOrEmpty (deviceSerial)) {
				initialParams = new string[] { "-s", deviceSerial };
			}
		}

		public async Task<bool> Pull (string remotePath, string localPath)
		{
			var runner = CreateAdbRunner ();
			runner.AddArgument ("pull");
			runner.AddArgument (remotePath);
			runner.AddArgument (localPath);

			return await RunAdb (runner);
		}

		public async Task<bool> Push (string localPath, string remotePath)
		{
			var runner = CreateAdbRunner ();
			runner.AddArgument ("push");
			runner.AddArgument (localPath);
			runner.AddArgument (remotePath);

			return await RunAdb (runner);
		}

		public async Task<(bool success, string output)> RunAs (string packageName, string command, params string[] args)
		{
			var shellArgs = new List<string> {
				packageName,
				command,
			};

			if (args != null && args.Length > 0) {
				shellArgs.AddRange (args);
			}

			return await Shell ("run-as", (IEnumerable<string>)shellArgs);
		}

		public async Task<(bool success, string output)> GetAppDataDirectory (string packageName)
		{
			return await RunAs (packageName, "/system/bin/sh", "-c", "pwd", "2>/dev/null");
		}

		public async Task<(bool success, string output)> Shell (string command, List<string> args)
		{
			return await Shell (command, (IEnumerable<string>)args);
		}

		public async Task<(bool success, string output)> Shell (string command, params string[] args)
		{
			return await Shell (command, (IEnumerable<string>)args);
		}

		async Task<(bool success, string output)> Shell (string command, IEnumerable<string> args)
		{
			var runner = CreateAdbRunner ();

			runner.AddArgument ("shell");
			runner.AddArgument (command);
			foreach (string arg in args) {
				runner.AddArgument (arg);
			}

			return await CaptureAdbOutput (runner);
		}

		public async Task<(bool success, string output)> GetPropertyValue (string propertyName)
		{
			var runner = CreateAdbRunner ();
			return await GetPropertyValue (runner, propertyName);
		}

		async Task<(bool success, string output)> GetPropertyValue (ProcessRunner runner, string propertyName)
		{
			runner.ClearArguments ();
			runner.ClearOutputSinks ();
			return await Shell ("getprop", propertyName);
		}

		async Task<(bool success, string output)> CaptureAdbOutput (ProcessRunner runner, bool firstLineOnly = false)
                {
                        string? outputLine = null;
                        List<string>? lines = null;

                        using (var outputSink = (AdbOutputSink)SetupOutputSink (runner, ignoreStderr: true)) {
                                outputSink.LineCallback = (string line) => {
                                        if (firstLineOnly) {
	                                        if (outputLine != null) {
                                                        return;
	                                        }
                                                outputLine = line.Trim ();
                                                return;
                                        }

                                        if (lines == null) {
                                                lines = new List<string> ();
                                        }
                                        lines.Add (line.Trim ());
                                };

                                if (!await RunAdb (runner, setupOutputSink: false)) {
                                        return (false, String.Empty);
                                }
                        }

                        if (firstLineOnly) {
                                return (true, outputLine ?? String.Empty);
                        }

                        return (true, lines != null ? String.Join (Environment.NewLine, lines) : String.Empty);
                }

		async Task<bool> RunAdb (ProcessRunner runner, bool setupOutputSink = true, bool ignoreStderr = true)
		{
			return await RunTool (
				() => {
					TextWriter? sink = null;
					if (setupOutputSink) {
						sink = SetupOutputSink (runner, ignoreStderr: ignoreStderr);
					}

					try {
						bool ret = runner.Run ();
						ExitCode = runner.ExitCode;
						return ret;
					} catch {
						ExitCode = -0xDEAD;
						throw;
					} finally {
						sink?.Dispose ();
					}
				}
			);
		}

		ProcessRunner CreateAdbRunner () => CreateProcessRunner (initialParams);

		protected override TextWriter CreateLogSink (TaskLoggingHelper logger)
		{
			return new AdbOutputSink (logger);
		}
	}
}
