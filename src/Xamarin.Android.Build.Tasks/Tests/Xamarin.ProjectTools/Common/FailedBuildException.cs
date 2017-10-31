using System;

namespace Xamarin.ProjectTools
{
	public class FailedBuildException : Exception
	{
		public FailedBuildException ()
		{
		}

		public FailedBuildException (string message)
			: base (message)
		{
		}

		public FailedBuildException (string message, Exception innerException)
			: base (message, innerException)
		{
		}

		public FailedBuildException (string message, Exception innerException, string buildLog)
			: this (message, innerException)
		{
			BuildLog = buildLog;
		}

		public string BuildLog { get; set; }

		public override string StackTrace {
			get {
				return $"{base.StackTrace}\nBuildLog: {BuildLog}";
			}
		}
	}
}

