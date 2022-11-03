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

		public AdbRunner (TaskLoggingHelper logger, string adbPath, string? deviceSerial = null)
			: base (logger, adbPath)
		{
			if (!String.IsNullOrEmpty (deviceSerial)) {
				initialParams = new string[] { "-s", deviceSerial };
			}
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
			runner.AddArgument ("shell");
			runner.AddArgument ("getprop");
			runner.AddArgument (propertyName);

			return await CaptureAdbOutput (runner);
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
						return runner.Run ();
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
