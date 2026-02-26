using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Xamarin.Android.Tools
{
	class FileUtil
	{
		public static string GetTempFilenameForWrite (string fileName)
		{
			return Path.GetDirectoryName (fileName) + Path.DirectorySeparatorChar + ".#" + Path.GetFileName (fileName);
		}

		//From MonoDevelop.Core.FileService
		public static void SystemRename (string sourceFile, string destFile)
		{
			//FIXME: use the atomic System.IO.File.Replace on NTFS
			if (OS.IsWindows) {
				string? wtmp = null;
				if (File.Exists (destFile)) {
					do {
						wtmp = Path.Combine (Path.GetTempPath (), Guid.NewGuid ().ToString ());
					} while (File.Exists (wtmp));

					File.Move (destFile, wtmp);
				}
				try {
					File.Move (sourceFile, destFile);
				}
				catch {
					try {
						if (wtmp != null)
							File.Move (wtmp, destFile);
					}
					catch {
						wtmp = null;
					}
					throw;
				}
				finally {
					if (wtmp != null) {
						try {
							File.Delete (wtmp);
						}
						catch { }
					}
				}
			}
			else {
				rename (sourceFile, destFile);
			}
		}

		/// <summary>Deletes a file if it exists, logging any failure instead of throwing.</summary>
		internal static void TryDeleteFile (string path, Action<TraceLevel, string> logger)
		{
			if (!File.Exists (path))
				return;
			try { File.Delete (path); }
			catch (Exception ex) { logger (TraceLevel.Warning, $"Could not delete '{path}': {ex.Message}"); }
		}

		/// <summary>Recursively deletes a directory if it exists, logging any failure instead of throwing.</summary>
		internal static void TryDeleteDirectory (string path, string label, Action<TraceLevel, string> logger)
		{
			if (!Directory.Exists (path))
				return;
			try { Directory.Delete (path, recursive: true); }
			catch (Exception ex) { logger (TraceLevel.Warning, $"Could not clean up {label} at '{path}': {ex.Message}"); }
		}

		/// <summary>Moves a directory to the target path, backing up any existing directory and restoring on failure.</summary>
		internal static void MoveWithRollback (string sourcePath, string targetPath, Action<TraceLevel, string> logger)
		{
			string? backupPath = null;
			if (Directory.Exists (targetPath)) {
				backupPath = targetPath + $".old-{Guid.NewGuid ():N}";
				Directory.Move (targetPath, backupPath);
			}

			var parentDir = Path.GetDirectoryName (targetPath);
			if (!string.IsNullOrEmpty (parentDir))
				Directory.CreateDirectory (parentDir);

			try {
				Directory.Move (sourcePath, targetPath);
			}
			catch (Exception ex) {
				logger (TraceLevel.Error, $"Failed to move to '{targetPath}': {ex.Message}");
				if (backupPath is not null && Directory.Exists (backupPath)) {
					try {
						if (Directory.Exists (targetPath))
							Directory.Delete (targetPath, recursive: true);
						Directory.Move (backupPath, targetPath);
						logger (TraceLevel.Warning, $"Restored previous directory from backup '{backupPath}'.");
					}
					catch (Exception restoreEx) {
						logger (TraceLevel.Error, $"Failed to restore from backup: {restoreEx.Message}");
					}
				}
				throw;
			}

			// Delete backup only after move and caller validation succeed
			if (backupPath is not null)
				TryDeleteDirectory (backupPath, "old backup", logger);
		}

		/// <summary>Deletes a backup created by MoveWithRollback. Call after validation succeeds.</summary>
		internal static void CommitMove (string targetPath, Action<TraceLevel, string> logger)
		{
			// Find and clean up any leftover backup directories
			var parentDir = Path.GetDirectoryName (targetPath);
			if (string.IsNullOrEmpty (parentDir) || !Directory.Exists (parentDir))
				return;

			var dirName = Path.GetFileName (targetPath);
			foreach (var dir in Directory.GetDirectories (parentDir, $"{dirName}.old-*")) {
				TryDeleteDirectory (dir, "old backup", logger);
			}
		}

		/// <summary>Checks if the target path is writable by probing write access on the nearest existing ancestor.</summary>
		/// <remarks>
		/// Follows the same pattern as dotnet/sdk WorkloadInstallerFactory.CanWriteToDotnetRoot:
		/// probe with File.Create + DeleteOnClose, only catch UnauthorizedAccessException.
		/// See https://github.com/dotnet/sdk/blob/db01067a9c4b67dc1806956393ec63b032032166/src/Cli/dotnet/Commands/Workload/Install/WorkloadInstallerFactory.cs
		/// </remarks>
		internal static bool IsTargetPathWritable (string targetPath, Action<TraceLevel, string> logger)
		{
			if (string.IsNullOrEmpty (targetPath))
				return false;

			try {
				targetPath = Path.GetFullPath (targetPath);
			}
			catch {
				return false;
			}

			try {
				// Walk up to the nearest existing ancestor directory
				var testDir = targetPath;
				while (!string.IsNullOrEmpty (testDir) && !Directory.Exists (testDir))
					testDir = Path.GetDirectoryName (testDir);

				if (string.IsNullOrEmpty (testDir))
					return false;

				var testFile = Path.Combine (testDir, Path.GetRandomFileName ());
				using (File.Create (testFile, 1, FileOptions.DeleteOnClose)) { }
				return true;
			}
			catch (UnauthorizedAccessException) {
				logger (TraceLevel.Warning, $"Target path '{targetPath}' is not writable.");
				return false;
			}
		}

		/// <summary>Checks if a path is under a given directory.</summary>
		internal static bool IsUnderDirectory (string path, string directory)
		{
			if (string.IsNullOrEmpty (directory) || string.IsNullOrEmpty (path))
				return false;
			if (path.Equals (directory, StringComparison.OrdinalIgnoreCase))
				return true;
			return path.StartsWith (directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
		}

		// Returns .msi (Windows), .pkg (macOS), or null (Linux)
		internal static string? GetInstallerExtension ()
		{
			if (OS.IsWindows) return ".msi";
			if (OS.IsMac) return ".pkg";
			return null;
		}

		internal static string GetArchiveExtension ()
		{
			return OS.IsWindows ? ".zip" : ".tar.gz";
		}

		[DllImport ("libc", SetLastError=true)]
		static extern int rename (string old, string @new);
	}
}

