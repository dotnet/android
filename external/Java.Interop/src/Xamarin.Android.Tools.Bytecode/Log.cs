using System;
using System.Diagnostics;

namespace Xamarin.Android.Tools.Bytecode
{
	public class Log
	{
		public static Action<TraceLevel, int, string, object[]>? OnLog;

		public static void Warning (int verbosity, string format, params object[] args)
		{
			var log = OnLog;
			if (log == null)
				return;
			log (TraceLevel.Warning, verbosity, format, args);
		}

		public static void Warning (int verbosity, string message) => Warning (verbosity, "{0}", message);

		public static void Error (string format, params object[] args)
		{
			var log = OnLog;
			if (log == null)
				return;
			log (TraceLevel.Error, 0, format, args);
		}

		public static void Error (string message) => Error ("{0}", message);

		public static void Message (string format, params object[] args)
		{
			var log = OnLog;
			if (log == null)
				return;
			log (TraceLevel.Info, 0, format, args);
		}

		public static void Message (string message) => Message ("{0}", message);

		public static void Debug (string format, params object[] args)
		{
			var log = OnLog;
			if (log == null)
				return;
			log (TraceLevel.Verbose, 0, format, args);
		}

		public static void Debug (string message) => Debug ("{0}", message);
	}
}

