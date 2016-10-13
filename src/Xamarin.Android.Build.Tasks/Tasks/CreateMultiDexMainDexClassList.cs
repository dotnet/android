// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.Collections.Specialized;
using Xamarin.Android.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	public class CreateMultiDexMainDexClassList : ToolTask
	{
		[Required]
		public string ClassesOutputDirectory { get; set; }

		[Required]
		public string ProguardHome { get; set; }

		[Required]
		public ITaskItem[] JavaLibraries { get; set; }
		
		public string MultiDexMainDexListFile { get; set; }
		public ITaskItem[] CustomMainDexListFiles { get; set; }

		public override bool Execute ()
		{
			Log.LogDebugMessage ("CreateMultiDexMainDexClassList");
			Log.LogDebugMessage ("  ClassesOutputDirectory: {0}", ClassesOutputDirectory);
			Log.LogDebugTaskItems ("  JavaLibraries:", JavaLibraries);
			Log.LogDebugMessage ("  MultiDexMainDexListFile: {0}", MultiDexMainDexListFile);
			Log.LogDebugTaskItems ("  CustomMainDexListFiles:", CustomMainDexListFiles);
			Log.LogDebugMessage ("  ToolExe: {0}", ToolExe);
			Log.LogDebugMessage ("  ToolPath: {0}", ToolPath);
			Log.LogDebugMessage ("  ProguardHome: {0}", ProguardHome);
			
			if (CustomMainDexListFiles != null && CustomMainDexListFiles.Any ()) {
				var content = string.Concat (CustomMainDexListFiles.Select (i => File.ReadAllText (i.ItemSpec)));
				File.WriteAllText (MultiDexMainDexListFile, content);
				return true;
			}

			return base.Execute ();
		}

		protected override string GenerateCommandLineCommands ()
		{
			var cmd = new CommandLineBuilder ();

			cmd.AppendSwitch ("--output");
			cmd.AppendFileNameIfNotNull (MultiDexMainDexListFile);

			var jars = JavaLibraries.Select (i => i.ItemSpec).Concat (new string [] { ClassesOutputDirectory });
			string files = string.Join (Path.PathSeparator.ToString (), jars.Select (s => '\'' + s + '\''));
			if (OS.IsWindows)
				cmd.AppendSwitch ('"' + files + '"');
			else
				cmd.AppendSwitch (files);

			return cmd.ToString ();
		}

		protected override string ToolName {
			get {
				return OS.IsWindows ? "mainDexClasses.bat" : "mainDexClasses";
			}
		}

		protected override string GenerateFullPathToTool ()
		{
			return Path.Combine (ToolPath, ToolExe);
		}

		protected override StringDictionary EnvironmentOverride {
			get {
				var sd = base.EnvironmentOverride ?? new StringDictionary ();
				// Windows seems to need special care.
				var opts = sd.ContainsKey ("JAVA_TOOL_OPTIONS") ? sd ["JAVA_TOOL_OPTIONS"] : null;
				opts += " -Dfile.encoding=UTF8";
				sd ["JAVA_TOOL_OPTIONS"] = opts;

				sd ["PROGUARD_HOME"] = ProguardHome;
				return sd;
			}
		}
	}
}

