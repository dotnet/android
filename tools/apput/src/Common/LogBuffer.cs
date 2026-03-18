using System;

namespace ApplicationUtility;

/// <summary>
/// Abstract base for log message buffers. Implementations handle writing messages
/// to the console, files, or other destinations, with context-based indentation.
/// </summary>
public abstract class LogBuffer
{
	public LogLevel MinimumConsoleLogLevel { get; set; }

	protected LogBuffer (LogLevel minimumConsoleLogLevel)
	{
		MinimumConsoleLogLevel = minimumConsoleLogLevel;
	}

	/// <summary>
	/// Writes a log message at the specified level.
	/// </summary>
	/// <param name="message">The message text, or <c>null</c> for a blank line.</param>
	/// <param name="level">The log level for this message.</param>
	/// <param name="writeLine">If <c>true</c>, append a newline after the message.</param>
	/// <param name="colorOverride">Optional console color override.</param>
	/// <param name="doNotTag">If <c>true</c>, omit the level tag prefix when writing to a file.</param>
	public abstract void Write (string? message, LogLevel level, bool writeLine = true, ConsoleColor? colorOverride = null, bool doNotTag = false);

	/// <summary>
	/// Pushes a named logging context onto the stack, increasing indentation.
	/// </summary>
	/// <param name="name">Optional context name.</param>
	public abstract void PushContext (string? name);

	/// <summary>
	/// Pops the most recent logging context from the stack, restoring prior indentation.
	/// </summary>
	public abstract void PopContext ();
}
