using System;

namespace Xamarin.Android.Tools
{
	public delegate void MessageHandler (string task, string message);
	public delegate void TaskLogHandler (AndroidTaskLog log);

	public static class AndroidLogger
	{
		public static event MessageHandler Info;
		public static event MessageHandler Warning;
		public static event MessageHandler Error;
		public static event MessageHandler Debug;
		public static event TaskLogHandler Task;

		public static void LogInfo (string format, params object[] args)
		{
			LogInfo (string.Empty, format, args);
		}

		public static void LogInfo (string task, string format, params object[] args)
		{
			if (Info != null) {
				if (args == null || args.Length == 0)
					Info (task, format);
				else
					Info (task, String.Format (format, args));
			} else
				throw new InvalidOperationException ("Internal Error: should initialize Info");
		}

		public static void LogWarning (string format, params object[] args)
		{
			LogWarning (string.Empty, format, args);
		}

		public static void LogWarning (string task, string format, params object[] args)
		{
			if (Warning != null) {
				if (args == null)
					Warning (task, format);
				else
					Warning (task, String.Format (format, args));
			} else
				throw new InvalidOperationException ("Internal Error: should initialize Warning");
		}

		public static void LogError (string format, params object[] args)
		{
			LogError (string.Empty, format, args);
		}

		public static void LogError (string message, Exception ex)
		{
			message += (ex != null? System.Environment.NewLine + ex.ToString () : string.Empty);
			LogError (message);
		}

		public static void LogError (string task, string format, params object[] args)
		{
			if (Error != null) {
				if (args == null || args.Length == 0)
					Error (task, format);
				else
					Error (task, String.Format (format, args));
			} else
				throw new InvalidOperationException ("Internal Error: should initialize Error");
		}

		public static void LogDebug (string format, params object[] args)
		{
			LogDebug (string.Empty, format, args);
		}

		public static void LogDebug (string task, string format, params object[] args)
		{
			if (Debug != null) {
				if (args == null || args.Length == 0)
					Debug (task, format);
				else
					Debug (task, String.Format (format, args));
			} else
				throw new InvalidOperationException ("Internal Error: should initialize Debug");
		}

		public static void LogTask (AndroidTaskLog log)
		{
			if (Task != null)
				Task (log);
			else
				throw new InvalidOperationException ("Internal Error: should initialize Task");
		}
	}

	public class AndroidTaskLog
	{
		public string Task { get; private set; }
		public string Input { get; private set; }
		public string Output { get; private set; }
		public DateTime StartTime { get; private set; }
		public DateTime EndTime { get; private set; }

		public AndroidTaskLog (string task, string input)
		{
			Task = task;
			Input = input;
			StartTime = DateTime.Now;
		}

		public AndroidTaskLog Complete (string output)
		{
			Output = output;
			EndTime = DateTime.Now;

			return this;
		}

		public AndroidTaskLog Complete (object output)
		{
			if (output == null)
				output = "";
			Output = output.ToString();
			EndTime = DateTime.Now;

			return this;
		}
	}
}

