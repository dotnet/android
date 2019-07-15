using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	partial class SnRunner : ToolRunner
	{
		protected override string DefaultToolExecutableName => "sn";
		protected override string ToolName                  => "sn";

		public SnRunner (Context context, Log log = null, string snPath = null)
			: base (context, log, snPath)
		{}

		public async Task<bool> ReSign (string snkPath, string assemblyPath, string logTag, string workingDirectory = null)
		{
			if (String.IsNullOrEmpty (snkPath))
				throw new ArgumentException ("must not be null or empty", nameof (snkPath));

			if (String.IsNullOrEmpty (assemblyPath))
				throw new ArgumentException ("must not be null or empty", nameof (assemblyPath));

			if (String.IsNullOrEmpty (logTag))
				throw new ArgumentException ("must not be null or empty", nameof (logTag));

			if (String.IsNullOrEmpty (workingDirectory))
				workingDirectory = BuildPaths.XamarinAndroidSourceRoot;

			ProcessRunner runner = CreateProcessRunner ();
			runner.AddQuotedArgument ( "-R");
			runner.AddQuotedArgument (Utilities.GetRelativePath (workingDirectory, assemblyPath));
			runner.AddQuotedArgument (Utilities.GetRelativePath (workingDirectory, snkPath));

			string message = GetLogMessage (runner);
			Log.Info (message, CommandMessageColor);
			Log.StatusLine ();

			return await RunTool (
				() => {
					using (var outputSink = (OutputSink)SetupOutputSink (runner, $"sn.{logTag}")) {
						runner.WorkingDirectory = workingDirectory;
						return runner.Run ();
					}
				}
			);
		}

		protected override TextWriter CreateLogSink (string logFilePath)
		{
			return new OutputSink (Log, logFilePath);
		}
	}
}
