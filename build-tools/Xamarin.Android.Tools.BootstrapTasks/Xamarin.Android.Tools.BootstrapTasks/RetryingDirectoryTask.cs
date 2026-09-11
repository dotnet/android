using System;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	public abstract class RetryingDirectoryTask : Task
	{
		public int RetryCount { get; set; } = 10;

		public int RetryDelayMilliseconds { get; set; } = 200;

		protected bool ValidateRetryParameters ()
		{
			if (RetryCount < 0) {
				Log.LogError ($"{nameof (RetryCount)} must be greater than or equal to zero.");
				return false;
			}
			if (RetryDelayMilliseconds < 0) {
				Log.LogError ($"{nameof (RetryDelayMilliseconds)} must be greater than or equal to zero.");
				return false;
			}
			return true;
		}

		protected void MoveDirectoryWithRetry (string source, string destination)
		{
			for (int attempt = 0; ; attempt++) {
				try {
					MoveDirectory (source, destination);
					return;
				} catch (Exception e) when ((e is IOException || e is UnauthorizedAccessException) && attempt < RetryCount) {
					Log.LogMessage (
						MessageImportance.Normal,
						$"Could not move directory '{source}' to '{destination}' (attempt {attempt + 1} of {RetryCount + 1}): {e.Message} Retrying.");
					Delay (RetryDelayMilliseconds * (attempt + 1));
				}
			}
		}

		protected bool TryDeleteDirectoryWithRetry (string directory, out Exception error)
		{
			error = null;
			for (int attempt = 0; ; attempt++) {
				try {
					if (!Directory.Exists (directory))
						return true;
					if (Path.DirectorySeparatorChar == '\\')
						Files.SetDirectoryWriteable (directory);
					DeleteDirectory (directory);
					return true;
				} catch (DirectoryNotFoundException) {
					return true;
				} catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) {
					error = e;
					if (attempt >= RetryCount)
						return false;
					Log.LogMessage (
						MessageImportance.Normal,
						$"Could not remove directory '{directory}' (attempt {attempt + 1} of {RetryCount + 1}): {e.Message} Retrying.");
					Delay (RetryDelayMilliseconds * (attempt + 1));
				}
			}
		}

		protected static string GetRemainingEntries (string directory)
		{
			try {
				var entries = Directory.EnumerateFileSystemEntries (directory, "*", SearchOption.AllDirectories)
					.Take (10)
					.Select (path => $"'{path}'")
					.ToArray ();
				return entries.Length == 0 ? "" : $" Remaining entries: {string.Join (", ", entries)}.";
			} catch (Exception e) {
				return $" Remaining entries could not be enumerated: {e.Message}";
			}
		}

		protected static string NormalizeDirectoryPath (string directory)
		{
			return Path.TrimEndingDirectorySeparator (Path.GetFullPath (directory));
		}

		protected virtual void MoveDirectory (string source, string destination)
		{
			Directory.Move (source, destination);
		}

		protected virtual void DeleteDirectory (string directory)
		{
			Directory.Delete (directory, recursive: true);
		}

		protected virtual void Delay (int milliseconds)
		{
			Thread.Sleep (milliseconds);
		}
	}
}
