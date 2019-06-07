using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	partial class TarRunner : ToolRunner
	{
		protected override string DefaultToolExecutableName => "tar";
		protected override string ToolName => "Tar";

		public TarRunner (Context context, Log log = null, string toolPath = null)
			: base (context, log, toolPath)
		{}

		public async Task<bool> Extract (string fullArchivePath, string destinationDirectory)
		{
			if (String.IsNullOrEmpty (fullArchivePath))
				throw new ArgumentException ("must not be null or empty", nameof (fullArchivePath));
			if (String.IsNullOrEmpty (destinationDirectory))
				throw new ArgumentException ("must not be null or empty", nameof (destinationDirectory));

			ProcessRunner runner = CreateProcessRunner ();
			runner.AddArgument ("-x");
			runner.AddArgument ("-f");
			runner.AddQuotedArgument (fullArchivePath);

			try {
				Log.StatusLine ($"Archive path: {fullArchivePath}");
				return await RunTool (
					() => {
						using (TextWriter outputSink = SetupOutputSink (runner, $"tar-extract-archive.{Path.GetFileName (fullArchivePath)}", "extracting archive")) {
							StartTwiddler ();
							runner.WorkingDirectory = destinationDirectory;
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
