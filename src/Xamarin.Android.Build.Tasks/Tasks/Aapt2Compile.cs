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

		List<ITaskItem> archives = new List<ITaskItem> ();

		public bool ExplicitCrunch { get; set; }

		[Output]
		public ITaskItem [] CompiledResourceFlatArchives => archives.ToArray ();

		public override bool Execute ()
		{
			Log.LogDebugMessage ("Aapt2Compile Task");
			Log.LogDebugMessage ("  ResourceNameCaseMap: {0}", ResourceNameCaseMap);
			Log.LogDebugMessage ("  ResourceSymbolsTextFile: {0}", ResourceSymbolsTextFile);
			Log.LogDebugTaskItems ("  ResourceDirectories: ", ResourceDirectories);

			Yield ();
			try {
				var task = ThreadingTasks.Task.Run (() => {
					DoExecute ();
				}, Token);

				task.ContinueWith (Complete);

				base.Execute ();
			} finally {
				Reacquire ();
			}

			return !Log.HasLoggedErrors;
		}

		void DoExecute ()
		{
			LoadResourceCaseMap ();

			ThreadingTasks.ParallelOptions options = new ThreadingTasks.ParallelOptions {
				CancellationToken = Token,
				TaskScheduler = ThreadingTasks.TaskScheduler.Default,
			};

			ThreadingTasks.Parallel.ForEach (ResourceDirectories, options, ProcessDirectory);
		}

		void ProcessDirectory (ITaskItem resourceDirectory)
		{
			if (!Directory.EnumerateDirectories (resourceDirectory.ItemSpec).Any ())
				return;
			
			var output = new List<OutputLine> ();
			var outputArchive = Path.Combine (resourceDirectory.ItemSpec, "..", "compiled.flata");
			var success = RunAapt (GenerateCommandLineCommands (resourceDirectory, outputArchive), output);
			if (success && File.Exists (Path.Combine (WorkingDirectory, outputArchive))) {
				archives.Add (new TaskItem (outputArchive));
			}
			foreach (var line in output) {
				if (line.StdError) {
					LogEventsFromTextOutput (line.Line, MessageImportance.Normal, success);
				} else {
					LogMessage (line.Line, MessageImportance.Normal);
				}
			}
		}

		protected string GenerateCommandLineCommands (ITaskItem dir, string outputArchive)
		{
			var cmd = new CommandLineBuilder ();
			cmd.AppendSwitch ("compile");
			cmd.AppendSwitchIfNotNull ("-o ", outputArchive);
			if (!string.IsNullOrEmpty (ResourceSymbolsTextFile))
				cmd.AppendSwitchIfNotNull ("--output-text-symbols ", ResourceSymbolsTextFile);
			cmd.AppendSwitchIfNotNull ("--dir ", ResourceDirectoryFullPath (dir.ItemSpec));
			if (ExplicitCrunch)
				cmd.AppendSwitch ("--no-crunch");
			if (MonoAndroidHelper.LogInternalExceptions)
				cmd.AppendSwitch ("-v");
			return cmd.ToString ();
		}

	}
}