using System;
using System.IO;
using System.Text;

namespace Xamarin.Android.Prepare
{
    class ProcessStandardStreamWrapper : TextWriter
    {
	    public enum LogLevel
	    {
		    Error,
		    Warning,
		    Info,
		    Message,
		    Debug,
	    }

	    Log Log = Log.Instance;
	    string indentString = " | ";

	    public bool IndentOutput         { get; set; } = true;
	    public LogLevel LoggingLevel     { get; set; } = LogLevel.Debug;
	    public string CustomSeverityName { get; set; }

	    public string IndentString {
		    get => indentString;
		    set {
			    indentString = value ?? String.Empty;
		    }
	    }

	    public override Encoding Encoding => Encoding.Default;

	    public ProcessStandardStreamWrapper ()
	    {}

	    public ProcessStandardStreamWrapper (IFormatProvider formatProvider)
		    : base (formatProvider)
        {}

	    public override void WriteLine (string value)
	    {
		    DoWrite (value, true);
	    }

	    protected virtual string PreprocessMessage (string message, ref bool writeLine)
	    {
		    return message;
	    }

	    void DoWrite (string message, bool writeLine)
	    {
		    Action<string, ConsoleColor, bool, string> writer;
		    ConsoleColor color;
		    bool showSeverity;

		    message = PreprocessMessage (message, ref writeLine) ?? String.Empty;
		    switch (LoggingLevel) {
			    case LogLevel.Error:
				    color = Log.ErrorColor;
				    showSeverity = Log.DefaultErrorShowSeverity;
				    if (writeLine)
					    writer = Log.ErrorLine;
				    else
					    writer = Log.Error;
				    break;

			    case LogLevel.Warning:
				    color = Log.WarningColor;
				    showSeverity = Log.DefaultWarningShowSeverity;
				    if (writeLine)
					    writer = Log.WarningLine;
				    else
					    writer = Log.Warning;
				    break;

			    case LogLevel.Info:
				    color = Log.InfoColor;
				    showSeverity = Log.DefaultInfoShowSeverity;
				    if (writeLine)
					    writer = Log.InfoLine;
				    else
					    writer = Log.Info;
				    break;

			    case LogLevel.Message:
				    color = Log.MessageColor;
				    showSeverity = Log.DefaultMessageShowSeverity;
				    if (writeLine)
					    writer = Log.MessageLine;
				    else
					    writer = Log.Message;
				    break;

			    case LogLevel.Debug:
				    color = Log.DebugColor;
				    showSeverity = Log.DefaultDebugShowSeverity;
				    if (writeLine)
					    writer = Log.DebugLine;
				    else
					    writer = Log.Debug;
				    break;

			    default:
				    throw new InvalidOperationException ($"Unsupported log level {LoggingLevel}");
		    }

		    if (IndentOutput)
			    writer ($"{IndentString}{message}", color, showSeverity, CustomSeverityName);
		    else
			    writer (message, color, showSeverity, CustomSeverityName);
	    }
    }
}
