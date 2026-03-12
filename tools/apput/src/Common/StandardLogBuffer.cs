using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ApplicationUtility;

class StandardLogBuffer : LogBuffer, IDisposable
{
	const ConsoleColor ErrorColor    = ConsoleColor.Red;
	const ConsoleColor WarningColor  = ConsoleColor.Yellow;
	const ConsoleColor InfoColor     = ConsoleColor.Green;
	const ConsoleColor DebugColor    = ConsoleColor.Gray;
	const ConsoleColor StandardColor = ConsoleColor.White;

	bool disposed;

	sealed class LogMessage
	{
		public string? Message;
		public LogLevel Level;
		public bool WriteLine;
		public bool DoNotTag;
	}

	readonly List<LogMessage> pendingMessages = new ();
	readonly bool consoleStderrHasColor;
	readonly bool consoleStdoutHasColor;

	StreamWriter? logFileWriter;
	string? logFilePath;

	public string? LogFilePath => logFilePath;

	public StandardLogBuffer (LogLevel minimumConsoleLogLevel)
		: base (minimumConsoleLogLevel)
	{
		consoleStderrHasColor = !Console.IsErrorRedirected;
		consoleStdoutHasColor = !Console.IsOutputRedirected;
	}

	public void SetLogFile (string? filePath)
	{
		// TODO: if we already have a log file and someone calls us again, move the old log into the new one
		if (String.IsNullOrWhiteSpace (filePath)) {
			DateTime now = DateTime.Now;
			string logTag = $"{now.Year}-{now.Month}-{now.Day}_{now.Hour}-{now.Minute}-{now.Second}";
			logFilePath = Path.Combine (Path.GetTempPath (), $"{Program.AppName}-{logTag}.log");
		} else {
			logFilePath = filePath;
		}

		var stream = File.Open (logFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
		logFileWriter = new StreamWriter (stream, Encoding.UTF8);
		FlushPendingMessagesToFile ();
	}

	public override void PopContext ()
	{
		throw new System.NotImplementedException ();
	}

	public override void PushContext (string? name)
	{
		throw new System.NotImplementedException ();
	}

	public override void Write (string? message, LogLevel level, bool writeLine = true, ConsoleColor? colorOverride = null, bool doNotTag = false)
	{
		WriteToFile (message, level, writeLine, doNotTag);
		if (level < MinimumConsoleLogLevel) {
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
			WriteToFile (message.Message, message.Level, message.WriteLine, message.DoNotTag);
		}
		pendingMessages.Clear ();
	}

	void WriteToFile (string? message, LogLevel level, bool writeLine, bool doNotTag)
	{
		if (logFileWriter == null) {
			pendingMessages.Add (
				new LogMessage {
					Message = message,
					Level = level,
					WriteLine = writeLine,
					DoNotTag = doNotTag,
				}
			);
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
