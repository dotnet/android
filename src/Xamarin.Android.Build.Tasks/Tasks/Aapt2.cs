// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Xamarin.Android.Tools;
using ThreadingTasks = System.Threading.Tasks;

namespace Xamarin.Android.Tasks {
	
	public class Aapt2 : AsyncTask {

		protected Dictionary<string, string> resource_name_case_map = new Dictionary<string, string> ();

		public ITaskItem [] ResourceDirectories { get; set; }

		public string ResourceNameCaseMap { get; set; }

		public string ResourceSymbolsTextFile { get; set; }

		protected string ToolName { get { return OS.IsWindows ? "aapt2.exe" : "aapt2"; } }

		public string ToolPath { get; set; }

		public string ToolExe { get; set; }

		protected string ResourceDirectoryFullPath (string resourceDirectory)
		{
			return (Path.IsPathRooted (resourceDirectory) ? resourceDirectory : Path.Combine (WorkingDirectory, resourceDirectory)).TrimEnd ('\\');
		}

		protected string GenerateFullPathToTool ()
		{
			return Path.Combine (ToolPath, string.IsNullOrEmpty (ToolExe) ? ToolName : ToolExe);
		}

		protected bool RunAapt (string commandLine, IList<OutputLine> output)
		{
			var stdout_completed = new ManualResetEvent (false);
			var stderr_completed = new ManualResetEvent (false);
			var psi = new ProcessStartInfo () {
				FileName = GenerateFullPathToTool (),
				Arguments = commandLine,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden,
				WorkingDirectory = WorkingDirectory,
			};
			object lockObject = new object ();
			using (var proc = new Process ()) {
				proc.OutputDataReceived += (sender, e) => {
					if (e.Data != null)
						lock (lockObject)
							output.Add (new OutputLine (e.Data, stdError: false));
					else
						stdout_completed.Set ();
				};
				proc.ErrorDataReceived += (sender, e) => {
					if (e.Data != null)
						lock (lockObject)
							output.Add (new OutputLine (e.Data, stdError: true));
					else
						stderr_completed.Set ();
				};
				LogDebugMessage ("Executing {0}", commandLine);
				proc.StartInfo = psi;
				proc.Start ();
				proc.BeginOutputReadLine ();
				proc.BeginErrorReadLine ();
				Token.Register (() => {
					try {
						proc.Kill ();
					} catch (Exception) {
					}
				});
				proc.WaitForExit ();
				if (psi.RedirectStandardError)
					stderr_completed.WaitOne (TimeSpan.FromSeconds (30));
				if (psi.RedirectStandardOutput)
					stdout_completed.WaitOne (TimeSpan.FromSeconds (30));
				return proc.ExitCode == 0 && !output.Any (x => x.StdError);
			}
		}

		protected void LogEventsFromTextOutput (string singleLine, MessageImportance messageImportance, bool apptResult)
		{
			if (string.IsNullOrEmpty (singleLine))
				return;

			var match = AndroidToolTask.AndroidErrorRegex.Match (singleLine.Trim ());

			if (match.Success) {
				var file = match.Groups ["file"].Value;
				int line = 0;
				if (!string.IsNullOrEmpty (match.Groups ["line"]?.Value))
					line = int.Parse (match.Groups ["line"].Value) + 1;
				var level = match.Groups ["level"].Value.ToLowerInvariant ();
				var message = match.Groups ["message"].Value;
				if (message.Contains ("fakeLogOpen")) {
					LogMessage (singleLine, messageImportance);
					return;
				}
				if (message.Contains ("note:")) {
					LogMessage (singleLine, messageImportance);
					return;
				}
				if (message.Contains ("warn:")) {
					LogCodedWarning ("APT0000", singleLine);
					return;
				}
				if (level.Contains ("note")) {
					LogMessage (message, messageImportance);
					return;
				}
				if (level.Contains ("warning")) {
					LogCodedWarning ("APT0000", singleLine);
					return;
				}

				// Try to map back to the original resource file, so when the user
				// double clicks the error, it won't take them to the obj/Debug copy
				if (ResourceDirectories != null) {
					foreach (var dir in ResourceDirectories) {
						var resourceDirectoryFullPath = ResourceDirectoryFullPath (dir.ItemSpec);
						if (file.StartsWith (resourceDirectoryFullPath, StringComparison.InvariantCultureIgnoreCase)) {
							var newfile = file.Substring (resourceDirectoryFullPath.Length).TrimStart (Path.DirectorySeparatorChar);
							newfile = resource_name_case_map.ContainsKey (newfile) ? resource_name_case_map [newfile] : newfile;
							newfile = Path.Combine ("Resources", newfile);
							file = newfile;
							break;
						}
					}
				}

				// Strip any "Error:" text from aapt's output
				if (message.StartsWith ("error: ", StringComparison.InvariantCultureIgnoreCase))
					message = message.Substring ("error: ".Length);

				if (level.Contains ("error") || (line != 0 && !string.IsNullOrEmpty (file))) {
					LogCodedError ("APT0000", message, file, line);
					return;
				}
			}

			if (!apptResult) {
				LogCodedError ("APT0000", string.Format ("{0} \"{1}\".", singleLine.Trim (), singleLine.Substring (singleLine.LastIndexOfAny (new char [] { '\\', '/' }) + 1)), ToolName);
			} else {
				LogCodedWarning ("APT0000", singleLine);
			}
		}

		protected void LoadResourceCaseMap ()
		{
			if (ResourceNameCaseMap != null)
				foreach (var arr in ResourceNameCaseMap.Split (';').Select (l => l.Split ('|')).Where (a => a.Length == 2))
					resource_name_case_map [arr [1]] = arr [0]; // lowercase -> original
		}
	}
}
