using System;

namespace Microsoft.Android.AppTools;

public interface ILogger
{
	LogLevel Level { get; set; }

	void Message (string? message);
	void MessageLine (string? message = null);
	void Warning (string? message);
	void WarningLine (string? message = null);
	void Error (string? message);
	void ErrorLine (string? message = null);
	void Info (string? message);
	void InfoLine (string? message = null);
	void Debug (string? message);
	void DebugLine (string? message = null);
	void Verbose (string? message);
	void VerboseLine (string? message = null);
	void Status (string label, string text);
	void StatusLine (string label, string text);
	void Log (LogLevel level, string? message);
	void LogLine (LogLevel level, string? message, ConsoleColor color);
	void Log (LogLevel level, string? message, ConsoleColor color);
	void ExceptionError (string desc, Exception ex);
}
