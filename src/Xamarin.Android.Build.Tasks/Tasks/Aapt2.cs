// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using System.Text;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Xamarin.Android.Tools;
using ThreadingTasks = System.Threading.Tasks;
using Xamarin.Build;

namespace Xamarin.Android.Tasks {
	
	public class Aapt2 : AsyncTask {

		protected Dictionary<string, string> resource_name_case_map;

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
				StandardOutputEncoding = Encoding.UTF8,
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
							output.Add (new OutputLine (e.Data, stdError: !IsAapt2Warning (e.Data)));
					else
						stderr_completed.Set ();
				};
				LogDebugMessage ("Executing {0}", commandLine);
				proc.StartInfo = psi;
				proc.Start ();
				proc.BeginOutputReadLine ();
				proc.BeginErrorReadLine ();
				CancellationToken.Register (() => {
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

		bool IsAapt2Warning (string singleLine)
		{
			var match = AndroidToolTask.AndroidErrorRegex.Match (singleLine.Trim ());
			if (match.Success) {
				var file = match.Groups ["file"].Value;
				var level = match.Groups ["level"].Value.ToLowerInvariant ();
				var message = match.Groups ["message"].Value;
				if (file.StartsWith ("W/", StringComparison.OrdinalIgnoreCase))
					return true;
				if (message.Contains ("warn:"))
					return true;
				if (level.Contains ("warning"))
					return true;
			}
			return false;
		}

		protected bool LogAapt2EventsFromOutput (string singleLine, MessageImportance messageImportance, bool apptResult)
		{
			if (string.IsNullOrEmpty (singleLine))
				return true;

			var match = AndroidToolTask.AndroidErrorRegex.Match (singleLine.Trim ());

			if (match.Success) {
				var file = match.Groups ["file"].Value;
				int line = 0;
				if (!string.IsNullOrEmpty (match.Groups ["line"]?.Value))
					line = int.Parse (match.Groups ["line"].Value.Trim ()) + 1;
				var level = match.Groups ["level"].Value.ToLowerInvariant ();
				var message = match.Groups ["message"].Value;

				// Handle the following which is NOT an error :(
				// W/ResourceType(23681): For resource 0x0101053d, entry index(1341) is beyond type entryCount(733)
				// W/ResourceType( 3681): For resource 0x0101053d, entry index(1341) is beyond type entryCount(733)
				if (file.StartsWith ("W/", StringComparison.OrdinalIgnoreCase)) {
					LogCodedWarning ("APT0000", singleLine);
					return true;
				}
				if (message.StartsWith ("unknown option", StringComparison.OrdinalIgnoreCase)) {
					// we need to filter out the remailing help lines somehow. 
					LogCodedError ("APT0001", $"{message}. This is the result of using `aapt` command line arguments with `aapt2`. The arguments are not compatible.");
					return false;
				}
				if (message.Contains ("fakeLogOpen")) {
					LogMessage (singleLine, messageImportance);
					return true;
				}
				if (message.Contains ("note:")) {
					LogMessage (singleLine, messageImportance);
					return true;
				}
				if (message.Contains ("warn:")) {
					LogCodedWarning ("APT0000", singleLine);
					return true;
				}
				if (level.Contains ("note")) {
					LogMessage (message, messageImportance);
					return true;
				}
				if (level.Contains ("warning")) {
					LogCodedWarning ("APT0000", singleLine);
					return true;
				}

				// Try to map back to the original resource file, so when the user
				// double clicks the error, it won't take them to the obj/Debug copy
				if (ResourceDirectories != null) {
					foreach (var dir in ResourceDirectories) {
						var resourceDirectory = dir.ItemSpec;
						var resourceDirectoryFullPath = ResourceDirectoryFullPath (resourceDirectory);

						string newfile = MonoAndroidHelper.FixUpAndroidResourcePath (file, resourceDirectory, resourceDirectoryFullPath, resource_name_case_map);
						if (!string.IsNullOrEmpty (newfile)) {
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
					return true;
				}
			}

			if (!apptResult) {
				LogCodedError ("APT0000", string.Format ("{0} \"{1}\".", singleLine.Trim (), singleLine.Substring (singleLine.LastIndexOfAny (new char [] { '\\', '/' }) + 1)), ToolName);
			} else {
				LogCodedWarning ("APT0000", singleLine);
			}
			return true;
		}

		protected void LoadResourceCaseMap ()
		{
			resource_name_case_map = MonoAndroidHelper.LoadResourceCaseMap (ResourceNameCaseMap);
		}
	}
}
