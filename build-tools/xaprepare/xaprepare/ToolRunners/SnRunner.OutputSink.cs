using System.Text;

namespace Xamarin.Android.Prepare
{
	partial class SnRunner
	{
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
