using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Xamarin.Android.BuildTools.PrepTasks;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	public class Emulator : PathToolTask
	{
		public                  string          Arguments                   { get; set; }

		[Output]
		public                  string[]        Output                      { get; set; }

		protected   virtual     bool            LogTaskMessages {
			get { return true; }
		}

		protected   override    string          ToolBaseName {
			get { return "emulator"; }
		}

		List<string> lines;
		List<string> Lines {
			get { return lines ?? (lines = new List<string> ()); }
		}

		public override bool Execute ()
		{
			if (LogTaskMessages) {
				Log.LogMessage (MessageImportance.Low, $"Task {nameof (Emulator)}");
				Log.LogMessage (MessageImportance.Low, $"  {nameof (Arguments)}: {Arguments}");
			}

			base.Execute ();

			Output  = lines?.ToArray ();

			if (LogTaskMessages) {
				Log.LogMessage (MessageImportance.Low, $"  [Output] {nameof (Output)}:");
				foreach (var line in (Output ?? new string [0]))
					Log.LogMessage (MessageImportance.Low, $"    {line}");
			}

			return !Log.HasLoggedErrors;
		}

		protected override string GenerateCommandLineCommands ()
		{
			return Arguments;
		}

		protected override void LogEventsFromTextOutput (string singleLine, MessageImportance messageImportance)
		{
			base.LogEventsFromTextOutput (singleLine, messageImportance);
			Lines.Add (singleLine);
		}
	}
}

