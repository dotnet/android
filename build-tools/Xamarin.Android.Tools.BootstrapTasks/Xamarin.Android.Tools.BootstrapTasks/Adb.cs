using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Tasks = System.Threading.Tasks;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	public class Adb : Task, ICancelableTask
	{
		protected class CommandInfo
		{
			public string       ArgumentsString      { get; set; }
			public Func<string> ArgumentsGenerator   { get; set; }
			public bool         MergeStdoutAndStderr { get; set; } = true;
			public bool         IgnoreExitCode       { get; set; }
			public bool         LogIgnoredExitCodeAsWarning { get; set; } = true;
			public string       StdoutFilePath       { get; set; }
			public bool         StdoutAppend         { get; set; }
			public string       StderrFilePath       { get; set; }
			public bool         StderrAppend         { get; set; }
			public StreamWriter StdoutWriter         { get; set; }
			public StreamWriter StderrWriter         { get; set; }
			public Func<bool>   ShouldRun            { get; set; } = () => true;
			public bool         SuppressMSbuildLog   { get; set; }
			public int          ExitCode             { get; set; } = -1;

			public string       Arguments            => ArgumentsGenerator != null ? ArgumentsGenerator () : ArgumentsString;
		}

		readonly List<string>   lines = new List <string> ();
		readonly Lock           cancellationLock = new Lock ();
		CancellationTokenSource commandCancellation;
		volatile bool           canceled;

		public                  string          AdbTarget             { get; set; }
		public                  string          AdbOptions            { get; set; }
		public                  string          Arguments             { get; set; }
		public                  string          WorkingDirectory      { get; set; }
		public                  bool            IgnoreExitCode        { get; set; }
		public                  int             Timeout               { get; set; } = -1;
		public                  string[]        EnvironmentVariables  { get; set; }
		public                  bool            WriteOutputAsMessage  { get; set; } = false;

		[Required]
		public                  string          ToolPath              { get; set; }

		[Required]
		public                  string          ToolExe               { get; set; }

		[Output]
		public                  string[]        Output                { get; set; }

		protected   virtual     int             OutputTimeout         => 30; // seconds

		protected bool IsCancellationRequested => canceled;

		public void Cancel ()
		{
			lock (cancellationLock) {
				canceled = true;
				commandCancellation?.Cancel ();
			}
		}

		public override bool Execute ()
		{
			using var cancellation = new CancellationTokenSource ();
			lock (cancellationLock) {
				if (canceled)
					return false;
				commandCancellation = cancellation;
			}
			lines.Clear ();
			try {
				bool success = ExecuteCommands (cancellation.Token);
				return success && !cancellation.IsCancellationRequested;
			} catch (OperationCanceledException) when (cancellation.IsCancellationRequested) {
				return false;
			} finally {
				lock (cancellationLock) commandCancellation = null;
				Output = lines.ToArray ();
			}
		}

		protected virtual bool ExecuteCommands (CancellationToken cancellationToken)
		{
			List <CommandInfo> commandArguments = GenerateCommandArguments ();
			if (commandArguments == null || commandArguments.Count == 0)
				return !Log.HasLoggedErrors;

			string adbPath = Path.Combine (ToolPath, ToolExe);
			for (int i = 0; i < commandArguments.Count; i++) {
				cancellationToken.ThrowIfCancellationRequested ();
				CommandInfo info = commandArguments [i];
				if (info.ShouldRun != null && !info.ShouldRun ())
					continue;
				info.ExitCode = -1;

				try {
					using (info.StdoutWriter = OpenOutputFile (info.StdoutFilePath, info.StdoutAppend))
					using (info.StderrWriter = info.MergeStdoutAndStderr ? null : OpenOutputFile (info.StderrFilePath, info.StderrAppend)) {
						BeforeCommand (i, info);
						try {
							string arguments = info.Arguments;
							Log.LogMessage (MessageImportance.Normal, $"Executing: {adbPath} {arguments}");
							if (info.StdoutWriter != null)
								LogFileWrite ("stdout", info.StdoutFilePath, info.StdoutAppend);
							if (info.StderrWriter != null)
								LogFileWrite ("stderr", info.StderrFilePath, info.StderrAppend);
							info.ExitCode = RunCommand (adbPath, arguments, info, cancellationToken);
							if (info.ExitCode == 0)
								continue;

							bool ignoreExit = IgnoreExitCode | info.IgnoreExitCode;
							string message = $"  Command {adbPath} {arguments} failed with exit code {info.ExitCode}";
							if (!ignoreExit) {
								Log.LogError (message);
								break;
							}

							if (info.LogIgnoredExitCodeAsWarning)
								Log.LogWarning (message);
							else
								Log.LogMessage (MessageImportance.Normal, message);
						} finally {
							AfterCommand (i, info);
						}
					}
				} finally {
					info.StdoutWriter = null;
					info.StderrWriter = null;
				}
			}

			return !Log.HasLoggedErrors;
		}

		void LogFileWrite (string streamName, string outputFileName, bool appends)
		{
			string op = appends ? "appending" : "writing";
			Log.LogMessage (MessageImportance.Normal, $"  {op} {streamName} to file: {outputFileName}");
		}

		StreamWriter OpenOutputFile (string path, bool appendIfExists)
		{
			if (String.IsNullOrEmpty (path))
				return null;

			return new StreamWriter (appendIfExists ? File.Open (path, FileMode.Append) : File.Create (path));
		}

		protected virtual List <CommandInfo> GenerateCommandArguments ()
		{
			return new List <CommandInfo> {
				new CommandInfo {
					ArgumentsString = Arguments
				}
			};
		}

		protected virtual void BeforeCommand (int commandIndex, CommandInfo info)
		{}

		protected virtual void AfterCommand (int commandIndex, CommandInfo info)
		{}

		protected virtual void CustomizeProcessStartInfo (ProcessStartInfo psi)
		{}

		protected virtual void ProcessStdout (string line)
		{}

		protected virtual void ProcessStderr (string line)
		{}

		void OnOutput (string line, bool isStdout, CommandInfo info)
		{
			lines.Add (line);

			if (!info.SuppressMSbuildLog) {
				if (isStdout || WriteOutputAsMessage)
					Log.LogMessage (MessageImportance.Low, line);
				else
					Log.LogWarning (line);
			}

			if (isStdout || info.MergeStdoutAndStderr) {
				info.StdoutWriter?.WriteLine (line);
				ProcessStdout (line);
			} else {
				info.StderrWriter?.WriteLine (line);
				ProcessStderr (line);
			}
		}

		int RunCommand (string commandPath, string arguments, CommandInfo info, CancellationToken cancellationToken)
		{
			var si = new ProcessStartInfo (commandPath) {
				UseShellExecute = false,
				CreateNoWindow = true,
			};

			if (!String.IsNullOrEmpty (WorkingDirectory))
				si.WorkingDirectory = WorkingDirectory;

			si.RedirectStandardOutput = true;
			si.RedirectStandardError = true;
			si.StandardOutputEncoding = Encoding.Default;
			si.StandardErrorEncoding = Encoding.Default;
			si.Arguments = arguments;

			if (EnvironmentVariables != null && EnvironmentVariables.Length > 0) {
				foreach (string ev in EnvironmentVariables) {
					string name;
					string value;

					int idx = ev.IndexOf ('=');
					if (idx < 0) {
						name = ev.Trim ();
						value = String.Empty;
					} else {
						name = ev.Substring (0, idx).Trim ();
						value = ev.Substring (idx + 1).Trim ();
					}

					if (String.IsNullOrEmpty (name)) {
						Log.LogWarning ($"  Invalid environment variable definition: '{ev}'");
						continue;
					}

					if (String.IsNullOrEmpty (value))
						value = "1";

					Log.LogMessage (MessageImportance.Low, $"  Defining environment variable: {name} = {value}");
					si.EnvironmentVariables.Add (name, value);
				}
			}

			CustomizeProcessStartInfo (si);

			if (!si.RedirectStandardError)
				si.StandardErrorEncoding = null;
			if (!si.RedirectStandardOutput)
				si.StandardOutputEncoding = null;

			using var p = new Process {
				StartInfo = si
			};
			p.Start ();

			using var exitCancellation = CancellationTokenSource.CreateLinkedTokenSource (cancellationToken);
			using var outputCancellation = CancellationTokenSource.CreateLinkedTokenSource (cancellationToken);
			if (Timeout > 0)
				exitCancellation.CancelAfter (Timeout);
			var exit = WaitForExitAsync ();
			try {
				if (si.RedirectStandardOutput && si.RedirectStandardError) {
					var output = p.ReadAllLinesAsync (outputCancellation.Token).GetAsyncEnumerator ();
					try {
						while (output.MoveNextAsync ().AsTask ().GetAwaiter ().GetResult ()) {
							var line = output.Current;
							OnOutput (line.Content, !line.StandardError, info);
						}
					} finally {
						output.DisposeAsync ().AsTask ().GetAwaiter ().GetResult ();
					}
				} else if (si.RedirectStandardOutput || si.RedirectStandardError) {
					var reader = si.RedirectStandardOutput ? p.StandardOutput : p.StandardError;
					while (reader.ReadLineAsync (outputCancellation.Token).AsTask ().GetAwaiter ().GetResult () is string line)
						OnOutput (line, si.RedirectStandardOutput, info);
				}

				int exitCode = exit.GetAwaiter ().GetResult ();
				if (exitCancellation.IsCancellationRequested)
					Log.LogWarning ($"  Process '{commandPath} {arguments}' failed to exit within the timeout of {Timeout}ms.");
				return exitCode;
			} catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
				if (exit.IsFaulted)
					exit.GetAwaiter ().GetResult ();
				if (exitCancellation.IsCancellationRequested)
					Log.LogWarning ($"  Process '{commandPath} {arguments}' failed to exit within the timeout of {Timeout}ms.");
				Log.LogWarning ($"  Process '{commandPath} {arguments}' output did not close within {OutputTimeout}s after exit.");
				return -1;
			} finally {
				exitCancellation.Cancel ();
				outputCancellation.Cancel ();
				try {
					try {
						exit.GetAwaiter ().GetResult ();
					} catch (OperationCanceledException) when (exitCancellation.IsCancellationRequested) {
					}
				} finally {
					try {
						// ADB can start a shared server. Only this finite client belongs to us.
						try {
							if (!p.HasExited)
								p.Kill ();
						} catch (InvalidOperationException) when (p.HasExited) {
						}
						if (!p.SafeHandle.TryWaitForExit (TimeSpan.FromSeconds (5), out _))
							Log.LogError ($"Process '{commandPath} {arguments}' did not exit after termination.");
					} catch (Win32Exception ex) {
						Log.LogError ($"Could not terminate '{commandPath} {arguments}': {ex.Message}");
					} finally {
						using var stdout = si.RedirectStandardOutput ? p.StandardOutput : null;
						using var stderr = si.RedirectStandardError ? p.StandardError : null;
					}
				}
			}

			async Tasks.Task<int> WaitForExitAsync ()
			{
				try {
					int exitCode;
					try {
						var status = await p.SafeHandle.WaitForExitAsync (exitCancellation.Token).ConfigureAwait (false);
						exitCancellation.CancelAfter (System.Threading.Timeout.Infinite);
						exitCode = status.ExitCode;
					} catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && exitCancellation.IsCancellationRequested) {
						try {
							if (!p.HasExited)
								p.Kill ();
						} catch (InvalidOperationException) when (p.HasExited) {
						}
						if (!p.SafeHandle.TryWaitForExit (TimeSpan.FromSeconds (5), out _))
							throw new TimeoutException ($"Process '{commandPath} {arguments}' did not exit after termination.");
						exitCode = -1;
					}
					if (OutputTimeout >= 0)
						outputCancellation.CancelAfter (TimeSpan.FromSeconds (OutputTimeout));
					return exitCode;
				} catch {
					outputCancellation.Cancel ();
					throw;
				}
			}
		}
	}
}

