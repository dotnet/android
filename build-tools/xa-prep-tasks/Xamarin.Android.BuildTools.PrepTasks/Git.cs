using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using IOFile = System.IO.File;

namespace Xamarin.Android.BuildTools.PrepTasks
{
	public class Git : ToolTask
	{
		[Required]
		public                  ITaskItem       WorkingDirectory            { get; set; }

		public                  string          Arguments                   { get; set; }

		[Output]
		public                  string[]        Output                      { get; set; }

		protected   virtual     bool            LogTaskMessages {
			get { return true; }
		}

		protected   override    string          ToolName {
			get {
				return "git" + (Path.DirectorySeparatorChar == '\\' ? ".exe" : "");
			}
		}

		List<string> lines;
		List<string> Lines {
			get { return lines ?? (lines = new List<string> ()); }
		}

		public override bool Execute ()
		{
			if (LogTaskMessages) {
				Log.LogMessage (MessageImportance.Low, $"Task {nameof (Git)}");
				Log.LogMessage (MessageImportance.Low, $"  {nameof (WorkingDirectory)}: {WorkingDirectory.ItemSpec}");
			}

			base.Execute ();

			Output  = Lines?.ToArray ();

			if (LogTaskMessages) {
				Log.LogMessage (MessageImportance.Low, $"  [Output] {nameof (Output)}:");
				foreach (var line in (Output ?? new string [0]))
					Log.LogMessage (MessageImportance.Low, $"    {line}");
			}

			return !Log.HasLoggedErrors;
		}

		protected override string GenerateFullPathToTool ()
		{
			var app     = string.IsNullOrEmpty (ToolExe) ? ToolName: ToolExe;
			var path    = !string.IsNullOrEmpty (ToolPath) ? ToolPath : GetDirectoryFromPath (app);
			return Path.Combine (path, app);
		}

		string GetDirectoryFromPath (string app)
		{
			foreach (var p in Environment.GetEnvironmentVariable ("PATH").Split (new [] { Path.PathSeparator.ToString () }, StringSplitOptions.RemoveEmptyEntries)) {
				if (File.Exists (Path.Combine (p, app))) {
					return p;
				}
			}
			return null;
		}

		protected override string GenerateCommandLineCommands ()
		{
			return Arguments;
		}

		protected override string GetWorkingDirectory ()
		{
			return WorkingDirectory.ItemSpec;
		}

		protected override void LogEventsFromTextOutput (string singleLine, MessageImportance messageImportance)
		{
			Lines.Add (singleLine);
		}
	}
}

