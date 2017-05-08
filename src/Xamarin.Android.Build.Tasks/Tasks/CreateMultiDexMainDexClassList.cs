﻿// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.Collections.Specialized;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	public class CreateMultiDexMainDexClassList : JavaToolTask
	{
		[Required]
		public string ClassesOutputDirectory { get; set; }

		[Required]
		public string ProguardJarPath { get; set; }

		[Required]
		public string AndroidSdkBuildToolsPath { get; set; }

		[Required]
		public ITaskItem[] JavaLibraries { get; set; }
		
		public string MultiDexMainDexListFile { get; set; }
		public ITaskItem[] CustomMainDexListFiles { get; set; }
		public string ProguardInputJarFilter { get; set; }
		public string ExtraArgs { get; set; }

		Action<CommandLineBuilder> commandlineAction;
		string tempJar;
		bool writeOutputToKeepFile = false;

		public override bool Execute ()
		{
			Log.LogDebugMessage ("CreateMultiDexMainDexClassList");
			Log.LogDebugMessage ("  ClassesOutputDirectory: {0}", ClassesOutputDirectory);
			Log.LogDebugTaskItems ("  JavaLibraries:", JavaLibraries);
			Log.LogDebugMessage ("  MultiDexMainDexListFile: {0}", MultiDexMainDexListFile);
			Log.LogDebugTaskItems ("  CustomMainDexListFiles:", CustomMainDexListFiles);
			Log.LogDebugMessage ("  ToolExe: {0}", ToolExe);
			Log.LogDebugMessage ("  ToolPath: {0}", ToolPath);
			Log.LogDebugMessage ("  ProguardJarPath: {0}", ProguardJarPath);
			Log.LogDebugMessage ("  ProguardInputJarFilter: {0}", ProguardInputJarFilter);

			tempJar = Path.Combine (Path.GetTempPath (), Path.GetRandomFileName () + ".jar");
			commandlineAction = GenerateProguardCommands;
			// run proguard first
			var retval = base.Execute ();
			if (!retval || Log.HasLoggedErrors)
				return false;

			commandlineAction = GenerateMainDexListBuilderCommands;
			// run java second

			if (File.Exists (MultiDexMainDexListFile))
				File.WriteAllText (MultiDexMainDexListFile, string.Empty);

			var result = base.Execute () && !Log.HasLoggedErrors;

			if (result && CustomMainDexListFiles != null && CustomMainDexListFiles.Any (x => File.Exists (x.ItemSpec))) {
				foreach (var content in CustomMainDexListFiles.Select (i => File.ReadAllLines (i.ItemSpec)))
					File.AppendAllLines (MultiDexMainDexListFile, content);
			}

			return result;

		}

		protected override string GenerateCommandLineCommands ()
		{
			var cmd = new CommandLineBuilder ();
			commandlineAction (cmd);
			return cmd.ToString ();
		}

		void GenerateProguardCommands (CommandLineBuilder cmd)
		{
			var enclosingChar = OS.IsWindows ? "\"" : string.Empty;
			var jars = JavaLibraries.Select (i => i.ItemSpec).Concat (new string [] { Path.Combine (ClassesOutputDirectory, "..", "classes.zip") });
			cmd.AppendSwitchIfNotNull ("-jar ", ProguardJarPath);
			cmd.AppendSwitchUnquotedIfNotNull ("-injars ", "\"'" + string.Join ($"'{ProguardInputJarFilter}{Path.PathSeparator}'", jars) + $"'{ProguardInputJarFilter}\"");
			cmd.AppendSwitch ("-dontwarn");
			cmd.AppendSwitch ("-forceprocessing");
			cmd.AppendSwitchIfNotNull ("-outjars ", tempJar);
			cmd.AppendSwitchIfNotNull ("-libraryjars ", $"'{Path.Combine (AndroidSdkBuildToolsPath, "lib", "shrinkedAndroid.jar")}'");
			cmd.AppendSwitch ("-dontoptimize");
			cmd.AppendSwitch ("-dontobfuscate");
			cmd.AppendSwitch ("-dontpreverify");
			cmd.AppendSwitchUnquotedIfNotNull ("-include ", $"{enclosingChar}'{Path.Combine (AndroidSdkBuildToolsPath, "mainDexClasses.rules")}'{enclosingChar}");
		}

		void GenerateMainDexListBuilderCommands(CommandLineBuilder cmd)
		{
			var enclosingDoubleQuote = OS.IsWindows ? "\"" : string.Empty;
			var enclosingQuote = OS.IsWindows ? string.Empty : "'";
			var jars = JavaLibraries.Select (i => i.ItemSpec).Concat (new string [] { Path.Combine (ClassesOutputDirectory, "..", "classes.zip") });
			cmd.AppendSwitchIfNotNull ("-Djava.ext.dirs=", Path.Combine (AndroidSdkBuildToolsPath, "lib"));
			cmd.AppendSwitch ("com.android.multidex.MainDexListBuilder");
			if (!string.IsNullOrWhiteSpace (ExtraArgs))
				cmd.AppendSwitch (ExtraArgs);
			cmd.AppendSwitch ($"{enclosingDoubleQuote}{tempJar}{enclosingDoubleQuote}");
			cmd.AppendSwitchUnquotedIfNotNull ("", $"{enclosingDoubleQuote}{enclosingQuote}" +
				string.Join ($"{enclosingQuote}{Path.PathSeparator}{enclosingQuote}", jars) + 
				$"{enclosingQuote}{enclosingDoubleQuote}");
			writeOutputToKeepFile = true;
		}

		protected override void LogEventsFromTextOutput (string singleLine, MessageImportance messageImportance)
		{
			var match = CodeErrorRegEx.Match (singleLine);
			var exceptionMatch = ExceptionRegEx.Match (singleLine);

			if (writeOutputToKeepFile && !match.Success && !exceptionMatch.Success)
				File.AppendAllText (MultiDexMainDexListFile, singleLine + "\n");
			base.LogEventsFromTextOutput (singleLine, messageImportance);
		}
	}
}

