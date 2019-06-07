using System;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	abstract class StepWithDownloadProgress : Step
	{
		protected StepWithDownloadProgress (string description)
			: base (description)
		{}

		protected async Task Download (Context context, Uri url, string destinationFilePath, string descriptiveName, string fileName, DownloadStatus downloadStatus)
		{
			bool fancyLogging = context.InteractiveSession;
			Log.DebugLine ($"{descriptiveName} URL: {url}");
			if (!context.InteractiveSession)
				LogStatus ($"downloading {fileName} ", 4, ConsoleColor.Gray);

			bool success;
			Exception downloadEx = null;
			try {
				Log.DebugLine ("About to start downloading");
				success = await Utilities.Download (url, destinationFilePath, downloadStatus);
			} catch (Exception ex) {
				Log.DebugLine ($"Caught exception: {ex}");
				downloadEx = ex;
				success = false;
			}

			Log.Debug ($"success == {success}");
			if (success)
				return;

			string message = $"Failed to download {url}";

			if (downloadEx != null)
				throw new InvalidOperationException ($"{message}: {downloadEx.Message}", downloadEx);
			throw new InvalidOperationException (message);
		}

		protected void LogStatus (string status, int padLeft, ConsoleColor color, bool logLine = true)
		{
			string message = PadStatus (status, padLeft);
			if (logLine)
				Log.StatusLine (message, color);
			else
				Log.Status (message, color);
		}

		protected string PadStatus (string status, int padLeft)
		{
			return status.PadLeft (status.Length + padLeft);
		}
	}
}
