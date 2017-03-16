using System;
using System.IO;

namespace Xamarin.Android.Tools.JavaDocToMdoc
{
	public static class Logger
	{
		const string LogCategoryName = "JavadocToMDoc";

		static TextWriter Output {
			get { return Application.ProcessingContext.LoggingOutput; }
		}

		public static LoggingVerbosity Verbosity { get; set; } = LoggingVerbosity.Warning;

		static bool ShouldLog (LoggingVerbosity toCheck)
		{
			return (int) toCheck <= (int) Verbosity;
		}

		static string LoggingVerbosityToLabel (LoggingVerbosity v)
		{
			switch (v) {
			case LoggingVerbosity.Error: return "error ";
			case LoggingVerbosity.Warning: return "warning ";
			case LoggingVerbosity.Debug: return string.Empty;
			default: throw new NotSupportedException ();
			}
		}

		internal static void Log (LoggingVerbosity verbosity, int errorCode, string message)
		{
			if (!ShouldLog (verbosity))
				return;
			switch (verbosity) {
			case LoggingVerbosity.Error:
			case LoggingVerbosity.Warning:
				Output.Write (LoggingVerbosityToLabel (verbosity));
				break;
			}
			Output.Write (LogCategoryName);
			switch (verbosity) {
			case LoggingVerbosity.Error:
			case LoggingVerbosity.Warning:
				Output.Write (errorCode.ToString ("D04"));
				break;
			}
			Output.Write (" : ");
			Output.WriteLine (message);
		}

		internal static void Log (LoggingVerbosity verbosity, int errorCode, string format, params object [] args)
		{
			if (!ShouldLog (verbosity))
				return;
			switch (verbosity) {
			case LoggingVerbosity.Error:
			case LoggingVerbosity.Warning:
				Output.Write (LoggingVerbosityToLabel (verbosity));
				break;
			}
			Output.Write (LogCategoryName);
			switch (verbosity) {
			case LoggingVerbosity.Error:
			case LoggingVerbosity.Warning:
				Output.Write (errorCode.ToString ("D04"));
				break;
			}
			Output.Write (" : ");
			Output.WriteLine (format, args);
		}
	}

	public enum LoggingVerbosity
	{
		Error,
		Warning,
		Debug,
	}
}
