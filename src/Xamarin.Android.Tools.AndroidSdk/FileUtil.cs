using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Xamarin.Android.Tools
{
	class FileUtil
	{
		internal static IEnumerable<string> FindExecutablesInPath (string executable)
		{
			foreach (var directory in (Environment.GetEnvironmentVariable (EnvironmentVariableNames.Path) ?? "").Split (Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)) {
				foreach (var path in FindExecutablesInDirectory (directory, executable))
					yield return path;
			}
		}

		internal static IEnumerable<string> FindExecutablesInDirectory (string directory, string executable)
		{
			if (!Directory.Exists (directory))
				yield break;
			foreach (var extension in (Environment.GetEnvironmentVariable (EnvironmentVariableNames.PathExt) ?? "").Split (Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)) {
				var path = Path.Combine (directory, Path.ChangeExtension (executable, extension));
				if (File.Exists (path))
					yield return path;
			}
			var unextended = Path.Combine (directory, executable);
			if (File.Exists (unextended))
				yield return unextended;
		}

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

		/// <summary>
		/// Extracts a zip archive to a temp directory, locates the expected top-level folder,
		/// and moves it to the target path with rollback support.
		/// </summary>
		internal static void ExtractAndInstall (string archivePath, string targetPath, string expectedTopDir, Action<TraceLevel, string> logger, CancellationToken cancellationToken)
		{
			var tempExtractDir = Path.Combine (Path.GetTempPath (), $"extract-{Guid.NewGuid ()}");
			try {
				Directory.CreateDirectory (tempExtractDir);
				DownloadUtils.ExtractZipSafe (archivePath, tempExtractDir, cancellationToken);

				var extractedDir = Path.Combine (tempExtractDir, expectedTopDir);
				if (!Directory.Exists (extractedDir)) {
					var dirs = Directory.GetDirectories (tempExtractDir);
					extractedDir = dirs.Length == 1 ? dirs[0] : tempExtractDir;
				}

				MoveWithRollback (extractedDir, targetPath, logger);
				logger (TraceLevel.Info, $"Installed to '{targetPath}'.");
			}
			finally {
				TryDeleteDirectory (tempExtractDir, "temp extract dir", logger);
			}
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
		internal static bool IsUnderDirectory (string? path, string? directory)
		{
			if (string.IsNullOrEmpty (directory) || string.IsNullOrEmpty (path))
				return false;

			var fullPath = Path.GetFullPath (path);
			var fullDirectory = Path.GetFullPath (directory);
			if (fullPath.Equals (fullDirectory, StringComparison.Ordinal))
				return true;
			if (!fullDirectory.EndsWith (Path.DirectorySeparatorChar.ToString (), StringComparison.Ordinal))
				fullDirectory += Path.DirectorySeparatorChar;
			return fullPath.StartsWith (fullDirectory, StringComparison.Ordinal);
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


		/// <summary>
		/// Sets Unix file permissions with File.SetUnixFileMode.
		/// </summary>
		internal static bool Chmod (string path, int mode)
		{
			if (OS.IsWindows)
				return true; // No-op on Windows

			try {
				if (!OperatingSystem.IsWindows ()) {
					File.SetUnixFileMode (path, (UnixFileMode) mode);
					return true;
				}
				return true;
			}
			catch {
				return false;
			}
		}

		/// <summary>
		/// Sets executable permissions on all files in the bin/ subdirectory.
		/// Uses File.SetUnixFileMode and reports permission failures to the caller.
		/// </summary>
		internal static Task SetExecutablePermissionsAsync (string directory, Action<TraceLevel, string> logger, CancellationToken cancellationToken = default)
		{
			var binDir = Path.Combine (directory, "bin");
			if (!Directory.Exists (binDir))
				return Task.CompletedTask;

			foreach (var file in Directory.GetFiles (binDir)) {
				cancellationToken.ThrowIfCancellationRequested ();
				if (!Chmod (file, 0x1ED)) { // 0755 C# does not have octal literals
					logger (TraceLevel.Error, $"Failed to set executable permission on '{file}'.");
					throw new InvalidOperationException ($"Could not set executable permission on '{file}'.");
				}
			}
			return Task.CompletedTask;
		}

		[DllImport ("libc", SetLastError=true)]
		static extern int rename (string old, string @new);

	}
}
