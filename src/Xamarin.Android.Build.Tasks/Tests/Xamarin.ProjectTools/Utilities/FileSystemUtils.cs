using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

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

				var stderr_completed = new ManualResetEvent (false);
				var stdout_completed = new ManualResetEvent (false);
				var stdout_lines = new List<string> ();
				var stderr_lines = new List<string> ();

				var p = new Process () {
					StartInfo   = psi,
				};

				p.ErrorDataReceived += (sender, e) => {
					if (e.Data == null) {
						stderr_completed.Set ();
					} else {
						stderr_lines.Add (e.Data);
					}
				};

				p.OutputDataReceived += (sender, e) => {
					if (e.Data == null) {
						stdout_completed.Set ();
					} else {
						stdout_lines.Add (e.Data);
					}
				};

				bool gotOutput = false;
				using (p) {
					p.StartInfo = psi;
					p.Start ();
					p.BeginOutputReadLine ();
					p.BeginErrorReadLine ();

					bool success = p.WaitForExit (60000);

					// We need to call the parameter-less WaitForExit only if any of the standard
					// streams have been redirected (see
					// https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.process.waitforexit?view=netframework-4.7.2#System_Diagnostics_Process_WaitForExit)
					//
					p.WaitForExit ();
					stderr_completed.WaitOne (TimeSpan.FromSeconds (60));
					stdout_completed.WaitOne (TimeSpan.FromSeconds (60));

					if (!success || p.ExitCode != 0) {
						Console.Error.WriteLine ($"Process `{psi.FileName} {psi.Arguments}` exited with value {p.ExitCode}.");
						if (stderr_lines.Count > 0) {
							foreach (string line in stderr_lines) {
								Console.Error.WriteLine (line);
							}
						}
					} else if (stdout_lines.Count > 0) {
						gotOutput = true;
					}
				}

				if (!gotOutput) {
					return CachedNugetGlobalPackageFolder = GetDefaultPackagesPath ();
				}

				string[] parts = stdout_lines[0].Split (NugetFieldSeparator, 2);
				if (parts.Length < 2) {
					Console.Error.WriteLine ($"Process `{psi.FileName} {psi.Arguments}` did not return expected output, using default nuget package cache path.");
					return CachedNugetGlobalPackageFolder = GetDefaultPackagesPath ();
				}

				return CachedNugetGlobalPackageFolder = parts [1].Trim ();

				string GetDefaultPackagesPath ()
				{
					return Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.UserProfile), ".nuget", "packages");
				}
			}

			return String.Empty;
		}
	}
}
