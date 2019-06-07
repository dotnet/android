using System;

namespace Xamarin.Android.Prepare
{
	partial class GitRunner
	{
		class OutputSink : ToolRunner.ToolOutputSink
		{
			public Action<string> LineCallback { get; set; }

			public OutputSink (Log log, string logFilePath = null)
				: base (log, logFilePath)
			{
			}

			public override void WriteLine (string value)
			{
				base.WriteLine (value);
				LineCallback?.Invoke (value);
			}
		}
	}
}
