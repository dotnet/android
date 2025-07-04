using System;

namespace ApplicationUtility;

static class Log
{
	public const ConsoleColor ErrorColor       = ConsoleColor.Red;
	public const ConsoleColor WarningColor     = ConsoleColor.Yellow;
	public const ConsoleColor InfoColor        = ConsoleColor.Green;
	public const ConsoleColor DebugColor       = ConsoleColor.DarkGray;

	static bool showDebug = false;

	static void WriteStderr (string message)
	{
		Console.Error.Write (message);
	}

	static void WriteStderr (ConsoleColor color, string message)
	{
		ConsoleColor oldFG = Console.ForegroundColor;
		Console.ForegroundColor = color;
		WriteStderr (message);
		Console.ForegroundColor = oldFG;
	}

	static void WriteLineStderr (string message)
	{
		Console.Error.WriteLine (message);
	}

	static void WriteLineStderr (ConsoleColor color, string message)
	{
		ConsoleColor oldFG = Console.ForegroundColor;
		Console.ForegroundColor = color;
		WriteLineStderr (message);
		Console.ForegroundColor = oldFG;
	}

	static void Write (string message)
	{
		Console.Write (message);
	}

	static void Write (ConsoleColor color, string message)
	{
		ConsoleColor oldFG = Console.ForegroundColor;
		Console.ForegroundColor = color;
		Write (message);
		Console.ForegroundColor = oldFG;
	}

	static void WriteLine (string message)
	{
		Console.WriteLine (message);
	}

	static void WriteLine (ConsoleColor color, string message)
	{
		ConsoleColor oldFG = Console.ForegroundColor;
		Console.ForegroundColor = color;
		WriteLine (message);
		Console.ForegroundColor = oldFG;
	}

	public static void SetVerbose (bool verbose)
	{
		showDebug = verbose;
	}

	public static void Error (string message = "")
	{
		Error (tag: String.Empty, message);
	}

	public static void Error (string tag, string message)
	{
		if (message.Length > 0) {
			WriteStderr (ErrorColor, "[E] ");
		}

		if (tag.Length > 0) {
			WriteStderr (ErrorColor, $"{tag}: ");
		}

		WriteLineStderr (message);
	}

	public static void Warning (string message = "")
	{
		Warning (tag: String.Empty, message);
	}

	public static void Warning (string tag, string message)
	{
		if (message.Length > 0) {
			WriteStderr (WarningColor, "[W] ");
		}

		if (tag.Length > 0) {
			WriteStderr (WarningColor, $"{tag}: ");
		}

		WriteLineStderr (message);
	}

	public static void Info (string message = "")
	{
		Info (tag: String.Empty, message);
	}

	public static void Info (string tag, string message)
	{
		if (tag.Length > 0) {
			Write (InfoColor, $"{tag}: ");
		}

		WriteLine (InfoColor,message);
	}

	public static void Debug (string message = "")
	{
		Debug (tag: String.Empty, message);
	}

	public static void Debug (string tag, string message)
	{
		if (!showDebug) {
			return;
		}

		if (message.Length > 0) {
			Write (DebugColor, "[D] ");
		}

		if (tag.Length > 0) {
			Write (DebugColor, $"{tag}: ");
		}

		WriteLine (message);
	}

	public static void ExceptionError (string message, Exception ex)
	{
		Log.Error (message);
		Log.Error ("Exception was thrown:");
		Log.Error (ex.ToString ());
	}
}
