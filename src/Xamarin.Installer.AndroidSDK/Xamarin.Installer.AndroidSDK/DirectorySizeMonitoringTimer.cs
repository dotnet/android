using ICSharpCode.SharpZipLib.Zip;
using Mono.AndroidTools.Util;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.AndroidTools;
using Xamarin.Installer.Common;
using Timer = System.Timers.Timer;

namespace Xamarin.Installer.AndroidSDK
{
	public class DirectorySizeMonitoringTimer : IDisposable
	{
		const int MIN_MOVE_PROGRESS_SPLIT = 10, MAX_MOVE_PROGRESS_SPLIT = 50;
		const int SMALL_NUMBER_OF_FILES = 1, BIG_NUMBER_OF_FILES = 10000;

		Timer timer;
		float maxProgress, targetMaxProgress;
		Action<float> progressUpdateAction;
		bool disposed;

		public DirectorySizeMonitoringTimer (string targetDirectoryPath, string dirToCompareWith, Action<float> progressUpdateAction,
			float remapFrom = 0f, float remapTo = 100f, double checkInterval = 500d)
		{
			this.progressUpdateAction = progressUpdateAction;
			maxProgress = -1f;
			targetMaxProgress = remapTo;

			progressUpdateAction?.Invoke (remapFrom);

			if (!Platform.IsMac)
				return;

			ulong? totalExpectedSize = null;

			timer = new Timer { Interval = checkInterval, AutoReset = false };
			timer.Elapsed += async (_, __) => {
				if (disposed)
					return;

				if (!totalExpectedSize.HasValue)
					totalExpectedSize = await GetDirectorySizeAsync (dirToCompareWith);

				if (disposed)
					return;

				var size = await GetDirectorySizeAsync (targetDirectoryPath);
				if (size > totalExpectedSize.Value)
					size = totalExpectedSize.Value;

				if (disposed)
					return;

				float progress = (float) Remap (size * 100f / totalExpectedSize.Value, 0f, 100f, remapFrom, remapTo);
				if (progress > maxProgress) {
					// Logger.Debug ($"[DirectorySizeMonitoringTimer] size: {size} / totalExpectedSize: {totalExpectedSize} --> {progress}");
					progressUpdateAction.Invoke (progress);
					maxProgress = progress;
				}

				if (!disposed)
					timer.Start();
			};
			timer.Start ();
		}

		public void Dispose ()
		{
			disposed = true;
			//Logger.Debug ($"DirectorySizeMonitoringTimer disposing");
			timer?.Stop ();
			timer?.Dispose ();

			if (maxProgress < targetMaxProgress)
				progressUpdateAction?.Invoke (targetMaxProgress);

			progressUpdateAction = null;
		}

		async Task<ulong> GetDirectorySizeAsync (string directory)
		{
			// TODO: DirectoryInfo.GetFiles is throwing IO exceptions because of simultaneous access - find another way
			// maybe this: https://docs.microsoft.com/en-us/sysinternals/downloads/du - update: its EULA disallows their binaries distribution
			if (!Platform.IsMac)
				return 0;

			var result = 0uL;
			var duCommand = "/usr/bin/du";
			var args = new ProcessArgumentBuilder ();
			args.Add ($"-sk \"{directory}\"");
			string sizeOutput = null;
			try {
				sizeOutput = await ProcessUtils.ExecuteToolAsync(duCommand, args, output => output, CancellationToken.None);
			} catch (Exception ex) {
				if (ex?.Message?.Contains ("No such file or directory") == true) {
					Logger.Debug ($"[DirectorySizeMonitoringTimer] WARN! Dir \"{directory}\" was removed before \"/usr/bin/du\" could calculate its size");
				} else {
					Logger.Warning ($"[DirectorySizeMonitoringTimer] ProcessUtils.ExecuteToolAsync failed to execute {duCommand}.\n{ex}");
				}
			}

			if (!String.IsNullOrEmpty (sizeOutput)) {
				sizeOutput = sizeOutput.Replace (directory, string.Empty).Trim ();
				if (ulong.TryParse (sizeOutput, out ulong parsed)) {
					// "k" gives size in KBytes
					result = parsed * 1024;
				}
			}
			return result;
		}

		public static double Remap (double value, double from1, double to1, double from2, double to2)
		{
			var mapping = (value - from1) / (to1 - from1) * (to2 - from2) + from2;
			if (mapping < from2)
				mapping = from2;
			if (mapping > to2)
				mapping = to2;
			return mapping;
		}

		public static ulong CalculateTotalSize (ZipFile zipFile)
		{
			ulong totalSize = 0;
			foreach (ZipEntry entry in zipFile)
				totalSize += (ulong) entry.Size;
			Logger.Debug ($"[DirectorySizeMonitoringTimer] ZipFile totalSize: {totalSize}");

			return totalSize;
		}

		/// <summary>
		/// Move operation will take different time depending on number of files in an archive
		/// so we split the 100% installation progress according these values (unzip + move = 100%)
		/// </summary>
		/// <param name="archivePath">The path to the archive file.</param>
		/// <returns>Amount of percents for the Move part of the installation</returns>
		public static int CalculateMoveProgressSplit (string archivePath)
		{
			ZipFile zip = null;
			long filesCount;
			try {
				zip = new ZipFile (archivePath);
				filesCount = zip.Count;
			} finally {
				zip?.Close ();
			}

			return (int) Remap (filesCount, SMALL_NUMBER_OF_FILES, BIG_NUMBER_OF_FILES, MIN_MOVE_PROGRESS_SPLIT, MAX_MOVE_PROGRESS_SPLIT);
		}
	}
}
