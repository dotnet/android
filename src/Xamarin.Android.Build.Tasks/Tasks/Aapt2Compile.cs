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

namespace Xamarin.Android.Tasks {
	
	public class Aapt2Compile : Aapt2 {
		public override string TaskPrefix => "A2C";

		List<ITaskItem> archives = new List<ITaskItem> ();

		public string ExtraArgs { get; set; }

		public string FlatArchivesDirectory { get; set; }

		[Output]
		public ITaskItem [] CompiledResourceFlatArchives => archives.ToArray ();

		public override System.Threading.Tasks.Task RunTaskAsync ()
		{
			LoadResourceCaseMap ();

			return this.WhenAllWithLock (ResourceDirectories, ProcessDirectory);
		}

		void ProcessDirectory (ITaskItem resourceDirectory, object lockObject)
		{
			if (!Directory.EnumerateDirectories (resourceDirectory.ItemSpec).Any ())
				return;
			
			var output = new List<OutputLine> ();
			var hash = resourceDirectory.GetMetadata ("Hash");
			var filename = !string.IsNullOrEmpty (hash) ? hash : "compiled";
			var outputArchive = Path.Combine (FlatArchivesDirectory, $"{filename}.flata");
			var success = RunAapt (GenerateCommandLineCommands (resourceDirectory, outputArchive), output);
			if (success && File.Exists (Path.Combine (WorkingDirectory, outputArchive))) {
				lock (lockObject)
					archives.Add (new TaskItem (outputArchive));
			}
			foreach (var line in output) {
				if (!LogAapt2EventsFromOutput (line.Line, MessageImportance.Normal, success))
					break;
			}
		}

		protected string GenerateCommandLineCommands (ITaskItem dir, string outputArchive)
		{
			var cmd = new CommandLineBuilder ();
			cmd.AppendSwitch ("compile");
			cmd.AppendSwitchIfNotNull ("-o ", outputArchive);
			if (!string.IsNullOrEmpty (ResourceSymbolsTextFile))
				cmd.AppendSwitchIfNotNull ("--output-text-symbols ", ResourceSymbolsTextFile);
			cmd.AppendSwitchIfNotNull ("--dir ", dir.ItemSpec.TrimEnd ('\\'));
			if (!string.IsNullOrEmpty (ExtraArgs))
				cmd.AppendSwitch (ExtraArgs);
			if (MonoAndroidHelper.LogInternalExceptions)
				cmd.AppendSwitch ("-v");
			return cmd.ToString ();
		}

	}
}