using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Reflection;

namespace Xamarin.Android.Prepare
{
	partial class Log : IDisposable
	{
		static readonly char[] lineSplit = new [] { '\n' };

		static Log instance;
		static readonly object writeLock = new object ();

		public const ConsoleColor ErrorColor       = ConsoleColor.Red;
		public const ConsoleColor ErrorLeadColor   = ErrorColor;
		public const ConsoleColor ErrorTailColor   = ErrorColor;
		public const ConsoleColor WarningColor     = ConsoleColor.Yellow;
		public const ConsoleColor InfoColor        = ConsoleColor.Green;
		public const ConsoleColor InfoLeadColor    = ConsoleColor.White;
		public const ConsoleColor InfoTailColor    = InfoColor;
		public const ConsoleColor MessageColor     = ConsoleColor.Gray;
		public const ConsoleColor DebugColor       = ConsoleColor.DarkGray;
		public const ConsoleColor DestinationColor = ConsoleColor.Cyan;
		public const ConsoleColor StatusColor      = ConsoleColor.Gray;
		public const ConsoleColor StatusLeadColor  = StatusColor;
		public const ConsoleColor StatusTailColor  = ConsoleColor.White;

		public const bool DefaultErrorShowSeverity   = true;
		public const bool DefaultWarningShowSeverity = true;
		public const bool DefaultInfoShowSeverity    = false;
		public const bool DefaultMessageShowSeverity = false;
		public const bool DefaultDebugShowSeverity   = false;

		const LoggingVerbosity ErrorMinimumVerbosity = LoggingVerbosity.Quiet;
		const LoggingVerbosity WarningMinimumVerbosity = LoggingVerbosity.Quiet;
		const LoggingVerbosity InfoMinimumVerbosity = LoggingVerbosity.Quiet;
		const LoggingVerbosity MessageMinimumVerbosity = LoggingVerbosity.Normal;
		const LoggingVerbosity DebugMinimumVerbosity = LoggingVerbosity.Verbose;

		const string ErrorSeverity   = "  Error: ";
		const string WarningSeverity = "Warning: ";
		const string InfoSeverity    = "   Info: ";
		const string MessageSeverity = "Message: ";
		const string DebugSeverity   = "  Debug: ";

		static Context ctx;
		static LoggingVerbosity Verbosity => ctx != null ? ctx.LoggingVerbosity : Configurables.Defaults.LoggingVerbosity;

		public static Log Instance => instance;

		static bool UseColor => ctx?.UseColor ?? true;

		TextWriter logFileWriter;
		bool alreadyDisposed;
		Stopwatch watch;

		static Log ()
		{
			instance = new Log ();
		}

		public Log (string logFilePath = null)
		{
			InitOS ();
			SetLogFile (logFilePath);
			watch = new Stopwatch ();
			watch.Start ();
		}

		public static void SetContext (Context context)
		{
			if (context == null)
				throw new ArgumentNullException (nameof (context));
			ctx = context;
		}

		public void SetLogFile (string logFilePath)
		{
			CloseLogFile ();
			if (String.IsNullOrEmpty (logFilePath))
				return;

			logFileWriter = new StreamWriter (File.Open (logFilePath, FileMode.Create));
		}

		public void Todo (string text)
		{
			var caller = new StackFrame (1, true);
			MethodBase method = caller.GetMethod ();

			var sb = new StringBuilder ();
			sb.Append (method.DeclaringType.FullName);
			sb.Append ('.');
			sb.Append (method.Name);

			if (!String.IsNullOrEmpty (caller.GetFileName ())) {
				int lineNumber = caller.GetFileLineNumber ();
				int columnNumber = caller.GetFileColumnNumber ();
				bool haveLocation = false;

				if (lineNumber > 0 || columnNumber > 0) {
					haveLocation = true;
					sb.Append (" at ");
				} else
					sb.Append (" in ");

				sb.Append (caller.GetFileName ());

				if (haveLocation)
					sb.Append ($"({lineNumber},{columnNumber})");
			}
			DoWrite (WriteToConsole, ErrorMinimumVerbosity, String.Empty, "TODO:", ConsoleColor.Red);

			string message = $" {text}\n      From: {sb.ToString()}";
			DoWrite (WriteLineToConsole, ErrorMinimumVerbosity, String.Empty, message, ConsoleColor.Blue);
		}

		public void Status (string text, ConsoleColor color = StatusColor, bool skipLogFile = false)
		{
			DoWrite (WriteToConsole, ErrorMinimumVerbosity, String.Empty, text, color, skipLogFile);
		}

		public void Status (string lead, string tail, ConsoleColor leadColor = StatusLeadColor, ConsoleColor tailColor = StatusTailColor, bool skipLogFile = false)
		{
			DoWrite (WriteToConsole, ErrorMinimumVerbosity, String.Empty, lead, leadColor, skipLogFile);
			DoWrite (WriteToConsole, ErrorMinimumVerbosity, String.Empty, tail, tailColor, skipLogFile);
		}

		public void StatusLine (string line = null, ConsoleColor color = StatusColor, bool skipLogFile = false)
		{
			DoWrite (WriteLineToConsole, ErrorMinimumVerbosity, String.Empty, line, color, skipLogFile);
		}

		public void StatusLine (string lead, string tail, ConsoleColor leadColor = StatusLeadColor, ConsoleColor tailColor = StatusTailColor, bool skipLogFile = false)
		{
			DoWrite (WriteToConsole, ErrorMinimumVerbosity, String.Empty, lead, leadColor, skipLogFile);
			DoWrite (WriteLineToConsole, ErrorMinimumVerbosity, String.Empty, tail, tailColor, skipLogFile);
		}

		public void Error (string text, ConsoleColor color = ErrorColor, bool showSeverity = DefaultErrorShowSeverity, string customSeverityName = null)
		{
			string severity = showSeverity ? (customSeverityName ?? ErrorSeverity) : String.Empty;
			DoWrite (WriteToConsole, ErrorMinimumVerbosity, severity, text, color);
		}

		public void Error (string lead, string tail, ConsoleColor leadColor = ErrorLeadColor, ConsoleColor tailColor = ErrorTailColor, bool showSeverity = DefaultErrorShowSeverity, string customSeverityName = null)
		{
			string severity = showSeverity ? (customSeverityName ?? ErrorSeverity) : String.Empty;
			DoWrite (WriteToConsole, ErrorMinimumVerbosity, severity, lead, leadColor);
			DoWrite (WriteToConsole, ErrorMinimumVerbosity, String.Empty, tail, tailColor);
		}

		public void ErrorLine (string line = null, ConsoleColor color = ErrorColor, bool showSeverity = DefaultErrorShowSeverity, string customSeverityName = null)
		{
			string severity = showSeverity ? (customSeverityName ?? ErrorSeverity) : String.Empty;
			DoWrite (WriteLineToConsole, ErrorMinimumVerbosity, severity, line, color);
		}

		public void ErrorLine (string lead, string tail, ConsoleColor leadColor = ErrorLeadColor, ConsoleColor tailColor = ErrorTailColor, bool showSeverity = DefaultErrorShowSeverity, string customSeverityName = null)
		{
			string severity = showSeverity ? (customSeverityName ?? ErrorSeverity) : String.Empty;
			DoWrite (WriteToConsole, ErrorMinimumVerbosity, severity, lead, leadColor);
			DoWrite (WriteLineToConsole, ErrorMinimumVerbosity, String.Empty, tail, tailColor);
		}

		public void Warning (string text, ConsoleColor color = WarningColor, bool showSeverity = DefaultWarningShowSeverity, string customSeverityName = null)
		{
			string severity = showSeverity ? (customSeverityName ?? WarningSeverity) : String.Empty;
			DoWrite (WriteToConsole, WarningMinimumVerbosity, severity, text, color);
		}

		public void WarningLine (string line = null, ConsoleColor color = WarningColor, bool showSeverity = DefaultWarningShowSeverity, string customSeverityName = null)
		{
			string severity = showSeverity ? (customSeverityName ?? WarningSeverity) : String.Empty;
			DoWrite (WriteLineToConsole, WarningMinimumVerbosity, severity, line, color);
		}

		public void Info (string text, ConsoleColor color = InfoColor, bool showSeverity = DefaultInfoShowSeverity, string customSeverityName = null)
		{
			string severity = showSeverity ? (customSeverityName ?? InfoSeverity) : String.Empty;
			DoWrite (WriteToConsole, InfoMinimumVerbosity, severity, text, color);
		}

		public void Info (string lead, string tail, ConsoleColor leadColor = InfoLeadColor, ConsoleColor tailColor = InfoTailColor, bool showSeverity = DefaultInfoShowSeverity, string customSeverityName = null)
		{
			string severity = showSeverity ? (customSeverityName ?? InfoSeverity) : String.Empty;
			DoWrite (WriteToConsole, InfoMinimumVerbosity, severity, lead, leadColor);
			DoWrite (WriteToConsole, InfoMinimumVerbosity, String.Empty, tail, tailColor);
		}

		public void InfoLine (string line = null, ConsoleColor color = InfoColor, bool showSeverity = DefaultInfoShowSeverity, string customSeverityName = null)
		{
			string severity = showSeverity ? (customSeverityName ?? InfoSeverity) : String.Empty;
			DoWrite (WriteLineToConsole, InfoMinimumVerbosity, severity, line, color);
		}

		public void InfoLine (string lead, string tail, ConsoleColor leadColor = InfoLeadColor, ConsoleColor tailColor = InfoTailColor, bool showSeverity = DefaultInfoShowSeverity, string customSeverityName = null)
		{
			string severity = showSeverity ? (customSeverityName ?? InfoSeverity) : String.Empty;
			DoWrite (WriteToConsole, InfoMinimumVerbosity, severity, lead, leadColor);
			DoWrite (WriteLineToConsole, InfoMinimumVerbosity, String.Empty, tail, tailColor);
		}

		public void Message (string text, ConsoleColor color = MessageColor, bool showSeverity = DefaultMessageShowSeverity, string customSeverityName = null)
		{
			string severity = showSeverity ? (customSeverityName ?? MessageSeverity) : String.Empty;
			DoWrite (WriteToConsole, MessageMinimumVerbosity, severity, text, color);
		}

		public void MessageLine (string line = null, ConsoleColor color = MessageColor, bool showSeverity = DefaultMessageShowSeverity, string customSeverityName = null)
		{
			string severity = showSeverity ? (customSeverityName ?? MessageSeverity) : String.Empty;
			DoWrite (WriteLineToConsole, MessageMinimumVerbosity, showSeverity ? MessageSeverity : String.Empty, line, color);
		}

		public void Debug (string text, ConsoleColor color = DebugColor, bool showSeverity = DefaultDebugShowSeverity, string customSeverityName = null)
		{
			string severity = showSeverity ? (customSeverityName ?? DebugSeverity) : String.Empty;
			DoWrite (WriteToConsole, DebugMinimumVerbosity, severity, text, color);
		}

		public void DebugLine (string line = null, ConsoleColor color = DebugColor, bool showSeverity = DefaultDebugShowSeverity, string customSeverityName = null)
		{
			string severity = showSeverity ? (customSeverityName ?? DebugSeverity) : String.Empty;
			DoWrite (WriteLineToConsole, DebugMinimumVerbosity, severity, line, color);
		}

		void DoWrite (Action<LoggingVerbosity, string, ConsoleColor, bool> writer, LoggingVerbosity minimumVerbosity, string prefix, string message, ConsoleColor color, bool skipLogFile = false)
		{
			writer (minimumVerbosity, $"{prefix}{message}", color, skipLogFile);
		}

		void WriteLineToConsole (LoggingVerbosity minimumVerbosity, string message, ConsoleColor color, bool skipLogFile = false)
		{
			ConsoleColor fg = ConsoleColor.Gray;
			try {
				lock (writeLock) {
					if (UseColor && Verbosity >= minimumVerbosity) {
						fg = Console.ForegroundColor;
						Console.ForegroundColor = color;
					}

					WriteLineToConsole (minimumVerbosity, message, skipLogFile);
				}
			} finally {
				if (UseColor && Verbosity >= minimumVerbosity) {
					Console.ForegroundColor = fg;
				}
			}
		}

		void WriteLineToConsole (LoggingVerbosity minimumVerbosity, string message = null, bool skipLogFile = false)
		{
			lock (writeLock) {
				if (Verbosity >= minimumVerbosity) {
					Console.WriteLine (message ?? String.Empty);
				}

				if (skipLogFile)
					return;
				LogToFile (message ?? String.Empty, addNewLine: true);
			}
		}

		void WriteToConsole (LoggingVerbosity minimumVerbosity, string message, ConsoleColor color, bool skipLogFile = false)
		{
			ConsoleColor fg = ConsoleColor.Gray;
			try {
				lock (writeLock) {
					if (UseColor && Verbosity >= minimumVerbosity) {
						fg = Console.ForegroundColor;
						Console.ForegroundColor = color;
					}

					WriteToConsole (minimumVerbosity, message, skipLogFile);
				}
			} finally {
				if (UseColor && Verbosity >= minimumVerbosity) {
					Console.ForegroundColor = fg;
				}
			}
		}

		void WriteToConsole (LoggingVerbosity minimumVerbosity, string message = null, bool skipLogFile = false)
		{
			if (message == null)
				return;

			lock (writeLock) {
				if (Verbosity >= minimumVerbosity) {
					Console.Write (message);
				}

				if (skipLogFile)
					return;
				LogToFile (message, addNewLine: false);
			}
		}

		void LogToFile (string message, bool addNewLine)
		{
			if (logFileWriter == null)
				return;

			string stamp = watch.Elapsed.ToString ();
			string[] lines = message.Split (lineSplit);
			if (lines.Length > 1) {
				for (int i = 0; i < lines.Length - 1; i++) {
					logFileWriter.WriteLine (FormatMessage (lines [i]));
				}
			}
			logFileWriter.Write (FormatMessage (lines [lines.Length - 1]));

			if (addNewLine)
				logFileWriter.WriteLine ();

			string FormatMessage (string msg)
			{
				return $"[{stamp}] {msg}";
			}
		}

		void CloseLogFile ()
		{
			if (logFileWriter == null)
				return;

			logFileWriter.Flush ();
			logFileWriter.Dispose ();
			logFileWriter = null;
		}

#region IDisposable Support
		protected void Dispose (bool disposing)
		{
			if (!alreadyDisposed) {
				if (disposing) {
					CloseLogFile ();
					ShutdownOS ();
				}

				alreadyDisposed = true;
			}
		}

		public void Dispose ()
		{
			Dispose (true);
		}
#endregion
	}
}
