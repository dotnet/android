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
using Xamarin.Android.Tools;
using System.IO.Compression;
using Xamarin.Tools.Zip;

namespace Xamarin.Android.Tasks
{
	// See AOSP/sdk/eclipse/plugins/com.android.ide.eclipse.adt/src/com/android/ide/eclipse/adt/internal/build/BuildHelper.java
	public class Proguard : ToolTask
	{
		public string ProguardJarPath { get; set; }

		public string ProguardToolPath { get; set; }

		public string JavaToolPath { get; set; }

		[Required]
		public string JavaPlatformJarPath { get; set; }

		[Required]
		public string AndroidSdkDirectory { get; set; }

		[Required]
		public string ClassesOutputDirectory { get; set; }

		[Required]
		public string AcwMapFile { get; set; }

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
			Log.LogDebugMessage ("  AndroidSdkDirectory: {0}", AndroidSdkDirectory);
			Log.LogDebugMessage ("  JavaPlatformJarPath: {0}", JavaPlatformJarPath);
			Log.LogDebugMessage ("  ClassesOutputDirectory: {0}", ClassesOutputDirectory);
			Log.LogDebugMessage ("  AcwMapFile: {0}", AcwMapFile);
			Log.LogDebugMessage ("  ProguardGeneratedApplicationConfiguration: {0}", ProguardGeneratedApplicationConfiguration);
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

			EnvironmentVariables = MonoAndroidHelper.GetProguardEnvironmentVaribles (ProguardHome);

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

			if (!ClassesOutputDirectory.EndsWith (Path.DirectorySeparatorChar.ToString (), StringComparison.OrdinalIgnoreCase))
				ClassesOutputDirectory += Path.DirectorySeparatorChar;

			var classesZip = Path.Combine (ClassesOutputDirectory, "classes.zip");
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
			injars.Add (classesZip);
			if (JavaLibrariesToEmbed != null)
				foreach (var jarfile in JavaLibrariesToEmbed)
					injars.Add (jarfile.ItemSpec);

			using (var xamcfg = File.Create (ProguardCommonXamarinConfiguration))
				GetType ().Assembly.GetManifestResourceStream ("proguard_xamarin.cfg").CopyTo (xamcfg);
			
			var configs = ProguardConfigurationFiles
				.Replace ("{sdk.dir}", AndroidSdkDirectory + Path.DirectorySeparatorChar)
				.Replace ("{intermediate.common.xamarin}", ProguardCommonXamarinConfiguration)
				.Replace ("{intermediate.references}", ProguardGeneratedReferenceConfiguration)
				.Replace ("{intermediate.application}", ProguardGeneratedApplicationConfiguration)
				.Replace ("{project}", string.Empty) // current directory anyways.
				.Split (';')
				.Select (s => s.Trim ())
				.Where (s => !string.IsNullOrWhiteSpace (s));

			var enclosingChar = OS.IsWindows ? "\"" : string.Empty;

			foreach (var file in configs) {
				if (File.Exists (file))
					cmd.AppendSwitchUnquotedIfNotNull ("-include ", $"{enclosingChar}'{file}'{enclosingChar}");
				else
					Log.LogWarning ("Proguard configuration file '{0}' was not found.", file);
			}

			libjars.Add (JavaPlatformJarPath);
			if (ExternalJavaLibraries != null)
				foreach (var jarfile in ExternalJavaLibraries.Select (p => p.ItemSpec))
					libjars.Add (jarfile);

			cmd.AppendSwitchUnquotedIfNotNull ("-injars ", $"{enclosingChar}'" + string.Join ($"'{Path.PathSeparator}'", injars.Distinct ()) + $"'{enclosingChar}");

			cmd.AppendSwitchUnquotedIfNotNull ("-libraryjars ", $"{enclosingChar}'" + string.Join ($"'{Path.PathSeparator}'", libjars.Distinct ()) + $"'{enclosingChar}");
			
			cmd.AppendSwitchIfNotNull ("-outjars ", ProguardJarOutput);

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
				return Path.Combine (ProguardToolPath, "bin", ToolExe);
			return Path.Combine (JavaToolPath, ToolName);
		}

		string ProguardHome {
			get { return UseProguard ? ProguardToolPath : Path.GetDirectoryName (Path.GetDirectoryName (ProguardJarPath)); }
		}
	}
}

