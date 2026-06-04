// Copyright (C) 2015 Xamarin, Inc. All rights reserved.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.Text.RegularExpressions;
using Xamarin.AndroidTools;
using System.Collections.Generic;
using System.Text;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class Adb : AndroidRunToolTask {
		public override string TaskPrefix => "ADB";

		[Required]
		public string Command { get; set; }

		public string Filter { get; set; }

		public string Capture { get; set; }

		[Output]
		public string Output { get; set; }

		Regex filter = null;
		StringBuilder sb = new StringBuilder ();

		protected override string DefaultErrorCode => "ADB0000";

		public override bool RunTask ()
		{
			if (!string.IsNullOrWhiteSpace (Filter))
				filter = new Regex (Filter);
			base.Execute ();
			Output = sb.ToString().Trim ();
			return !Log.HasLoggedErrors;
		}

		protected override void LogEventsFromTextOutput (string singleLine, MessageImportance messageImportance)
		{
			if (filter == null) {
				base.LogEventsFromTextOutput (singleLine, messageImportance);
				sb.AppendLine (singleLine);
				return;
			}
			var matches = filter.Matches (singleLine.Trim ());
			foreach (Match match in matches) {
				if (!string.IsNullOrWhiteSpace (Capture)) {
					sb.AppendLine (match.Groups [Capture].ToString ());
				} else {
					foreach (Group grp in match.Groups) {
						sb.AppendLine (grp.Value);
					}
				}
			}
		}

		protected virtual CommandLineBuilder CreateCommandLine()
		{
			var cmd = new CommandLineBuilder ();
			cmd.AppendSwitch (Command);
			return cmd;
		}

		protected override string GenerateCommandLineCommands ()
		{
			var cmd = CreateCommandLine ();
			return cmd.ToString ();
		}

		protected override string GenerateFullPathToTool ()
		{
			return Path.Combine (ToolPath, ToolExe);
		}

		protected override string ToolName
		{
			get { return IsWindows ? "adb.exe" : "adb"; }
		}
	}
}

