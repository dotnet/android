using System;

namespace ApplicationUtility;

public abstract class LogBuffer
{
	public LogLevel MinimumConsoleLogLevel { get; set; }

	protected LogBuffer (LogLevel minimumConsoleLogLevel)
	{
		MinimumConsoleLogLevel = minimumConsoleLogLevel;
	}

	public abstract void Write (string? message, LogLevel level, bool writeLine = true, ConsoleColor? colorOverride = null, bool doNotTag = false);
	public abstract void PushContext (string? name);
	public abstract void PopContext ();
}
