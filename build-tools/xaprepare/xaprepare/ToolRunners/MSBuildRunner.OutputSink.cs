using System;
using System.Text;

namespace Xamarin.Android.Prepare
{
	partial class MSBuildRunner
	{
		class OutputSink : ToolRunner.ToolOutputSink
		{
			public override Encoding Encoding => Encoding.Default;

			public OutputSink (Log log, string logFilePath)
				: base (log, logFilePath)
			{
			}

			public override void WriteLine (string value)
			{
				base.WriteLine (value);
			}
		}
	}
}
