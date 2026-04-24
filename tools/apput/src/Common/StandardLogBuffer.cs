using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ApplicationUtility;

/// <summary>
/// Default <see cref="LogBuffer"/> implementation that writes colored messages to the console
/// and plain messages to an optional log file, with support for context-based indentation.
/// </summary>
class StandardLogBuffer : LogBuffer, IDisposable
{
	sealed class ContextFrame
	{
		public string? SavedIndent;
		public string? Name;
	}

	const ConsoleColor ErrorColor    = ConsoleColor.Red;
	const ConsoleColor WarningColor  = ConsoleColor.Yellow;
	const ConsoleColor InfoColor     = ConsoleColor.Green;
	const ConsoleColor DebugColor    = ConsoleColor.Gray;
	const ConsoleColor StandardColor = ConsoleColor.White;

	bool disposed;

	sealed class LogMessage
	{
		public string? Message;
		public string? Indent;
		public LogLevel Level;
		public bool WriteLine;
		public bool DoNotTag;
	}

	readonly List<LogMessage> pendingMessages = new ();
	readonly bool consoleStderrHasColor;
	readonly bool consoleStdoutHasColor;

	StreamWriter? logFileWriter;
	string? logFilePath;
	bool logFileOpenFailed;
	string currentIndent = "";
	Stack<ContextFrame> contexts = new ();

	public string? LogFilePath => logFilePath;

	public StandardLogBuffer (LogLevel minimumConsoleLogLevel)
		: base (minimumConsoleLogLevel)
	{
		consoleStderrHasColor = !Console.IsErrorRedirected;
		consoleStdoutHasColor = !Console.IsOutputRedirected;
	}

	public void SetLogFile (string? filePath)
	{
		(string? newLogFilePath, StreamWriter? newLogFileWriter) = OpenLogFile (filePath);
		if (newLogFileWriter == null) {
			return;
		}

		CopyOldLog (newLogFileWriter, newLogFilePath);

		logFilePath = newLogFilePath;
		logFileWriter = newLogFileWriter;
		FlushPendingMessagesToFile ();
	}

	void CopyOldLog (StreamWriter? newLogFileWriter, string? newLogFilePath)
	{
		if (logFileWriter == null) {
			return;
		}

		bool canCopy = true;
		try {
			logFileWriter.Flush ();
			logFileWriter.Close ();
			logFileWriter.Dispose ();
		} catch (Exception ex) {
			Console.Error.WriteLine ($"Error closing old log file. {ex}");
			canCopy = false;
		}

		if (!canCopy || logFilePath == null || newLogFileWriter == null) {
			return;
		}

		try {
			string oldLog = File.ReadAllText (logFilePath);
			newLogFileWriter.Write (oldLog);
			newLogFileWriter.Flush ();
		} catch (Exception ex) {
			Console.Error.WriteLine ($"Failed to copy old log file '{logFilePath}' contents to new log file '{newLogFilePath ?? String.Empty}'");
			Console.Error.WriteLine (ex.ToString ());
		}

		try {
			File.Delete (logFilePath);
		} catch (Exception) {
			Console.Error.WriteLine ($"Failed to delete old log file '{logFilePath}'");
		}
	}

	(string? path, StreamWriter? writer) OpenLogFile (string? filePath)
	{
		string fullFilePath;
		if (String.IsNullOrEmpty (filePath)) {
			DateTime now = DateTime.Now;
			string logTag = $"{now.Year}-{now.Month}-{now.Day}_{now.Hour}-{now.Minute}-{now.Second}";
			string fileName = $"{Program.AppName}-{logTag}.log";

			// First we try in the current directory
			fullFilePath = Path.Combine (Environment.CurrentDirectory, fileName);
		} else {
			fullFilePath = filePath;
		}

		if (!TryOpenStream (fullFilePath, out Stream? stream) || stream == null) {
			LogFailureAlternate ();
			// Then in the temporary directory
			fullFilePath = Path.Combine (Path.GetTempPath (), Path.GetFileName (fullFilePath));
			if (!TryOpenStream (fullFilePath, out stream) || stream == null) {
				LogFailureWillUseConsole ();
				logFileOpenFailed = true;
				return (null, null);
			}
		}

		return (Path.GetFullPath (fullFilePath), new StreamWriter (stream, new UTF8Encoding (false)));

		void LogFailureAlternate () => LogFailure ("Trying alternate location.");
		void LogFailureWillUseConsole () => LogFailure ("All logging will be written to the console.");

		void LogFailure (string whatNext)
		{
			Write ($"Failed to open log file '{fullFilePath}' for writing. {whatNext}", LogLevel.Warning);
		}

		bool TryOpenStream (string path, out Stream? stream)
		{
			stream = null;
			try {
				stream = File.Open (fullFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
			} catch (IOException) {
				// Ignore
			} catch (UnauthorizedAccessException) {
				// Ignore
			}

			return stream != null;
		}
	}

	public override void PopContext ()
	{
		if (contexts.Count == 0) {
			currentIndent = "";
			return;
		}

		ContextFrame frame = contexts.Pop ();
		currentIndent = frame.SavedIndent ?? "";

		if (logFileWriter != null) {
			ReallyWriteToFile (
				$"----- Context end: {GetContextName (frame.Name)}",
				LogLevel.None,
				writeLine: true,
				doNotTag: true,
				indent: null
			);
		}
	}

	public override void PushContext (string? name)
	{
		contexts.Push (
			new ContextFrame {
				Name = name,
				SavedIndent = currentIndent,
			}
		);

		if (logFileWriter != null) {
			ReallyWriteToFile (
				$"----- Context start: {GetContextName (name)}",
				LogLevel.None,
				writeLine: true,
				doNotTag: true,
				indent: null
			);
		}

		currentIndent = $"{currentIndent}  ";
	}

	string GetContextName (string? name) => String.IsNullOrEmpty (name) ? "Unnamed" : name;

	public override void Write (string? message, LogLevel level, bool writeLine = true, ConsoleColor? colorOverride = null, bool doNotTag = false)
	{
		WriteToFileOrSave (message, level, writeLine, doNotTag, currentIndent);
		if (!logFileOpenFailed && level < MinimumConsoleLogLevel) {
			return;
		}

		bool useColor;
		TextWriter writer;
		if (level >= LogLevel.Warning) {
			useColor = consoleStderrHasColor;
			writer = Console.Error;
		} else {
			useColor = consoleStdoutHasColor;
			writer = Console.Out;
		}

		if (String.IsNullOrEmpty (message)) {
			if (writeLine) {
				writer.WriteLine ();
			}
			return;
		}

		Action<LogLevel, ConsoleColor?, Action> writeWrapper = useColor ? ColorWrapper : NoColorWrapper;
		writeWrapper (level, colorOverride, () => {
			if (level < LogLevel.Warning && !String.IsNullOrEmpty (currentIndent)) {
				writer.Write (currentIndent);
			}

			if (writeLine) {
				writer.WriteLine (message);
			} else {
				writer.Write (message);
			}
		});
	}

	void ColorWrapper (LogLevel level, ConsoleColor? colorOverride, Action doWrite)
	{
		ConsoleColor? oldFG = SaveColorFG ();
		try {
			if (colorOverride != null) {
				Console.ForegroundColor = colorOverride.Value;
			} else {
				Console.ForegroundColor = level switch {
					LogLevel.Debug   => DebugColor,
					LogLevel.Info    => InfoColor,
					LogLevel.Warning => WarningColor,
					LogLevel.Error   => ErrorColor,
					_                => StandardColor,
				};
			}
		} catch (Exception) {
			// ignore
		}

		try {
			doWrite ();
		} catch (Exception) {
			// ignore
		}
		RestoreColorFG ();

		void RestoreColorFG ()
		{
			if (oldFG == null) {
				return;
			}

			try {
				Console.ResetColor ();
				Console.ForegroundColor = oldFG.Value;
			} catch (Exception) {
				// ignore
			}
		}

		ConsoleColor? SaveColorFG ()
		{
			try {
				return Console.ForegroundColor;
			} catch (Exception) {
				// Ignore
				return null;
			}
		}
	}

	void NoColorWrapper (LogLevel level, ConsoleColor? colorOverride, Action doWrite) => doWrite ();

	void FlushPendingMessagesToFile ()
	{
		foreach (LogMessage message in pendingMessages) {
			WriteToFileOrSave (message.Message, message.Level, message.WriteLine, message.DoNotTag, message.Indent);
		}
		pendingMessages.Clear ();
	}

	void WriteToFileOrSave (string? message, LogLevel level, bool writeLine, bool doNotTag, string? indent)
	{
		if (logFileWriter != null) {
			ReallyWriteToFile (message, level, writeLine, doNotTag, indent);
			return;
		}

		pendingMessages.Add (
			new LogMessage {
				Message = message,
				Indent = indent,
				Level = level,
				WriteLine = writeLine,
				DoNotTag = doNotTag,
			}
		);
	}

	void ReallyWriteToFile (string? message, LogLevel level, bool writeLine, bool doNotTag, string? indent)
	{
		if (logFileWriter == null) {
			return;
		}

		if (String.IsNullOrEmpty (message)) {
			if (writeLine) {
				logFileWriter.WriteLine ();
			}
			return;
		}

		string formatted;
		if (doNotTag) {
			formatted = message;
		} else {
			string levelTag = level switch {
				LogLevel.Debug   => "D",
				LogLevel.Info    => "I",
				LogLevel.Warning => "W",
				LogLevel.Error   => "E",
				_                => "M",
			};

			formatted = $"<{levelTag}> {message}";
		}

		if (!String.IsNullOrEmpty (indent)) {
			logFileWriter.Write (indent);
		}

		if (writeLine) {
			logFileWriter.WriteLine (formatted);
		} else {
			logFileWriter.Write (formatted);
		}
	}

	protected virtual void Dispose (bool disposing)
	{
		if (disposed) {
			return;
		}

		if (disposing && logFileWriter != null) {
			try {
				logFileWriter.Flush ();
				logFileWriter.Close ();
				logFileWriter.Dispose ();
			} catch (Exception ex) {
				Console.Error.WriteLine ("Exception thrown while disposing of the log file writer.");
				Console.Error.WriteLine (ex.ToString ());
			}
			logFileWriter = null;
		}

		disposed = true;
	}

	public void Dispose ()
	{
		Dispose (disposing: true);
		GC.SuppressFinalize (this);
	}
}
