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
	
	public class Aapt2Compile : Aapt2 {
		
		[Required]
		public ITaskItem OutputFlatArchive { get; set; }

		public bool ExplicitCrunch { get; set; }

		public override bool Execute ()
		{
			Log.LogDebugMessage ("Aapt2Compile Task");
			Log.LogDebugMessage ("  ResourceDirectory: {0}", ResourceDirectory);
			Log.LogDebugMessage ("  OutputFlatArchive: {0}", OutputFlatArchive);
			Log.LogDebugMessage ("  ResourceNameCaseMap: {0}", ResourceNameCaseMap);
			Log.LogDebugMessage ("  ResourceSymbolsTextFile: {0}", ResourceSymbolsTextFile);

			if (!Directory.EnumerateDirectories (ResourceDirectory).Any ())
				return true;

			LoadResourceCaseMap ();

			var output = new List<OutputLine> ();
			//var task = ThreadingTasks.Task.Run (() => ), Token);
			//task.ContinueWith ((obj) => { Complete (); });
			var success = RunAapt (GenerateCommandLineCommands (), output);//base.Execute ();
			foreach (var line in output) {
				if (line.StdError) {
					LogEventsFromTextOutput (line.Line, MessageImportance.Normal, success);
				} else {
					LogMessage (line.Line, MessageImportance.Normal);
				}
			}
			return !Log.HasLoggedErrors;
		}

		protected string GenerateCommandLineCommands ()
		{
			var cmd = new CommandLineBuilder ();
			cmd.AppendSwitch ("compile");
			cmd.AppendSwitchIfNotNull ("-o ", OutputFlatArchive.ItemSpec);
			if (!string.IsNullOrEmpty (ResourceSymbolsTextFile))
				cmd.AppendSwitchIfNotNull ("--output-text-symbols ", ResourceSymbolsTextFile);
			cmd.AppendSwitchIfNotNull ("--dir ", ResourceDirectoryFullPath);
			if (ExplicitCrunch)
				cmd.AppendSwitch ("--no-crunch");
			if (MonoAndroidHelper.LogInternalExceptions)
				cmd.AppendSwitch ("-v");
			return cmd.ToString ();
		}

	}
}