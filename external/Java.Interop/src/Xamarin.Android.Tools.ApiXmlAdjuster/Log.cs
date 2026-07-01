using System;
using System.IO;

namespace Xamarin.Android.Tools.ApiXmlAdjuster
{
	public static class Log
	{
		public enum LoggingLevel
		{
			None = 0,
			Error = 1,
			Warning = 2,
			Debug = 3,
		}

		static Action<string> write_default = s => (DefaultWriter ?? Console.Out).WriteLine (s);

		static Action<string>? e, w, d;

		public  static  TextWriter?     DefaultWriter   { get; set; }

		public  static  LoggingLevel    Verbosity       { get; set; } = LoggingLevel.Error;

		public static Action<string> LogErrorAction {
			get { return e ?? write_default; }
			set { e = value; }
		}
		public static Action<string> LogWarningAction {
			get { return w ?? write_default; }
			set { w = value; }
		}
		public static Action<string> LogDebugAction {
			get { return d ?? write_default; }
			set { d = value; }
		}

		public static void LogError (string format, params object?[] args)
		{
			if ((int) Verbosity >= (int) LoggingLevel.Error)
				LogErrorAction (args.Length > 0 ? string.Format (format, args) : format);
		}

		public static void LogWarning (string format, params object?[] args)
		{
			if ((int)Verbosity >= (int)LoggingLevel.Warning)
				LogWarningAction (args.Length > 0 ? string.Format (format, args) : format);
		}

		public static void LogDebug (string format, params object?[] args)
		{
			if ((int)Verbosity >= (int)LoggingLevel.Debug)
				LogDebugAction (args.Length > 0 ? string.Format (format, args) : format);
		}
	}
}
