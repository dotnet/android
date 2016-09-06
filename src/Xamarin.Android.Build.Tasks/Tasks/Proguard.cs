// Copyright (C) 2012 Xamarin, Inc. All rights reserved.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.Text.RegularExpressions;
using Xamarin.Android.Build.Utilities;
using System.IO.Compression;
using Xamarin.Tools.Zip;

namespace Xamarin.Android.Tasks
{
	// See AOSP/sdk/eclipse/plugins/com.android.ide.eclipse.adt/src/com/android/ide/eclipse/adt/internal/build/BuildHelper.java
	public class Proguard : ToolTask
	{
		public string ProguardJarPath { get; set; }

		public string JavaToolPath { get; set; }

		[Required]
		public string JavaPlatformJarPath { get; set; }

		[Required]
		public string ClassesOutputDirectory { get; set; }

		[Required]
		public string AcwMapFile { get; set; }

		[Required]
		public string ProguardJarInput { get; set; }

		[Required]
		public string ProguardJarOutput { get; set; }

		[Required]
		public string ProguardGeneratedReferenceConfiguration { get; set; }

		[Required]
		public string ProguardGeneratedApplicationConfiguration { get; set; }

		[Required]
		public string ProguardCommonXamarinConfiguration { get; set; }

		public string ProguardConfigurationFiles { get; set; }

		public ITaskItem[] JavaLibrariesToEmbed { get; set; }
		
		public ITaskItem[] ExternalJavaLibraries { get; set; }
		
		public ITaskItem[] DoNotPackageJavaLibraries { get; set; }

		public bool UseProguard { get; set; }

		public string JavaOptions { get; set; }

		public string JavaMaximumHeapSize { get; set; }

		public bool EnableLogging { get; set; }
		public string DumpOutput { get; set; }
		public string PrintSeedsOutput { get; set; }
		public string PrintUsageOutput { get; set; }
		public string PrintMappingOutput { get; set; }

		protected override string ToolName {
			get {
				if (UseProguard)
					return OS.IsWindows ? "proguard.bat" : "proguard";
				return OS.IsWindows ? "java.exe" : "java";
			}
		}

		public override bool Execute ()
		{
			Log.LogDebugMessage ("Proguard");
			Log.LogDebugMessage ("  JavaPlatformJarPath: {0}", JavaPlatformJarPath);
			Log.LogDebugMessage ("  ClassesOutputDirectory: {0}", ClassesOutputDirectory);
			Log.LogDebugMessage ("  AcwMapFile: {0}", AcwMapFile);
			Log.LogDebugMessage ("  ProguardGeneratedApplicationConfiguration: {0}", ProguardGeneratedApplicationConfiguration);
			Log.LogDebugMessage ("  ProguardJarInput: {0}", ProguardJarInput);
			Log.LogDebugMessage ("  ProguardJarOutput: {0}", ProguardJarOutput);
			Log.LogDebugTaskItems ("  ProguardGeneratedReferenceConfiguration:", ProguardGeneratedReferenceConfiguration);
			Log.LogDebugTaskItems ("  ProguardGeneratedApplicationConfiguration:", ProguardGeneratedApplicationConfiguration);
			Log.LogDebugTaskItems ("  ProguardCommonXamarinConfiguration:", ProguardCommonXamarinConfiguration);
			Log.LogDebugTaskItems ("  ProguardConfigurationFiles:", ProguardConfigurationFiles);
			Log.LogDebugTaskItems ("  ExternalJavaLibraries:", ExternalJavaLibraries);
			Log.LogDebugTaskItems ("  DoNotPackageJavaLibraries:", DoNotPackageJavaLibraries);
			Log.LogDebugMessage ("  UseProguard: {0}", UseProguard);
			Log.LogDebugMessage ("  EnableLogging: {0}", EnableLogging);
			Log.LogDebugMessage ("  DumpOutput: {0}", DumpOutput);
			Log.LogDebugMessage ("  PrintSeedsOutput: {0}", PrintSeedsOutput);
			Log.LogDebugMessage ("  PrintMappingOutput: {0}", PrintMappingOutput);

			return base.Execute ();
		}

		protected override string GenerateCommandLineCommands ()
		{
			var cmd = new CommandLineBuilder ();

			if (!UseProguard) {
				// Add the JavaOptions if they are not null
				// These could be any of the additional options
				if (!string.IsNullOrEmpty (JavaOptions)) {
					cmd.AppendSwitch (JavaOptions);		
				}

				// Add the specific -XmxN to override the default heap size for the JVM
				// N can be in the form of Nm or NGB (e.g 100m or 1GB ) 
				cmd.AppendSwitchIfNotNull ("-Xmx", JavaMaximumHeapSize);

				cmd.AppendSwitchIfNotNull ("-jar ", Path.Combine (ProguardJarPath));
			}

			if (!ClassesOutputDirectory.EndsWith (Path.DirectorySeparatorChar.ToString ()))
				ClassesOutputDirectory += Path.DirectorySeparatorChar;
			var classesFullPath = Path.GetFullPath (ClassesOutputDirectory);

			if (File.Exists (ProguardJarInput))
				File.Delete (ProguardJarInput);
			using (var zip = ZipArchive.Open (ProguardJarInput, FileMode.Create)) {
				foreach (var file in Directory.GetFiles (classesFullPath, "*", SearchOption.AllDirectories))
					zip.AddFile (file, Path.Combine (Path.GetDirectoryName (file.Substring (classesFullPath.Length)), Path.GetFileName (file)));
			}

			var acwLines = File.ReadAllLines (AcwMapFile);
			using (var appcfg = File.CreateText (ProguardGeneratedApplicationConfiguration))
				for (int i = 0; i + 3 < acwLines.Length; i += 4)
					try {
						var java = acwLines [i + 3].Substring (acwLines [i + 3].IndexOf (';') + 1);
						appcfg.WriteLine ("-keep class " + java + " { *; }");
					} catch {
						// skip invalid lines
					}

			var injars = new List<string> ();
			var libjars = new List<string> ();
			injars.Add (ProguardJarInput);
			if (JavaLibrariesToEmbed != null)
				foreach (var jarfile in JavaLibrariesToEmbed)
					injars.Add (jarfile.ItemSpec);

			using (var xamcfg = File.Create (ProguardCommonXamarinConfiguration))
				GetType ().Assembly.GetManifestResourceStream ("proguard_xamarin.cfg").CopyTo (xamcfg);
			
			var configs = ProguardConfigurationFiles
				.Replace ("{sdk.dir}", Path.GetDirectoryName (Path.GetDirectoryName (ProguardHome)) + Path.DirectorySeparatorChar)
				.Replace ("{intermediate.common.xamarin}", ProguardCommonXamarinConfiguration)
				.Replace ("{intermediate.references}", ProguardGeneratedReferenceConfiguration)
				.Replace ("{intermediate.application}", ProguardGeneratedApplicationConfiguration)
				.Replace ("{project}", string.Empty) // current directory anyways.
				.Split (';')
				.Select (s => s.Trim ())
				.Where (s => !string.IsNullOrWhiteSpace (s));

			foreach (var file in configs) {
				if (File.Exists (file))
					cmd.AppendSwitchIfNotNull ("-include ", file);
				else
					Log.LogWarning ("Proguard configuration file '{0}' was not found.", file);
			}

			libjars.Add (JavaPlatformJarPath);
			if (ExternalJavaLibraries != null)
				foreach (var jarfile in ExternalJavaLibraries.Select (p => p.ItemSpec))
					libjars.Add (jarfile);

			cmd.AppendSwitch ("\"-injars");
			cmd.AppendSwitch (string.Join (Path.PathSeparator.ToString (), injars.Distinct ().Select (s => '\'' + s + '\''))+"\"");
			
			cmd.AppendSwitch ("\"-libraryjars");
			cmd.AppendSwitch (string.Join (Path.PathSeparator.ToString (), libjars.Distinct ().Select (s => '\'' + s + '\''))+"\"");
			
			cmd.AppendSwitch ("-outjars");
			cmd.AppendSwitch ('"' + ProguardJarOutput + '"');

			if (EnableLogging) {
				cmd.AppendSwitchIfNotNull ("-dump ", DumpOutput);
				cmd.AppendSwitchIfNotNull ("-printseeds ", PrintSeedsOutput);
				cmd.AppendSwitchIfNotNull ("-printusage ", PrintUsageOutput);
				cmd.AppendSwitchIfNotNull ("-printmapping ", PrintMappingOutput);
			}

			// http://stackoverflow.com/questions/5701126/compile-with-proguard-gives-exception-local-variable-type-mismatch#7587680
			cmd.AppendSwitch ("-optimizations !code/allocation/variable");
			
			return cmd.ToString ();
		}

		protected override string GenerateFullPathToTool ()
		{
			if (UseProguard)
				return Path.Combine (ToolPath, ToolExe);
			return Path.Combine (JavaToolPath, ToolName);
		}

		// Windows seems to need special care.
		protected override StringDictionary EnvironmentOverride {
			get {
				var sd = base.EnvironmentOverride ?? new StringDictionary ();
				if (OS.IsWindows) {
					if (!sd.ContainsKey ("PROGUARD_HOME"))
						sd.Add ("PROGUARD_HOME", ProguardHome);
				}
				var opts = sd.ContainsKey ("JAVA_TOOL_OPTIONS") ? sd ["JAVA_TOOL_OPTIONS"] : null;
				opts += " -Dfile.encoding=UTF8";
				sd ["JAVA_TOOL_OPTIONS"] = opts;
				return sd;
			}
		}

		string ProguardHome {
			get {
				return Path.GetDirectoryName (Path.GetDirectoryName (UseProguard ? ToolPath : ProguardJarPath));
			}
		}
	}
}

