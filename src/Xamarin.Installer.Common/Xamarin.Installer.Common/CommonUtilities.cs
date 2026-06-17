using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Xamarin.Installer.Common.Properties;

namespace Xamarin.Installer.Common
{
	public static class CommonUtilities
	{
		const int SHORT_PATH_BUFFER_LENGTH = 1024;

		static readonly char[] invalidPathChars = Path.GetInvalidPathChars ();

		static IHelpers __helpers;
		static string __productName;

		public const string MANIFEST_TYPE_PROPERTY_KEY = "ManifestType";
		public const string DONT_SHOW_REPAIR_AGAIN_KEY = "DontShowRepairAgain";

		public static IHelpers Helpers {
			get {
				if (__helpers == null)
					throw new InvalidOperationException ("Internal error: Helpers must be initialized!");
				return __helpers; 
			}

			set {
				if (value == null)
					throw new ArgumentNullException ("value");
				__helpers = value;
			}
		}

		// Used in the proxy forms
		public static string ProductName {
			get {
				if (String.IsNullOrEmpty (__productName))
					throw new InvalidOperationException ("Internal error: ProductName must be initialized to a non-empty value");
				return __productName; 
			}

			set {
				if (String.IsNullOrEmpty (value))
					throw new ArgumentException ("Must not be empty or null", "value");
				__productName = value;
			}
		}

		public static void CopyDirectory (string sourceDirName, string destDirName, bool overwrite = false)
		{
			MoveDirectory (sourceDirName, destDirName, false, overwrite);
		}

		// Based on code from: http://foldermover.codeplex.com (Apache license)
		public static void MoveDirectory (string sourceDirName, string destDirName, bool move = true, bool overwrite = false, bool createSourceDirRootAtDestination = false)
		{
			if (sourceDirName == null)
				throw new ArgumentNullException ("sourceDirName", "The source directory cannot be null.");
			if (destDirName == null)
				throw new ArgumentNullException ("destDirName", "The destination directory cannot be null.");

			sourceDirName = sourceDirName.Trim ();
			destDirName = destDirName.Trim ();

			if ((sourceDirName.Length == 0) || (destDirName.Length == 0))
				throw new ArgumentException ("sourceDirName or destDirName is a zero-length string.");

			if (sourceDirName.IndexOfAny (invalidPathChars) > 0)
				throw new ArgumentException ("The directory contains invalid path characters.", "sourceDirName");

			if (destDirName.IndexOfAny (invalidPathChars) > 0)
				throw new ArgumentException ("The directory contains invalid path characters.", "destDirName");

			if (createSourceDirRootAtDestination)
				destDirName = Path.Combine (destDirName, Path.GetFileName (sourceDirName));

			var sourceDir = new DirectoryInfo (sourceDirName);
			var destDir = new DirectoryInfo (destDirName);

			if (!sourceDir.Exists)
				throw new DirectoryNotFoundException ("The path specified by sourceDirName is invalid: " + sourceDirName);
			bool destExists = destDir.Exists;
			if (!overwrite && destExists)
				throw new IOException ("The path specified by destDirName already exists: " + destDirName);

			if (!destExists)
				Directory.CreateDirectory (destDirName);

			//Copy the files in the current directory.
			FileInfo[] files = sourceDir.GetFiles ();
			IHelpers helpers = Helpers;
			foreach (FileInfo file in files) {
				string newPath = Path.Combine (destDirName, file.Name);
				if (helpers.IsSpecialFile (file.FullName))
					helpers.CopySpecialFile (file.FullName, newPath);
				else
					Copy (file, newPath, overwrite);

			}

			//Copy all sub directories.
			DirectoryInfo[] subDirs = sourceDir.GetDirectories ();
			foreach (DirectoryInfo subDir in subDirs) {
				string newPath = Path.Combine (destDirName, subDir.Name);
				MoveDirectory (subDir.FullName, newPath, move, overwrite);
			}

			if (move) {
				try {
					Directory.Delete (sourceDirName, true);
				} catch (Exception ex) {
					// Treat the error as warning - the files got copied to the destination and this is the
					// most important thing here.
					Logger.Exception ("Failed to delete source directory '{0}' after copying it to '{1}'", ex, sourceDir, destDir);
					Logger.Warning ("You will need to delete the source directory '{0}' manually", sourceDirName);
				}
			}
		}

		static void Copy (FileInfo source, string target, bool overwrite)
		{
			if (overwrite && File.Exists (target))
				File.Delete (target);
			source.CopyTo (target);
		}

		public static bool DeleteDirectoryRecursively (string path, bool throwOnError = false)
		{
			if (String.IsNullOrEmpty (path)) {
				Logger.Error ("Cannot delete directory recursively, no path given.");
				if (throwOnError)
					throw new ArgumentNullException ("path");
				return true;
			}
			if (!Directory.Exists (path)) {
				Logger.Warning ("Attempt to delete a non-existing directory '{0}'", path);
				if (throwOnError)
					throw new ArgumentException ("Directory does not exist", "path");
				return true;
			}

			try {
				DoDeleteDirectoryRecursively (new DirectoryInfo (path));
				return true;
			} catch (Exception ex) {
				Logger.Exception ("Failed to recursively delete directory '{0}'", ex, path);
				if (throwOnError)
					throw;
				return false;
			}
		}

		static void DoDeleteDirectoryRecursively (FileSystemInfo fi)
		{
			if (fi == null)
				return;

			fi.Attributes = FileAttributes.Normal;
			var di = fi as DirectoryInfo;
			string kind;
			if (di != null) {
				kind = "directory";
				foreach (var info in di.GetFileSystemInfos ())
					DoDeleteDirectoryRecursively (info);
			} else
				kind = "file";

			try {
				fi.Delete ();
			} catch (Exception ex) {
				Logger.Exception ("Failed to delete {0} '{1}',", ex, kind, fi.FullName);
				throw;
			}
		}
		
		public static string RunCommand (string command, out int exitCode, params string[] arguments)
		{
			return RunCommand (null, command, out exitCode, false, null, arguments);
		}

		public static string RunCommand (string workingDirectory, string command, out int exitCode, params string[] arguments)
		{
			return RunCommand (workingDirectory, command, out exitCode, false, null, arguments);
		}

		public static string RunCommand (string command, out int exitCode, bool fireAndForget, params string[] arguments)
		{
			return RunCommand (null, command, out exitCode, fireAndForget, null, arguments);
		}

		public static string RunCommand (string workingDirectory, string command, out int exitCode, bool fireAndForget, params string[] arguments)
		{
			return RunCommand (workingDirectory, command, out exitCode, fireAndForget, null, arguments);
		}

		public static string RunCommand (string workingDirectory, string command, out int exitCode, bool fireAndForget, Action<ProcessStartInfo> modifyStartInfo, params string[] arguments)
		{
			if (String.IsNullOrEmpty (command))
				throw new ArgumentNullException ("command");

			var si = new ProcessStartInfo (command) {
				UseShellExecute = false,
				CreateNoWindow = true,
			};

			if (!String.IsNullOrEmpty (workingDirectory))
				si.WorkingDirectory = workingDirectory;

			CleanEnvironment (si.EnvironmentVariables);
			StringBuilder output = null;
			ManualResetEvent stdout_completed = null;
			ManualResetEvent stderr_completed = null;

			if (!fireAndForget) {
				si.RedirectStandardOutput = true;
				si.RedirectStandardError = true;
				si.StandardOutputEncoding = Encoding.Default;
				si.StandardErrorEncoding = Encoding.Default;
				output = new StringBuilder ();
				stdout_completed = new ManualResetEvent (false);
				stderr_completed = new ManualResetEvent (false);
			}
			if (arguments != null && arguments.Length > 0) {
				var sb = new StringBuilder ();
				bool first = true;
				foreach (string a in arguments) {
					if (first)
						first = false;
					else
						sb.Append (' ');
					if (a.Any (c => Char.IsWhiteSpace (c)))
						sb.AppendFormat ("\"{0}\"", a);
					else
						sb.Append (a);
				}
				si.Arguments = sb.ToString ();
			}

			if (modifyStartInfo != null) {
				modifyStartInfo (si);
				if (!si.RedirectStandardError)
					si.StandardErrorEncoding = null;
				if (!si.RedirectStandardOutput)
					si.StandardOutputEncoding = null;
			}

			var p = new Process {
				StartInfo = si
			};
			p.Start ();

			if (!fireAndForget) {
				if (si.RedirectStandardOutput) {
					p.OutputDataReceived += (sender, e) => {
						if (e.Data != null) {
							lock (output) {
								output.AppendLine (e.Data);
							}
						} else
							stdout_completed.Set ();
					};
					p.BeginOutputReadLine ();
				}

				if (si.RedirectStandardError) {
					p.ErrorDataReceived += (sender, e) => {
						if (e.Data != null) {
							lock (output) {
								output.AppendLine (e.Data);
							}
						} else
							stderr_completed.Set ();
					};
					p.BeginErrorReadLine ();
				}

				p.WaitForExit ();
				if (si.RedirectStandardError)
					stderr_completed.WaitOne (TimeSpan.FromSeconds (1));
				if (si.RedirectStandardOutput)
					stdout_completed.WaitOne (TimeSpan.FromSeconds (1));

				exitCode = p.ExitCode;
				return output.ToString ();
			} else {
				if (p.HasExited)
					exitCode = p.ExitCode;
				else
					exitCode = 0;
			}

			return null;
		}

		[DllImport ("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		static extern uint GetShortPathName (
			[MarshalAs (UnmanagedType.LPTStr)] string longPath,
			[MarshalAs (UnmanagedType.LPTStr)] StringBuilder shortPath,
			uint shortPathLength);

		public static string GetShortPath (string path)
		{
			if (String.IsNullOrEmpty (path))
				return path;

			if (!Directory.Exists (path) && !File.Exists (path))
				return path;

			var sb = new StringBuilder (SHORT_PATH_BUFFER_LENGTH);
			try {
				uint result = GetShortPathName (path, sb, SHORT_PATH_BUFFER_LENGTH);
				if (result == 0) {
					int error = Marshal.GetLastWin32Error ();
					Logger.Warning ("failed to retrieve short path name for '{0}'. Long path form will be used instead. Error: {1}", path, error);
					return path;
				}
			} catch (Exception ex) {
				Logger.Exception ("failed to get short path name for '{0}'. Long path form will be used instead. Exception was thrown.", ex, path);
				return path;
			}

			return sb.ToString ();
		}

		static void CleanEnvironment (StringDictionary envvars)
		{
			envvars.Remove ("MONO_PATH");
			envvars.Remove ("MONO_GAC_PREFIX");
			envvars.Remove ("MONOMAC_LOGDIR");
			envvars.Remove ("PKG_CONFIG_PATH");
		}
	}
}
