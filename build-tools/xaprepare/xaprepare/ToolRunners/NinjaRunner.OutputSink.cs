using System;

namespace Xamarin.Android.Prepare
{
	partial class NinjaRunner
	{
		class OutputSink : ToolRunner.ToolOutputSink
		{
			bool writeToStderr;

			public ProcessRunner Runner { get; set; }

			public OutputSink (Log log, string logFilePath)
				: base (log, logFilePath)
			{}

			public override void WriteLine (string value)
			{
				base.WriteLine (value);

				if (!writeToStderr && value.StartsWith ("FAILED:", StringComparison.Ordinal)) {
					writeToStderr = true;
				}

				if (!writeToStderr) {
					return;
				}

				Runner?.WriteStderrLine (value);
			}
		}
	}
}
