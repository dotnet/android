using System;
using System.IO;
using System.Text;

#if UNIX
using Mono.Unix.Native;
#endif

namespace Xamarin.Android.Prepare
{
	static partial class Utilities
	{
		public static string MakeID (string name)
		{
			var sb = new StringBuilder ();

			char previousChar = Char.MinValue;
			foreach (char ch in name) {
				if (!Char.IsDigit (ch) && !Char.IsLetter (ch)) {
					if (previousChar == '_') {
						continue;
					}

					sb.Append ('_');
					previousChar = '_';
					continue;
				}

				sb.Append (ch);
				previousChar = ch;
			}

			return sb.ToString ();
		}

		public static void Touch (string path)
		{
			var now = DateTime.Now;
			if (Utilities.FileExists (path)) {
				SetTime (now, nameof (File.SetLastWriteTime), File.SetLastWriteTime);
				SetTime (now, nameof (File.SetLastAccessTime), File.SetLastAccessTime);
			} else if (Directory.Exists (path)) {
				SetTime (now, nameof (Directory.SetLastWriteTime), Directory.SetLastWriteTime);
				SetTime (now, nameof (Directory.SetLastAccessTime), Directory.SetLastAccessTime);
			}

			void SetTime (DateTime time, string setterName, Action<string, DateTime> setter)
			{
				try {
					setter (path, time);
				} catch (Exception ex) {
					Log.Instance.WarningLine ($"Failed to set filesystem entry time for '{path}' ({setterName})");
					Log.Instance.DebugLine ("Exception thrown:");
					Log.Instance.DebugLine (ex.ToString ());
				}
			}
		}

		/// <summary>
		///   If <paramref name="path"/> is not rooted, create a full path rooted at the Xamarin.Android source
		///   directory.  <paramref name="path"/> must not be empty.
		/// </summary>
		public static string EnsureFullPath (string path)
		{
			if (path.Length == 0) {
				throw new ArgumentException (nameof (path), "must not be empty");
			}

			if (Path.IsPathRooted (path)) {
				return path;
			}

			return Path.Combine (BuildPaths.XamarinAndroidSourceRoot, path);
		}

#if UNIX
		public static bool ProcessHUP (int processId)
		{
			return SendSignal (nameof (ProcessHUP), Signum.SIGHUP, processId);
		}

		public static bool ProcessKILL (int processId)
		{
			return SendSignal (nameof (ProcessKILL), Signum.SIGKILL, processId);
		}

		static bool SendSignal (string funcName, Signum sig, int processId)
		{
			if (processId <= 1) {
				Log.Instance.DebugLine ($"{funcName}: ignoring request to kill PID {processId}");
				return true; // just ignore
			}

			Log.Instance.DebugLine ($"Sending {sig} to process {processId}");
			int result = Syscall.kill (processId, sig);
			if (result == 0) {
				Log.Instance.DebugLine ($"{sig} delivered to process {processId}");
				return true;
			}

			Errno errno = Stdlib.GetLastError ();
			if (errno == Errno.ESRCH) {
				Log.Instance.DebugLine ($"Process {processId} is already gone, {sig} not sent (not an error)");
				return true;
			}

			Log.Instance.WarningLine ($"Failed to deliver {sig} to process {processId}: {Stdlib.strerror (errno)}");
			return false;
		}
#else
		public static bool ProcessHUP (int processId)
		{
			return true;
		}

		public static bool ProcessKILL (int processId)
		{
			return true;
		}
#endif
	}
}
