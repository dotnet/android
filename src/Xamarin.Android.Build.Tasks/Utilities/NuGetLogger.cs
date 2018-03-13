using System;
using NuGet.Common;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks {

	class NuGetLogger : ILogger {
		TaskLoggingHelper log;

		public NuGetLogger (TaskLoggingHelper log)
		{
			this.log = log;
		}

		public void Log (LogLevel level, string data)
		{
			log.LogMessage (data);
		}

		public void Log (ILogMessage message)
		{
			log.LogMessage (message.Message);
		}

		public System.Threading.Tasks.Task LogAsync (LogLevel level, string data)
		{
			return System.Threading.Tasks.Task.Run (() => Log (level, data));
		}

		public System.Threading.Tasks.Task LogAsync (ILogMessage message)
		{
			return System.Threading.Tasks.Task.Run (() => Log (message));
		}

		public void LogDebug (string data)
		{
			Log (LogLevel.Debug, data);
		}

		public void LogError (string data)
		{
			Log (LogLevel.Debug, data);
		}

		public void LogInformation (string data)
		{
			Log (LogLevel.Debug, data);
		}

		public void LogInformationSummary (string data)
		{
			Log (LogLevel.Debug, data);
		}

		public void LogMinimal (string data)
		{
			Log (LogLevel.Debug, data);
		}

		public void LogVerbose (string data)
		{
			Log (LogLevel.Debug, data);
		}

		public void LogWarning (string data)
		{
			Log (LogLevel.Debug, data);
		}
	}
}
