using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	static class UnhandledExceptionLogger
	{
		public static void LogUnhandledException (this TaskLoggingHelper log, string prefix, Exception ex)
		{
			prefix = "XA" + prefix;

			// Some ordering is necessary here to ensure exceptions are before their base exceptions
			if (ex is NullReferenceException)
				log.LogCodedError (prefix + "7001", ex.ToString ());
			else if (ex is ArgumentOutOfRangeException)	// ArgumentException
				log.LogCodedError (prefix + "7002", ex.ToString ());
			else if (ex is ArgumentNullException)           // ArgumentException
				log.LogCodedError (prefix + "7003", ex.ToString ());
			else if (ex is ArgumentException)
				log.LogCodedError (prefix + "7004", ex.ToString ());
			else if (ex is FormatException)
				log.LogCodedError (prefix + "7005", ex.ToString ());
			else if (ex is IndexOutOfRangeException)
				log.LogCodedError (prefix + "7006", ex.ToString ());
			else if (ex is InvalidCastException)
				log.LogCodedError (prefix + "7007", ex.ToString ());
			else if (ex is ObjectDisposedException)		// InvalidOperationException
				log.LogCodedError (prefix + "7008", ex.ToString ());
			else if (ex is InvalidOperationException)
				log.LogCodedError (prefix + "7009", ex.ToString ());
			else if (ex is InvalidProgramException)
				log.LogCodedError (prefix + "7010", ex.ToString ());
			else if (ex is KeyNotFoundException)
				log.LogCodedError (prefix + "7011", ex.ToString ());
			else if (ex is TaskCanceledException)           // OperationCanceledException
				log.LogCodedError (prefix + "7012", ex.ToString ());
			else if (ex is OperationCanceledException)
				log.LogCodedError (prefix + "7013", ex.ToString ());
			else if (ex is OutOfMemoryException)
				log.LogCodedError (prefix + "7014", ex.ToString ());
			else if (ex is NotSupportedException)
				log.LogCodedError (prefix + "7015", ex.ToString ());
			else if (ex is StackOverflowException)
				log.LogCodedError (prefix + "7016", ex.ToString ());
			else if (ex is TimeoutException)
				log.LogCodedError (prefix + "7017", ex.ToString ());
			else if (ex is TypeInitializationException)
				log.LogCodedError (prefix + "7018", ex.ToString ());
			else if (ex is UnauthorizedAccessException)
				log.LogCodedError (prefix + "7019", ex.ToString ());
			else if (ex is ApplicationException)
				log.LogCodedError (prefix + "7020", ex.ToString ());
			else if (ex is KeyNotFoundException)
				log.LogCodedError (prefix + "7021", ex.ToString ());
			else if (ex is PathTooLongException)            // IOException
				log.LogCodedError (prefix + "7022", ex.ToString ());
			else if (ex is DirectoryNotFoundException)      // IOException
				log.LogCodedError (prefix + "7023", ex.ToString ());
			else if (ex is IOException) 
				log.LogCodedError (prefix + "7024", ex.ToString ());

			log.LogCodedError (prefix + "7000", ex.ToString ());
		}
	}
}
