using System;
using System.Text;

namespace Xamarin.Android.Prepare
{
	partial class SevenZipRunner
	{
		class OutputSink : ToolRunner.ToolOutputSink
		{
			public override Encoding Encoding => Encoding.Default;

			public OutputSink (Log log, string logFilePath, string indent = null)
				: base (log, logFilePath)
			{}

			public override void WriteLine (string value)
			{
				base.WriteLine (value);
				if (String.IsNullOrEmpty (value))
					return;

				// 7zip poses a small problem - its progress indicator is a single line which is rewritten repeatedly by
				// writing enough backspace ASCII characters to remove previous text and replace it with the updated
				// progress message. This means we will NOT see the updates until the process is done...
			}
		}
	}
}
