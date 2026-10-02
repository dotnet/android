using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;

namespace MonoDroid.Utils {

	public static class ProcessRocks {

		public static IEnumerable<string> ReadStandardOutput (IEnumerable<string> commandLine, bool printCommandLine)
		{
			var psi = new ProcessStartInfo () {
				FileName                = commandLine.First (),
				Arguments               = "\"" + string.Join ("\" \"", commandLine.Skip (1).ToArray ()) + "\"",
			};
			return ReadStandardOutput (psi, printCommandLine);
		}

		public static IEnumerable<string> ReadStandardOutput (ProcessStartInfo psi, bool printCommandLine)
		{
			psi.RedirectStandardError = true;
			psi.RedirectStandardOutput = true;
			psi.UseShellExecute = false;
			psi.InheritedHandles = [];
			
			if (printCommandLine)
				Console.WriteLine ("Running command: {0} {1}", psi.FileName, psi.Arguments);
			
			var timer = Stopwatch.StartNew ();
			using (Process p = Process.Start (psi) ?? throw new InvalidOperationException ($"Could not start '{psi.FileName}'.")) {
				var stderr = new StringBuilder ();
				var timeout = TimeSpan.FromMinutes (5);
				try {
					foreach (var line in p.ReadAllLines (timeout)) {
						if (line.StandardError) {
							stderr.AppendLine (line.Content);
						} else {
							yield return line.Content;
						}
					}

					var remaining = timeout - timer.Elapsed;
					var status = p.SafeHandle.WaitForExitOrKillOnTimeout (remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero);
					if (status.Canceled) {
						throw new TimeoutException ($"'{psi.FileName}' did not complete within {timeout}.");
					}
					if (status.ExitCode != 0) {
						throw new CommandFailedException (psi.FileName, psi.Arguments, stderr.ToString (), status.ExitCode);
					}
				} finally {
					p.SafeHandle.WaitForExitOrKillOnTimeout (TimeSpan.Zero);
				}
			}
			timer.Stop ();
			if (printCommandLine)
				Console.WriteLine ("\tProcess executed in: {0}", timer.Elapsed);
		}
	}
	
	static class MessageUtils {
		
		internal static string MapGeneratedToProjectFile (string filename)
		{
			// At this point, all we have is something like this:
			// ...\MonoDroidApplication22\obj\Debug\res\layout\main.axml
			// We are going to best guess it back to the original file:
			// ...\MonoDroidApplication22\Resources\Layout\Main.axml
			
			try {
				// Find the root, by stripping off \obj and beyond
				string root = filename.Substring (0, filename.IndexOf (string.Format ("{0}obj{0}", Path.DirectorySeparatorChar), StringComparison.Ordinal));
				
				var files = FindFileInDirectory (root, Path.GetFileName (filename));

				// This pretty much only works if there is only 1 matching file
				// name, which should be the commong case
				if (files.Count == 1)
					return files[0];
				
				// We couldn't successfully map, return only the file name,
				// and let the user figure it out.
				return Path.GetFileName (filename);
			} catch (Exception) {
				return Path.GetFileName (filename);
			}
		}
		
		private static List<string> FindFileInDirectory (string directory, string filename)
		{
			var results = new List<string> ();
			
			// Recurse
			foreach (var dir in Directory.GetDirectories (directory)) {
				// Don't go into obj or bin directories				
				if (Path.GetFileName (dir).ToLowerInvariant () == "obj" || Path.GetFileName (dir).ToLowerInvariant () == "bin")
					continue;
			
				results.AddRange (FindFileInDirectory (dir, filename));
			}
			
			// Check this directory for the file
			foreach (var file in Directory.GetFiles (directory)) {
				if (Path.GetFileName (file).ToLowerInvariant () == filename.ToLowerInvariant ())
					results.Add (file);
			}
			
			return results;
		}		
	}

	class CommandFailedException : InvalidOperationException
	{
		public string FileName { get; private set; }
		public string Arguments { get; private set; }
		public string ErrorLog { get; private set; }
		public int ExitCode { get; private set; }
		public new string Message { get; private set; }
		
		public CommandFailedException () : base ()
		{
		}
		
		public CommandFailedException (string message) : base (message)
		{
		}
		
		public CommandFailedException (string filename, string arguments, string errorLog, int exitCode)
		{
			FileName = filename;
			Arguments = arguments;
			ErrorLog = errorLog;
			ExitCode = exitCode;
			
			
			Message = "Command failed. Command: " + FileName + " " + Arguments + "\n" +
							"\t" + (string.IsNullOrEmpty (ErrorLog) ? "<none>\n" : ErrorLog);
		}

		public string VSFormattedErrorLog {
			get { return FormatForVS (ErrorLog); }
		}
		
		private string FormatForVS (string text)
		{
			Regex regex = new Regex (@"(?<FileName>.+):(?<LineNumber>\d+): error: Error: (?<Error>.+)");

			if (!regex.IsMatch (text))
				return text;

			var match = regex.Match (text);

			string filename = match.Groups["FileName"].Value;
			string line = match.Groups["LineNumber"].Value;
			string error = match.Groups["Error"].Value;

			int line_no;

			if (!int.TryParse (line, out line_no))
				return text;

			// Fix off by one error
			line_no++;

			return string.Format ("{0}({1}): error 1: {2}", MessageUtils.MapGeneratedToProjectFile (filename), line_no, error);
		}
	}
}
