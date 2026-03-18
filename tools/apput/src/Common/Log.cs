using System;

namespace ApplicationUtility;

/// <summary>
/// Static logging facade that delegates to a configured <see cref="LogBuffer"/> implementation.
/// Provides leveled logging (Error, Warning, Info, Debug) and context nesting.
/// </summary>
public static class Log
{
	static LogBuffer? buffer;

	static LogBuffer Buffer => buffer ?? throw new InvalidOperationException ("Log buffer implementation not set.");

	/// <summary>
	/// Sets the <see cref="LogBuffer"/> implementation to use for all logging.
	/// </summary>
	/// <param name="bufferImpl">The log buffer implementation.</param>
	public static void SetLogBuffer (LogBuffer bufferImpl)
	{
		buffer = bufferImpl;
	}

	/// <summary>
	/// Pushes a new named logging context. Messages logged within this context
	/// will be indented relative to the parent.
	/// </summary>
	/// <param name="name">Optional context name for labeling in the log file.</param>
	public static void StartContext (string? name = null)
	{
		Buffer.PushContext (name);
	}

	/// <summary>
	/// Pops the most recent logging context.
	/// </summary>
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

	/// <summary>
	/// Writes an error-level message and optional exception.
	/// </summary>
	public static void Error (string message = "", Exception? ex = null, bool writeLine = true)
	{
		Buffer.Write (message, LogLevel.Error, writeLine);
		WriteException (ex, LogLevel.Error);
	}

	/// <summary>
	/// Writes a warning-level message and optional exception.
	/// </summary>
	public static void Warning (string message = "", Exception? ex = null, bool writeLine = true)
	{
		Buffer.Write (message, LogLevel.Warning, writeLine);
		WriteException (ex, LogLevel.Warning);
	}

	/// <summary>
	/// Writes an info-level message and optional exception.
	/// </summary>
	public static void Info (string message = "", Exception? ex = null, bool writeLine = true)
	{
		Buffer.Write (message, LogLevel.Info, writeLine);
		WriteException (ex, LogLevel.Info);
	}

	/// <summary>
	/// Writes a debug-level message and optional exception.
	/// </summary>
	public static void Debug (string message = "", Exception? ex = null, bool writeLine = true)
	{
		Buffer.Write (message, LogLevel.Debug, writeLine);
		WriteException (ex, LogLevel.Debug);
	}

	/// <summary>
	/// Writes a labeled info message with the label and value rendered in distinct colors.
	/// </summary>
	/// <param name="label">The label text (rendered in white).</param>
	/// <param name="message">The value text (rendered in cyan).</param>
	public static void LabeledInfo (string label, string message)
	{
		Buffer.Write ($"{label}: ", LogLevel.Info, writeLine: false, colorOverride: ConsoleColor.White);
		Buffer.Write (message, LogLevel.Info, colorOverride: ConsoleColor.Cyan, doNotTag: true);
	}
}
