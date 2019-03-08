using System;
using System.IO;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	partial class NuGetRunner : ToolRunner
	{
		protected override string DefaultToolExecutableName => "nuget";
		protected override string ToolName                  => "NuGet";

		public NuGetRunner (Context context, Log log = null, string nugetPath = null)
			: base (context, log, nugetPath)
		{}

		public async Task<bool> Restore (string solutionFilePath)
		{
			if (String.IsNullOrEmpty (solutionFilePath))
				throw new ArgumentException ("must not be null or empty", nameof (solutionFilePath));

			if (!File.Exists (solutionFilePath))
				throw new InvalidOperationException ($"Solution file '{solutionFilePath}' does not exist");

			ProcessRunner runner = CreateProcessRunner ("restore");

			runner.AddArgument ("-Verbosity").AddArgument ("detailed");
			runner.AddArgument ("-NonInteractive");
			runner.AddArgument ("-ForceEnglishOutput");
			runner.AddQuotedArgument (solutionFilePath);

			try {
				return await RunTool (() => {
						using (TextWriter outputSink = SetupOutputSink (runner, $"nuget-restore.{Path.GetFileName (solutionFilePath)}", "restoring NuGet packages")) {
							Log.StatusLine ($"Solution file: {Utilities.GetRelativePath (BuildPaths.XamarinAndroidSourceRoot, solutionFilePath)}", ConsoleColor.White);
							runner.WorkingDirectory = Path.GetDirectoryName (solutionFilePath);
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
