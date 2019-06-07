using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	partial class MakeRunner : ToolRunner
	{
		Version version;

		protected override string ToolName => "Make";
		public Version Version             => version;
		public bool NoParallelJobs         { get; set; }

		public MakeRunner (Context context, Log log = null, string makePath = null)
			: base (context, log, makePath)
		{
			ProcessTimeout = TimeSpan.FromMinutes (60);
			string vs = VersionString?.Trim ();
			if (String.IsNullOrEmpty (vs) || !Version.TryParse (vs, out version))
				version = new Version (0, 0);
		}

		public async Task<bool> Run (string logTag, string workingDirectory = null, List<string> arguments = null)
		{
			if (String.IsNullOrEmpty (logTag))
				throw new ArgumentException ("must not be null or empty", nameof (logTag));

			bool haveChangeDirArg = false;
			bool haveConcurrency = false;
			ProcessRunner runner = CreateProcessRunner ();
			if (arguments != null && arguments.Count > 0) {
				foreach (string a in arguments) {
					string arg = a?.Trim ();
					if (String.IsNullOrEmpty (arg))
						continue;
					if (arg.StartsWith ("-C", StringComparison.Ordinal) || arg.StartsWith ("--directory", StringComparison.Ordinal))
						haveChangeDirArg = true;
					if (arg.StartsWith ("-j", StringComparison.Ordinal))
						haveConcurrency = true;
					runner.AddQuotedArgument (arg);
				}
			}

			SetStandardArguments (workingDirectory, haveChangeDirArg, haveConcurrency, runner);

			string message = GetLogMessage (runner);
			Log.Info (message, CommandMessageColor);
			Log.StatusLine ();

			try {
				return await RunTool (
					() => {
						using (var outputSink = (OutputSink)SetupOutputSink (runner, $"make.{logTag}")) {
							StartTwiddler ();
							runner.WorkingDirectory = workingDirectory;
							return runner.Run ();
						}
					}
				);
			} finally {
				StopTwiddler ();
			}
		}

		public void GetStandardArguments (ref List<string> arguments, string workingDirectory = null)
		{
			var args = new List<string> ();
			SetStandardArguments (workingDirectory, false, false, arg => args.Add (arg));

			if (args.Count == 0)
				return;

			if (arguments == null)
				arguments = args;
			else
				arguments.AddRange (args);
		}

		void SetStandardArguments (string workingDirectory, bool haveChangeDirArg, bool haveConcurrency, ProcessRunner runner)
		{
			SetStandardArguments (workingDirectory, haveChangeDirArg, haveConcurrency, arg => runner.AddArgument (arg));
		}

		void SetStandardArguments (string workingDirectory, bool haveChangeDirArg, bool haveConcurrency, Action<string> argumentSetter)
		{
			if (!haveChangeDirArg && !String.IsNullOrEmpty (workingDirectory)) {
				argumentSetter ("-C");
				argumentSetter (ProcessRunner.QuoteArgument (workingDirectory));
			}

			bool concurrencyUsed;
			if (!haveConcurrency && !NoParallelJobs && Context.MakeConcurrency > 1) {
				argumentSetter ($"-j{Context.MakeConcurrency}");
				concurrencyUsed = true;
			} else {
				concurrencyUsed = false;
			}

			if (concurrencyUsed && version.Major >= 4) {
				argumentSetter ("--output-sync=target");
			}
		}

		protected override TextWriter CreateLogSink (string logFilePath)
		{
			return new OutputSink (Log, logFilePath);
		}
	}
}
