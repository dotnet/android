// https://github.com/xamarin/xamarin-android/blob/9fca138604c53989e1cff7fc0c2e939583b4da28/src/Xamarin.Android.Build.Tasks/Utilities/UnhandledExceptionLogger.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Build.Utilities;

namespace Microsoft.Android.Build.Tasks
{
	public static class UnhandledExceptionLogger
	{
		public static void LogUnhandledException (this AsyncTask task, string prefix, Exception ex)
		{
			LogUnhandledException ((code, message) => task.LogCodedError (code, message), prefix, ex);
		}

		public static void LogUnhandledException (this TaskLoggingHelper log, string prefix, Exception ex)
		{
			LogUnhandledException ((code, message) => log.LogCodedError (code, message), prefix, ex);
		}

		static void LogUnhandledException (Action<string,string> logCodedError, string prefix, Exception ex)
		{
			prefix = "XA" + prefix;

			// Some ordering is necessary here to ensure exceptions are before their base exceptions
			if (ex is NullReferenceException)
				logCodedError (prefix + "7001", ex.ToString ());
			else if (ex is ArgumentOutOfRangeException)     // ArgumentException
				logCodedError (prefix + "7002", ex.ToString ());
			else if (ex is ArgumentNullException)           // ArgumentException
				logCodedError (prefix + "7003", ex.ToString ());
			else if (ex is ArgumentException)
				logCodedError (prefix + "7004", ex.ToString ());
			else if (ex is FormatException)
				logCodedError (prefix + "7005", ex.ToString ());
			else if (ex is IndexOutOfRangeException)
				logCodedError (prefix + "7006", ex.ToString ());
			else if (ex is InvalidCastException)
				logCodedError (prefix + "7007", ex.ToString ());
			else if (ex is ObjectDisposedException)		// InvalidOperationException
				logCodedError (prefix + "7008", ex.ToString ());
			else if (ex is InvalidOperationException)
				logCodedError (prefix + "7009", ex.ToString ());
			else if (ex is InvalidProgramException)
				logCodedError (prefix + "7010", ex.ToString ());
			else if (ex is KeyNotFoundException)
				logCodedError (prefix + "7011", ex.ToString ());
			else if (ex is TaskCanceledException)           // OperationCanceledException
				logCodedError (prefix + "7012", ex.ToString ());
			else if (ex is OperationCanceledException)
				logCodedError (prefix + "7013", ex.ToString ());
			else if (ex is OutOfMemoryException)
				logCodedError (prefix + "7014", ex.ToString ());
			else if (ex is NotSupportedException)
				logCodedError (prefix + "7015", ex.ToString ());
			else if (ex is StackOverflowException)
				logCodedError (prefix + "7016", ex.ToString ());
			else if (ex is TimeoutException)
				logCodedError (prefix + "7017", ex.ToString ());
			else if (ex is TypeInitializationException)
				logCodedError (prefix + "7018", ex.ToString ());
			else if (ex is UnauthorizedAccessException uaex)
				logCodedError (prefix + "7019", GetFileLockedExceptionMessage (uaex));
			else if (ex is ApplicationException)
				logCodedError (prefix + "7020", ex.ToString ());
			else if (ex is KeyNotFoundException)
				logCodedError (prefix + "7021", ex.ToString ());
			else if (ex is PathTooLongException)            // IOException
				logCodedError (prefix + "7022", ex.ToString ());
			else if (ex is DirectoryNotFoundException)      // IOException
				logCodedError (prefix + "7023", ex.ToString ());
			else if (ex is DriveNotFoundException)		// IOException
				logCodedError (prefix + "7025", ex.ToString ());
			else if (ex is EndOfStreamException)		// IOException
				logCodedError (prefix + "7026", ex.ToString ());
			else if (ex is FileLoadException)		// IOException
				logCodedError (prefix + "7027", ex.ToString ());
			else if (ex is FileNotFoundException)		// IOException
				logCodedError (prefix + "7028", ex.ToString ());
			else if (ex is IOException ioex)
				logCodedError (prefix + "7024", GetFileLockedExceptionMessage (ioex));
			else
				logCodedError (prefix + "7000", ex.ToString ());
		}

		static string GetFileLockedExceptionMessage (Exception ex)
		{
			// If we find a file path in the message, and the file exists, check if it's locked
			// en-US message is:
			// The process cannot access the file 'D:\temp\tmpw5mhqp.tmp' because it is being used by another process.
			var matches = Regex.Matches (ex.Message, @"'([^']+)'");
			for (int i = 0; i < matches.Count; ++i) {
				string path = matches [i].Groups [1].Value;
				if (!File.Exists (path)) {
					continue;
				}
				string processes = LockCheck.GetLockedFileMessage (path);
				if (string.IsNullOrEmpty (processes)) {
					continue;
				}
				return $"{processes}.{Environment.NewLine}{ex.ToString ()}";
			}
			return ex.ToString ();
		}

		public static void LogUnhandledToolError (this TaskLoggingHelper log, string prefix, string toolOutput)
		{
			log.LogCodedError ($"XA{prefix}0000", toolOutput);
		}
	}
}
