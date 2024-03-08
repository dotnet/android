using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Xamarin.Android.BuildTools.PrepTasks;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	public class Adb : Task
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

			public string       Arguments            => ArgumentsGenerator != null ? ArgumentsGenerator () : ArgumentsString;
		}

		List<string>            lines = new List <string> ();
		object                  linesLock = new object ();

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

		public override bool Execute ()
		{
			List <CommandInfo> commandArguments = GenerateCommandArguments ();
			if (commandArguments == null || commandArguments.Count == 0)
				return !Log.HasLoggedErrors;

			string adbPath = Path.Combine (ToolPath, ToolExe);
			for (int i = 0; i < commandArguments.Count; i++) {
				CommandInfo info = commandArguments [i];
				if (info.ShouldRun != null && !info.ShouldRun ())
					continue;

				info.StdoutWriter = OpenOutputFile (info.StdoutFilePath, info.StdoutAppend);
				if (!info.MergeStdoutAndStderr)
					info.StderrWriter = OpenOutputFile (info.StderrFilePath, info.StderrAppend);
				BeforeCommand (i, info);
				try {
					Log.LogMessage (MessageImportance.Normal, $"Executing: {adbPath} {info.Arguments}");
					if (info.StdoutWriter != null)
						LogFileWrite ("stdout", info.StdoutFilePath, info.StdoutAppend);
					if (info.StderrWriter != null)
						LogFileWrite ("stderr", info.StderrFilePath, info.StderrAppend);
					int exitCode = RunCommand (adbPath, info);
					if (exitCode == 0)
						continue;

					bool ignoreExit = IgnoreExitCode | info.IgnoreExitCode;
					string message = $"  Command {adbPath} {info.Arguments} failed with exit code {exitCode}";
					if (!ignoreExit) {
						Log.LogError (message);
						break;
					}

					if (info.LogIgnoredExitCodeAsWarning)
						Log.LogWarning (message);
					else
						Log.LogMessage (MessageImportance.Normal, message);
				} catch {
					throw;
				} finally {
					AfterCommand (i, info);
					info.StdoutWriter?.Dispose ();
					info.StdoutWriter = null;
					info.StderrWriter?.Dispose ();
					info.StderrWriter = null;
				}
			}

			Output  = lines?.ToArray ();

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
			lock (linesLock) lines.Add (line);

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

		int RunCommand (string commandPath, CommandInfo info)
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
			si.Arguments = info.Arguments;

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

			ManualResetEvent stdout_completed = null;
			if (!si.RedirectStandardError)
				si.StandardErrorEncoding = null;
			else
				stdout_completed = new ManualResetEvent (false);

			ManualResetEvent stderr_completed = null;
			if (!si.RedirectStandardOutput)
				si.StandardOutputEncoding = null;
			else
				stderr_completed = new ManualResetEvent (false);

			var p = new Process {
				StartInfo = si
			};
			p.Start ();

			var outputLock = new Object ();

			if (si.RedirectStandardOutput) {
				p.OutputDataReceived += (sender, e) => {
					if (e.Data != null)
						OnOutput (e.Data, true, info);
					else
						stdout_completed.Set ();
				};
				p.BeginOutputReadLine ();
			}

			if (si.RedirectStandardError) {
				p.ErrorDataReceived += (sender, e) => {
					if (e.Data != null)
						OnOutput (e.Data, false, info);
					else
						stderr_completed.Set ();
				};
				p.BeginErrorReadLine ();
			}

			bool needToWait = true;
			bool exited = true;
			if (Timeout > 0) {
				exited = p.WaitForExit (Timeout);
				if (!exited) {
					Log.LogWarning ($"  Process '{commandPath} {si.Arguments}' failed to exit within the timeout of {Timeout}ms, killing the process");
					p.Kill ();
				}

				// We need to call the parameter-less WaitForExit only if any of the standard output
				// streams have been redirected (see
				// https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.process.waitforexit?view=netframework-4.7.2#System_Diagnostics_Process_WaitForExit)
				//
				if (!si.RedirectStandardOutput && !si.RedirectStandardError)
					needToWait = false;
			}

			if (needToWait)
				p.WaitForExit ();

			if (si.RedirectStandardError && stderr_completed != null)
				stderr_completed.WaitOne (TimeSpan.FromSeconds (OutputTimeout));
			if (si.RedirectStandardOutput && stdout_completed != null)
				stdout_completed.WaitOne (TimeSpan.FromSeconds (OutputTimeout));

			return exited? p.ExitCode : -1;
		}

	}
}

