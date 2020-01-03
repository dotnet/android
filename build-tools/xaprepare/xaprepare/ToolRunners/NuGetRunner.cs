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
			: base (context, log, nugetPath ?? Configurables.Paths.LocalNugetPath)
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

		public async Task<bool> Install (string packageId, string outputDirectory, string packageVersion = null)
		{
			if (String.IsNullOrEmpty (packageId))
				throw new ArgumentException ("must not be null or empty", nameof (packageId));

			if (String.IsNullOrEmpty (outputDirectory))
				throw new ArgumentException ("must not be null or empty", nameof (outputDirectory));

			ProcessRunner runner = CreateProcessRunner ("install");
			runner.AddArgument (packageId);
			runner.AddArgument ("-OutputDirectory").AddArgument (outputDirectory);
			runner.AddArgument ("-Verbosity").AddArgument ("detailed");
			runner.AddArgument ("-NonInteractive");
			runner.AddArgument ("-ForceEnglishOutput");

			if (!String.IsNullOrEmpty (packageVersion)) {
				runner.AddArgument ("-Version").AddArgument (packageVersion);
			}

			try {
				return await RunTool (() => {
					using (TextWriter outputSink = SetupOutputSink (runner, $"nuget-install.{packageId}", "installing NuGet packages")) {
						Log.StatusLine ($"Installing package '{packageId}' to {outputDirectory}", ConsoleColor.White);
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
