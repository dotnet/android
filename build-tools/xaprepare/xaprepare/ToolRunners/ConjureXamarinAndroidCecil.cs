using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	partial class ConjureXamarinAndroidCecilRunner : ToolRunner
	{
		protected override string DefaultToolExecutableName => "dotnet";
		protected override string ToolName                  => "dotnet";

		public List<string> StandardArguments { get; }

		public ConjureXamarinAndroidCecilRunner (Context context, Log? log = null, string? msbuildPath = null)
			: base (context, log, msbuildPath)
		{
			ProcessTimeout = TimeSpan.FromMinutes (1);

			StandardArguments = new List<string> {
				Path.Combine (Configurables.Paths.BuildBinDir, "conjure-xamarin-android-cecil.dll"),
			};
		}

		public async Task<bool> Run (string logTag, List<string>? arguments = null, string? workingDirectory = null)
		{
			if (string.IsNullOrEmpty (logTag))
				throw new ArgumentException ("must not be null or empty", nameof (logTag));

			if (string.IsNullOrEmpty (workingDirectory))
				workingDirectory = BuildPaths.XamarinAndroidSourceRoot;

			ProcessRunner runner = CreateProcessRunner ();

			AddArguments (runner, StandardArguments);
			AddArguments (runner, arguments);

			string message = GetLogMessage (runner);
			Log.Info (message, CommandMessageColor);
			Log.StatusLine ();

			try {
				return await RunTool (
					() => {
						using (var outputSink = (OutputSink)SetupOutputSink (runner, $"conjurer.{logTag}")) {
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

		protected override TextWriter CreateLogSink (string? logFilePath)
		{
			return new OutputSink (Log, logFilePath);
		}

		class OutputSink : ToolRunner.ToolOutputSink
		{
			public override Encoding Encoding => Encoding.Default;

			public OutputSink (Log log, string? logFilePath)
				: base (log, logFilePath)
			{
			}
		}
	}
}
