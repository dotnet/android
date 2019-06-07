using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	partial class NinjaRunner : ToolRunner
	{
		protected override string ToolName                  => "Ninja";
		protected override string DefaultToolExecutableName => "ninja";

		public NinjaRunner (Context context, Log log = null, string toolPath = null)
			: base (context, log, toolPath)
		{
			ProcessTimeout = TimeSpan.FromMinutes (60);
		}

		public async Task<bool> Run (string logTag, string workingDirectory, List<string> arguments = null)
		{
			if (String.IsNullOrEmpty (logTag))
				throw new ArgumentException ("must not be null or empty", nameof (logTag));
			if (String.IsNullOrEmpty (workingDirectory))
				throw new ArgumentException ("must not be null or empty", nameof (workingDirectory));

			ProcessRunner runner = CreateProcessRunner ();
			AddArguments (runner, arguments);

			bool haveConcurrency = false;
			bool haveChangeDir = false;
			if (arguments != null) {
				foreach (string a in arguments) {
					if (String.IsNullOrEmpty (a))
						continue;

					if (a.StartsWith ("-j", StringComparison.Ordinal))
						haveConcurrency = true;
					if (a.StartsWith ("-C", StringComparison.Ordinal))
						haveChangeDir = true;
				}
			}

			if (!haveChangeDir) {
				runner.AddQuotedArgument ($"-C{workingDirectory}");
			}

			if (!haveConcurrency && Context.MakeConcurrency > 1) {
				runner.AddArgument ($"-j{Context.MakeConcurrency}");
			}

			try {
				return await RunTool (() => {
						using (var outputSink = (OutputSink)SetupOutputSink (runner, $"ninja.{logTag}")) {
							outputSink.Runner = runner;
							runner.WorkingDirectory = workingDirectory;
							StartTwiddler ();
							return runner.Run ();
						}
					});
			} finally {
				StopTwiddler ();
			}
		}

		protected override TextWriter CreateLogSink (string logFilePath)
		{
			return new OutputSink (Log, logFilePath);
		}
	}
}
