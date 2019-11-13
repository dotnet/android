using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	partial class CMakeRunner : ToolRunner
	{
		protected override string ToolName                  => "CMake";
		protected override string DefaultToolExecutableName => "cmake";

		public CMakeRunner (Context context, Log log = null, string toolPath = null)
			: base (context, log, toolPath)
		{
			ProcessTimeout = TimeSpan.FromMinutes (60);
		}

		public async Task<bool> Run (string logTag, string sourceDirectory, string workingDirectory, List<string> arguments)
		{
			if (String.IsNullOrEmpty (logTag))
				throw new ArgumentException ("must not be null or empty", nameof (logTag));

			if (String.IsNullOrEmpty (sourceDirectory))
				throw new ArgumentException ("must not be null or empty", nameof (sourceDirectory));

			if (String.IsNullOrEmpty (workingDirectory))
				throw new ArgumentException ("must not be null or empty", nameof (workingDirectory));

			var runner = CreateProcessRunner ();
			AddArguments (runner, arguments);
			runner.AddQuotedArgument (Utilities.GetRelativePath (workingDirectory,sourceDirectory));

			try {
				return await RunTool (() => {
						using (var outputSink = (OutputSink)SetupOutputSink (runner, $"cmake.{logTag}")) {
							runner.WorkingDirectory = workingDirectory;
							StartTwiddler ();
							return runner.Run ();
						}
					}
				);
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
