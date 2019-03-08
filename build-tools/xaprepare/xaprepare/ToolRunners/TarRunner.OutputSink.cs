using System;
using System.Text;

namespace Xamarin.Android.Prepare
{
	partial class TarRunner
	{
		class OutputSink : ToolRunner.ToolOutputSink
		{
			public override Encoding Encoding => Encoding.Default;

			public OutputSink (Log log, string logFilePath, string indent = null)
				: base (log, logFilePath)
			{
				Log.Todo ("Implement parsing, if necessary");
			}

			public override void WriteLine (string value)
			{
				base.WriteLine (value);
				if (String.IsNullOrEmpty (value))
					return;
			}
		}
	}
}
