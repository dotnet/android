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

		public string ProguardGeneratedReferenceConfiguration { get; set; }
		public string ProguardGeneratedApplicationConfiguration { get; set; }
		public string ProguardCommonXamarinConfiguration { get; set; }

		[Required]
		public string ProguardConfigurationFiles { get; set; }

		public ITaskItem[] JavaLibrariesToEmbed { get; set; }
		
		public ITaskItem[] JavaLibrariesToReference { get; set; }
		
		public bool UseProguard { get; set; }

		public string JavaOptions { get; set; }

		public string JavaMaximumHeapSize { get; set; }

		public bool EnableLogging { get; set; }
		public string DumpOutput { get; set; }
		public string PrintSeedsOutput { get; set; }
		public string PrintUsageOutput { get; set; }
		public string PrintMappingOutput { get; set; }
		public string ProguardInputJarFilter { get; set; }

		protected override string ToolName {
			get {
				if (UseProguard)
					return OS.IsWindows ? "proguard.bat" : "proguard";
				return OS.IsWindows ? "java.exe" : "java";
			}
		}

		public override bool Execute ()
		{
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

			var classesZip = Path.Combine (ClassesOutputDirectory, "..", "classes.zip");
			var acwLines = File.ReadAllLines (AcwMapFile);
			using (var appcfg = File.CreateText (ProguardGeneratedApplicationConfiguration))
				for (int i = 0; i + 2 < acwLines.Length; i += 3)
					try {
						var line = acwLines [i + 2];
						var java = line.Substring (line.IndexOf (';') + 1);
						appcfg.WriteLine ("-keep class " + java + " { *; }");
					} catch {
						// skip invalid lines
					}

			if (!string.IsNullOrWhiteSpace (ProguardCommonXamarinConfiguration))
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
					Log.LogCodedWarning ("XA4304", file, 0, "Proguard configuration file '{0}' was not found.", file);
			}

			var injars = new List<string> ();
			var libjars = new List<string> ();
			injars.Add (classesZip);
			if (JavaLibrariesToEmbed != null)
				foreach (var jarfile in JavaLibrariesToEmbed)
					injars.Add (jarfile.ItemSpec);
			libjars.Add (JavaPlatformJarPath);
			if (JavaLibrariesToReference != null)
				foreach (var jarfile in JavaLibrariesToReference.Select (p => p.ItemSpec))
					libjars.Add (jarfile);

			cmd.AppendSwitchUnquotedIfNotNull ("-injars ", "\"'" + string.Join ($"'{ProguardInputJarFilter}{Path.PathSeparator}'", injars.Distinct ()) + $"'{ProguardInputJarFilter}\"");
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

