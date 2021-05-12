using System;

namespace tmt
{
	static class Log
	{
		static bool showDebug = false;

		public static void SetVerbose (bool verbose)
		{
			showDebug = verbose;
		}

		public static void Error (string message = "")
		{
			if (message.Length > 0) {
				Console.Error.Write ("Error: ");
			}
			Console.Error.WriteLine (message);
		}

		public static void Warning (string message = "")
		{
			if (message.Length > 0) {
				Console.Error.Write ("Warning: ");
			}

			Console.Error.WriteLine (message);
		}

		public static void Info (string message = "")
		{
			Console.WriteLine (message);
		}

		public static void Debug (string message = "")
		{
			if (!showDebug) {
				return;
			}

			Console.WriteLine (message);
		}

		public static void ExceptionError (string message, Exception ex)
		{
			Log.Error (message);
			Log.Error ("Exception was thrown:");
			Log.Error (ex.ToString ());
		}
	}
}
