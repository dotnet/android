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
			Error (tag: String.Empty, message);
		}

		public static void Error (string tag, string message)
		{
			if (message.Length > 0) {
				Console.Error.Write ("[Error] ");
			}

			if (tag.Length > 0) {
				Console.Error.Write ($"{tag}: ");
			}

			Console.Error.WriteLine (message);
		}

		public static void Warning (string message = "")
		{
			Warning (tag: String.Empty, message);
		}

		public static void Warning (string tag, string message)
		{
			if (message.Length > 0) {
				Console.Error.Write ("[Warning] ");
			}

			if (tag.Length > 0) {
				Console.Error.Write ($"{tag}: ");
			}

			Console.Error.WriteLine (message);
		}

		public static void Info (string message = "")
		{
			Info (tag: String.Empty, message);
		}

		public static void Info (string tag, string message)
		{
			if (tag.Length > 0) {
				Console.Write ($"{tag}: ");
			}

			Console.WriteLine (message);
		}

		public static void Debug (string message)
		{
			Debug (tag: String.Empty, message);
		}

		public static void Debug (string tag, string message)
		{
			if (!showDebug) {
				return;
			}

			if (message.Length > 0) {
				Console.Write ("[Debug] ");
			}

			if (tag.Length > 0) {
				Console.Write ($"{tag}: ");
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
