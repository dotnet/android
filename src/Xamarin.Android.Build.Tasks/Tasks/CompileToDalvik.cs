// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	public class CompileToDalvik : JavaToolTask
	{
		public ITaskItem[] AdditionalJavaLibraryReferences { get; set; }

		[Required]
		public string ClassesOutputDirectory { get; set; }

		public string DxJarPath { get; set; }

		public string DxExtraArguments { get; set; }

		public string JavaToolPath { get; set; }

		[Required]
		public ITaskItem[] JavaLibrariesToCompile { get; set; }

		public ITaskItem [] AlternativeJarFiles { get; set; }

		public bool UseDx { get; set; }

		public bool MultiDexEnabled { get; set; }
		
		public string MultiDexMainDexListFile { get; set; }

		[Output]
		public string [] DexOutputs { get; set; }

		string inputListFile;

		protected override string ToolName {
			get {
				if (UseDx)
					return OS.IsWindows ? "dx.bat" : "dx";
				return OS.IsWindows ? "java.exe" : "java";
			}
		}

		public override bool Execute ()
		{
			Log.LogDebugMessage ("CompileToDalvik");
			Log.LogDebugMessage ("  JavaOptions: {0}", JavaOptions);
			Log.LogDebugMessage ("  JavaMaximumHeapSize: {0}", JavaMaximumHeapSize);
			Log.LogDebugMessage ("  ClassesOutputDirectory: {0}", ClassesOutputDirectory);
			Log.LogDebugMessage ("  JavaToolPath: {0}", JavaToolPath);
			Log.LogDebugMessage ("  DxJarPath: {0}", DxJarPath);
			Log.LogDebugMessage ("  ToolExe: {0}",  ToolExe);
			Log.LogDebugMessage ("  ToolPath: {0}", ToolPath);
			Log.LogDebugMessage ("  UseDx: {0}", UseDx);
			Log.LogDebugMessage ("  DxExtraArguments: {0}", DxExtraArguments);
			Log.LogDebugMessage ("  MultiDexEnabled: {0}", MultiDexEnabled);
			Log.LogDebugMessage ("  MultiDexMainDexListFile: {0}", MultiDexMainDexListFile);
			Log.LogDebugTaskItems ("  JavaLibrariesToCompile:", JavaLibrariesToCompile);
			Log.LogDebugTaskItems ("  AlternativeJarFiles:", AlternativeJarFiles);

			if (!Directory.Exists (ClassesOutputDirectory))
				Directory.CreateDirectory (ClassesOutputDirectory);

			bool ret = false;
			inputListFile = Path.GetTempFileName ();
			try {
				ret = base.Execute ();

				DexOutputs = Directory.GetFiles (Path.GetDirectoryName (ClassesOutputDirectory), "*.dex", SearchOption.TopDirectoryOnly);

				Log.LogDebugTaskItems ("  DexOutputs: ", DexOutputs);
			} catch (FileNotFoundException ex) {
				Log.LogCodedError ("XA1003", ex.ToString ());
			} finally {
				if (File.Exists (inputListFile) && !Log.HasLoggedErrors)
					File.Delete (inputListFile);
			}

			return ret && !Log.HasLoggedErrors;
		}

		protected override string GenerateCommandLineCommands ()
		{
			//   Running command: C:\Program Files\Java\jdk1.6.0_25\bin\java.exe -jar
			//     C:\Program Files (x86)\Android\android-sdk\platform-tools\lib\dx.jar --dex
			//     --output=C:\Users\jeff\Documents\Visual Studio 2010\Projects\<project>\...\android\bin\classes.dex
			//     C:\Users\jeff\Documents\Visual Studio 2010\Projects\<project>\...\android\bin\classes
			//     C:\Users\jeff\Documents\Visual Studio 2010\Projects\<project>\...\android\bin\mono.android.jar

			var cmd = new CommandLineBuilder ();

			if (!UseDx) {
				// Add the JavaOptions if they are not null
				// These could be any of the additional options
				if (!string.IsNullOrEmpty (JavaOptions)) {
					cmd.AppendSwitch (JavaOptions);		
				}

				// Add the specific -XmxN to override the default heap size for the JVM
				// N can be in the form of Nm or NGB (e.g 100m or 1GB ) 
				cmd.AppendSwitchIfNotNull("-Xmx", JavaMaximumHeapSize);

				cmd.AppendSwitchIfNotNull ("-jar ", Path.Combine (DxJarPath));
			}

			cmd.AppendSwitch (DxExtraArguments);

			cmd.AppendSwitchIfNotNull ("--input-list=", inputListFile);

			if (MultiDexEnabled) {
				cmd.AppendSwitch ("--multi-dex");
				cmd.AppendSwitchIfNotNull ("--main-dex-list=", MultiDexMainDexListFile);
			}
			cmd.AppendSwitchIfNotNull ("--output ", Path.GetDirectoryName (ClassesOutputDirectory));

			using (var sw = new StreamWriter (path: inputListFile, append: false,
					encoding: new UTF8Encoding (encoderShouldEmitUTF8Identifier: false))) {
				// .jar files
				if (AlternativeJarFiles != null && AlternativeJarFiles.Any ()) {
					Log.LogDebugMessage ("  processing AlternativeJarFiles...");
					foreach (var l in AlternativeJarFiles) {
						var fullPath = Path.GetFullPath (l.ItemSpec);
						Log.LogDebugMessage ($"    {fullPath}");
						sw.WriteLine (fullPath);
					}
				} else {
					Log.LogDebugMessage ("  processing ClassesOutputDirectory...");
					var zip = Path.GetFullPath (Path.Combine (ClassesOutputDirectory, "..", "classes.zip"));
					if (!File.Exists (zip)) {
						throw new FileNotFoundException ($"'{zip}' does not exist. Please rebuild the project.");
					}
					Log.LogDebugMessage ($"    {zip}");
					sw.WriteLine (Path.GetFullPath (zip));
					foreach (var jar in JavaLibrariesToCompile) {
						var fullPath = Path.GetFullPath (jar.ItemSpec);
						Log.LogDebugMessage ($"    {fullPath}");
						sw.WriteLine (fullPath);
					}
				}
			}

			return cmd.ToString ();
		}

		protected override string GenerateFullPathToTool ()
		{
			if (UseDx)
				return Path.Combine (ToolPath, ToolExe);
			return Path.Combine (JavaToolPath, ToolName);
		}
	}
}
