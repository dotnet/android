using System;
using System.IO;
using System.Text;

namespace Xamarin.Android.Prepare
{
	abstract partial class ToolRunner
	{
		protected abstract class ToolOutputSink : TextWriter
		{
			protected Log Log             { get; }
			protected StreamWriter Writer { get; private set; }

			public override Encoding Encoding => Encoding.Default;

			protected ToolOutputSink (Log log, string logFilePath)
			{
				Log = log ?? throw new ArgumentNullException (nameof (log));
				if (!String.IsNullOrEmpty (logFilePath))
					Writer = new StreamWriter (File.Open (logFilePath, FileMode.Create, FileAccess.Write), Utilities.UTF8NoBOM);
			}

			public override void WriteLine (string value)
			{
				Writer?.WriteLine (value ?? String.Empty);
			}

			protected override void Dispose (bool disposing)
			{
				if (disposing && Writer != null) {
					Writer.Flush ();
					Writer.Dispose ();
					Writer = null;
				}

				base.Dispose (disposing);
			}
		}
    }
}
