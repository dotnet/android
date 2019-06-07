using System;

namespace Xamarin.Android.Prepare
{
	partial class CMakeRunner
	{
		class OutputSink : ToolRunner.ToolOutputSink
		{
			public OutputSink (Log log, string logFilePath)
				: base (log, logFilePath)
			{}

			public override void WriteLine (string value)
			{
				base.WriteLine (value);
			}
		}
	}
}
