using System;

namespace ApplicationUtility;

public static class Log
{
	static LogBuffer? buffer;

	static LogBuffer Buffer => buffer ?? throw new InvalidOperationException ("Log buffer implementation not set.");

	public static void SetLogBuffer (LogBuffer bufferImpl)
	{
		buffer = bufferImpl;
	}

	public static void StartContext (string? name = null)
	{
		Buffer.PushContext (name);
	}

	public static void EndContext ()
	{
		Buffer.PopContext ();
	}

	/// <summary>
	/// Writes exception, if `ex` isn't `null`. The exception type and message are
	/// logged using `messageLogLevel`. Stack trace is written using the `Debug` level
	/// unless `messageLogLevel` is `Error` or higher, in which case the same level is
	/// used for the stack trace.
	/// </summary>
	static void WriteException (Exception? ex, LogLevel messageLogLevel)
	{
		if (ex == null) {
			return;
		}

		Buffer.Write (
			$"Exception '{ex.GetType ()}' was thrown: {ex.Message}",
			messageLogLevel,
			writeLine: true
		);

		Buffer.Write (
			"See the log file for full exception trace.",
			messageLogLevel,
			writeLine: true
		);

		Buffer.Write (
			ex.ToString (),
			messageLogLevel >= LogLevel.Error ? messageLogLevel : LogLevel.Debug,
			writeLine: true
		);
	}

	public static void Error (string message = "", Exception? ex = null, bool writeLine = true)
	{
		Buffer.Write (message, LogLevel.Error, writeLine);
		WriteException (ex, LogLevel.Error);
	}

	public static void Warning (string message = "", Exception? ex = null, bool writeLine = true)
	{
		Buffer.Write (message, LogLevel.Warning, writeLine);
		WriteException (ex, LogLevel.Warning);
	}

	public static void Info (string message = "", Exception? ex = null, bool writeLine = true)
	{
		Buffer.Write (message, LogLevel.Info, writeLine);
		WriteException (ex, LogLevel.Info);
	}

	public static void Debug (string message = "", Exception? ex = null, bool writeLine = true)
	{
		Buffer.Write (message, LogLevel.Debug, writeLine);
		WriteException (ex, LogLevel.Debug);
	}

	public static void LabeledInfo (string label, string message)
	{
		Buffer.Write ($"{label}: ", LogLevel.Info, writeLine: false, colorOverride: ConsoleColor.White);
		Buffer.Write (message, LogLevel.Info, colorOverride: ConsoleColor.Cyan, doNotTag: true);
	}
}
