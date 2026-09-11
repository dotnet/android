using System;
using System.IO;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	public sealed class RemoveDirectoryBackups : RetryingDirectoryTask
	{
		[Required]
		public ITaskItem [] Directories { get; set; } = [];

		public override bool Execute ()
		{
			if (!ValidateRetryParameters ())
				return false;

			foreach (var item in Directories) {
				var directory = NormalizeDirectoryPath (item.ItemSpec);
				var parentDirectory = Path.GetDirectoryName (directory);
				if (string.IsNullOrEmpty (parentDirectory) || !Directory.Exists (parentDirectory))
					continue;

				var directoryName = Path.GetFileName (directory);
				foreach (var backupDirectory in Directory.GetDirectories (parentDirectory, $"{directoryName}.old-*", SearchOption.TopDirectoryOnly)) {
					if (!TryDeleteDirectoryWithRetry (backupDirectory, out var deleteError)) {
						Log.LogWarning (
							$"Could not remove old directory '{backupDirectory}' after {RetryCount + 1} attempts: " +
							$"{deleteError.Message}{GetRemainingEntries (backupDirectory)}");
					}
				}
			}

			return !Log.HasLoggedErrors;
		}
	}
}
