using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	public class ReplaceDirectory : RetryingDirectoryTask
	{
		[Required]
		public string SourceDirectory { get; set; } = "";

		[Required]
		public string DestinationDirectory { get; set; } = "";

		[Required]
		public string RequiredFile { get; set; } = "";

		public override bool Execute ()
		{
			if (!ValidateRetryParameters ())
				return false;

			var sourceDirectory = NormalizeDirectoryPath (SourceDirectory);
			var destinationDirectory = NormalizeDirectoryPath (DestinationDirectory);
			if (!Directory.Exists (sourceDirectory)) {
				Log.LogError ($"Source directory '{sourceDirectory}' does not exist.");
				return false;
			}
			if (!File.Exists (Path.Combine (sourceDirectory, RequiredFile))) {
				Log.LogError ($"Source directory '{sourceDirectory}' does not contain required file '{RequiredFile}'.");
				return false;
			}

			var parentDirectory = Path.GetDirectoryName (destinationDirectory);
			if (!string.IsNullOrEmpty (parentDirectory))
				Directory.CreateDirectory (parentDirectory);

			string backupDirectory = null;
			try {
				if (Directory.Exists (destinationDirectory)) {
					var candidate = destinationDirectory + $".old-{Guid.NewGuid ():N}";
					MoveDirectoryWithRetry (destinationDirectory, candidate);
					backupDirectory = candidate;
				}

				try {
					MoveDirectoryWithRetry (sourceDirectory, destinationDirectory);
				} catch {
					RestoreBackup (backupDirectory, destinationDirectory);
					throw;
				}
			} catch (Exception e) {
				Log.LogError ($"Failed to replace directory '{destinationDirectory}' with '{sourceDirectory}': {e.Message}");
				return false;
			}

			if (backupDirectory != null && !TryDeleteDirectoryWithRetry (backupDirectory, out var deleteError)) {
				Log.LogWarning (
					$"Installed '{destinationDirectory}', but could not remove old directory '{backupDirectory}' after {RetryCount + 1} attempts: " +
					$"{deleteError.Message}{GetRemainingEntries (backupDirectory)}");
			}

			return !Log.HasLoggedErrors;
		}

		void RestoreBackup (string backupDirectory, string destinationDirectory)
		{
			if (backupDirectory == null || !Directory.Exists (backupDirectory))
				return;
			if (Directory.Exists (destinationDirectory)) {
				Log.LogError ($"Cannot restore previous directory from '{backupDirectory}' because '{destinationDirectory}' exists.");
				return;
			}
			try {
				MoveDirectoryWithRetry (backupDirectory, destinationDirectory);
				Log.LogMessage (MessageImportance.Normal, $"Restored previous directory from '{backupDirectory}'.");
			} catch (Exception e) {
				Log.LogError ($"Failed to restore previous directory from '{backupDirectory}': {e.Message}");
			}
		}

	}
}
