using System;

namespace Xamarin.Android.Prepare
{
	partial class MakeRunner
	{
		class OutputSink : ToolRunner.ToolOutputSink
		{
			public OutputSink (Log log, string? logFilePath)
				: base (log, logFilePath)
			{}
		}
	}
}
