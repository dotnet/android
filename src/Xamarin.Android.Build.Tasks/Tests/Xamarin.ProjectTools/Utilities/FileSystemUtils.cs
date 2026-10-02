using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Xamarin.ProjectTools
{
	/// <summary>
	/// Provides utility methods for file system operations commonly needed in test scenarios.
	/// This class contains helper methods for managing file permissions, finding NuGet directories,
	/// and other file system tasks used by the test framework.
	/// </summary>
	public static class FileSystemUtils
	{
		/// <summary>
		/// Recursively sets a directory and all its contents to be writable by removing read-only attributes.
		/// </summary>
		/// <param name="directory">The directory path to make writable.</param>
		/// <remarks>
		/// This method is useful for cleaning up test directories that may have read-only files,
		/// allowing them to be deleted during test cleanup.
		/// </remarks>
		/// <seealso cref="SetFileWriteable(string)"/>
		public static void SetDirectoryWriteable (string directory)
		{
			if (!Directory.Exists (directory))
				return;

			var dirInfo = new DirectoryInfo (directory);
			dirInfo.Attributes &= ~FileAttributes.ReadOnly;

			foreach (var dir in Directory.GetDirectories (directory, "*", SearchOption.AllDirectories)) {
				dirInfo = new DirectoryInfo (dir);
				dirInfo.Attributes &= ~FileAttributes.ReadOnly;
			}

			foreach (var file in Directory.GetFiles (directory, "*", SearchOption.AllDirectories)) {
				SetFileWriteable (Path.GetFullPath (file));
			}
		}

		/// <summary>
		/// Recursively deletes a directory, retrying on transient failures.
		/// </summary>
		/// <param name="directory">The directory path to delete.</param>
		/// <param name="retries">The maximum number of retries before giving up.</param>
		/// <remarks>
		/// On Windows, a handle to a just-written file can still be held by another process
		/// (e.g. the Roslyn shared-compilation server, an anti-virus scanner, or the search
		/// indexer), causing <see cref="Directory.Delete(string, bool)"/> to throw
		/// <see cref="UnauthorizedAccessException"/> or <see cref="IOException"/>. This method
		/// backs off and retries to let the other process release the handle.
		/// </remarks>
		/// <seealso cref="SetDirectoryWriteable(string)"/>
		public static void DeleteDirectoryWithRetry (string directory, int retries = 10)
		{
			if (!Directory.Exists (directory))
				return;

			for (int i = 0; ; i++) {
				try {
					SetDirectoryWriteable (directory);
					Directory.Delete (directory, true);
					return;
				} catch (DirectoryNotFoundException) {
					return;
				} catch (Exception e) when ((e is UnauthorizedAccessException || e is IOException) && i < retries) {
					Thread.Sleep (200 * (i + 1)); // back off; let AV/Roslyn release the handle
				}
			}
		}

		/// <summary>
		/// Sets a single file to be writable by removing the read-only attribute if present.
		/// </summary>
		/// <param name="source">The file path to make writable.</param>
		/// <seealso cref="SetDirectoryWriteable(string)"/>
		public static void SetFileWriteable (string source)
		{
			if (!File.Exists (source))
				return;

			var fileInfo = new FileInfo (source);
			if (fileInfo.IsReadOnly)
				fileInfo.IsReadOnly = false;
		}

		static readonly char[] NugetFieldSeparator = new char[]{ ':' };
		static string CachedNugetGlobalPackageFolder;

		/// <summary>
		/// Finds the NuGet global packages folder by checking environment variables and using dotnet CLI.
		/// </summary>
		/// <returns>The path to the NuGet global packages folder, or null if not found.</returns>
		/// <remarks>
		/// First checks the NUGET_PACKAGES environment variable, then uses 'dotnet nuget locals' 
		/// command to determine the global packages location. This is used for configuring
		/// test projects with the correct package restore location.
		/// The result is cached to avoid repeated process invocations.
		/// </remarks>
		/// <seealso cref="TestEnvironment"/>
		public static string FindNugetGlobalPackageFolder ()
		{
			if (!string.IsNullOrEmpty (CachedNugetGlobalPackageFolder)) {
				return CachedNugetGlobalPackageFolder;
			}

			string packagesPath = Environment.GetEnvironmentVariable ("NUGET_PACKAGES");
			if (!String.IsNullOrEmpty (packagesPath)) {
				return CachedNugetGlobalPackageFolder = packagesPath;
			}

			bool isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;

			string dotnet = Path.Combine (TestEnvironment.DotNetPreviewDirectory, isWindows ? "dotnet.exe" : "dotnet");

			if (File.Exists (dotnet)) {
				var psi = new ProcessStartInfo (dotnet) {
					Arguments = $"nuget locals --list global-packages",
					CreateNoWindow = true,
					UseShellExecute = false,
					WindowStyle = ProcessWindowStyle.Hidden,
					RedirectStandardError = true,
					RedirectStandardOutput = true,
				};

				return CachedNugetGlobalPackageFolder = FindNugetGlobalPackageFolder (psi, 60000);
			}

			return String.Empty;
		}

		static string FindNugetGlobalPackageFolder (ProcessStartInfo psi, int timeoutMilliseconds)
		{
			var errorDone = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
			var outputDone = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
			var stdout_lines = new List<string> ();
			var stderr_lines = new List<string> ();
			var outputLock = new object ();
			bool acceptingOutput = true;

			using (var p = new Process { StartInfo = psi }) {
				p.ErrorDataReceived += (sender, e) => {
					if (e.Data == null) {
						errorDone.TrySetResult (true);
					} else {
						lock (outputLock) {
							if (acceptingOutput)
								stderr_lines.Add (e.Data);
						}
					}
				};

				p.OutputDataReceived += (sender, e) => {
					if (e.Data == null) {
						outputDone.TrySetResult (true);
					} else {
						lock (outputLock) {
							if (acceptingOutput)
								stdout_lines.Add (e.Data);
						}
					}
				};

				bool completed;
				try {
					p.Start ();
					p.BeginOutputReadLine ();
					p.BeginErrorReadLine ();
					completed = p.WaitForExit (timeoutMilliseconds);
					if (!completed) {
						Console.Error.WriteLine ($"Process `{psi.FileName} {psi.Arguments}` timed out after {timeoutMilliseconds}ms.");
						if (!p.HasExited) {
							try {
								p.Kill (entireProcessTree: true);
							} catch (InvalidOperationException) when (p.HasExited) {
								// The process exited before the kill request.
							}
							if (!p.WaitForExit (30000))
								Console.Error.WriteLine ($"Process {p.Id} did not exit after termination.");
						}
					}
					if (!Builder.WaitForRedirectedOutput (outputDone.Task, errorDone.Task))
						Console.Error.WriteLine ($"Process {p.Id} exited or timed out with redirected output still open.");
				} finally {
					lock (outputLock) {
						acceptingOutput = false;
					}
				}

				if (!completed || p.ExitCode != 0) {
					if (completed)
						Console.Error.WriteLine ($"Process `{psi.FileName} {psi.Arguments}` exited with value {p.ExitCode}.");
					foreach (string line in stderr_lines)
						Console.Error.WriteLine (line);
					return GetDefaultPackagesPath ();
				}
			}

			if (stdout_lines.Count == 0)
				return GetDefaultPackagesPath ();
			string [] parts = stdout_lines [0].Split (NugetFieldSeparator, 2);
			if (parts.Length < 2) {
				Console.Error.WriteLine ($"Process `{psi.FileName} {psi.Arguments}` did not return expected output, using default nuget package cache path.");
				return GetDefaultPackagesPath ();
			}

			return parts [1].Trim ();

			string GetDefaultPackagesPath ()
			{
				return Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.UserProfile), ".nuget", "packages");
			}
		}
	}
}
