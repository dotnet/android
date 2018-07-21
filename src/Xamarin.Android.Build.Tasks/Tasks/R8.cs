// Copyright (C) 2018 Xamarin, Inc. All rights reserved.

using System;
using System.Linq;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Text;
using System.Collections.Generic;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{

	public class R8 : JavaToolTask
	{
		[Required]
		public string R8JarPath { get; set; }

		[Required]
		public string OutputDirectory { get; set; }

		public string Configuration { get; set; }

		// It is loaded to calculate --min-api, which is used by desugaring part to determine which levels of desugaring it performs.
		[Required]
		public string AndroidManifestFile { get; set; }

		// general r8 feature options.
		public bool EnableDesugar { get; set; }
		public bool EnableMinify { get; set; } // The Task has the option, but it is not supported at all.
		public bool EnableTreeShaking { get; set; }

		// Java libraries to embed or reference
		[Required]
		public string ClassesZip { get; set; }
		[Required]
		public string JavaPlatformJarPath { get; set; }
		public ITaskItem [] JavaLibrariesToEmbed { get; set; }
		public ITaskItem [] JavaLibrariesToReference { get; set; }

		// used for proguard configuration settings
		[Required]
		public string AndroidSdkDirectory { get; set; }
		[Required]
		public string AcwMapFile { get; set; }
		public string ProguardGeneratedReferenceConfiguration { get; set; }
		public string ProguardGeneratedApplicationConfiguration { get; set; }
		public string ProguardCommonXamarinConfiguration { get; set; }
		[Required]
		public string ProguardConfigurationFiles { get; set; }
		public string ProguardMappingOutput { get; set; }

		// multidex
		public bool EnableMultiDex { get; set; }
		public string MultiDexMainDexListFile { get; set; }

		public string R8ExtraArguments { get; set; }

		public override bool Execute ()
		{
			Log.LogDebugMessage ("R8 Task");
			Log.LogDebugTaskItems ("  R8JarPath: ", R8JarPath);
			Log.LogDebugTaskItems ("  OutputDirectory: ", OutputDirectory);
			Log.LogDebugTaskItems ("  AndroidManifestFile: ", AndroidManifestFile);
			Log.LogDebugMessage ("  Configuration: {0}", Configuration);
			Log.LogDebugTaskItems ("  JavaPlatformJarPath: ", JavaPlatformJarPath);
			Log.LogDebugTaskItems ("  ClassesZip: ", ClassesZip);
			Log.LogDebugTaskItems ("  JavaLibrariesToEmbed: ", JavaLibrariesToEmbed);
			Log.LogDebugTaskItems ("  JavaLibrariesToReference: ", JavaLibrariesToReference);
			Log.LogDebugMessage ("  EnableDesugar: {0}", EnableDesugar);
			Log.LogDebugMessage ("  EnableTreeShaking: {0}", EnableTreeShaking);
			Log.LogDebugTaskItems ("  AndroidSdkDirectory:", AndroidSdkDirectory);
			Log.LogDebugTaskItems ("  AcwMapFile: ", AcwMapFile);
			Log.LogDebugTaskItems ("  ProguardGeneratedReferenceConfiguration:", ProguardGeneratedReferenceConfiguration);
			Log.LogDebugTaskItems ("  ProguardGeneratedApplicationConfiguration:", ProguardGeneratedApplicationConfiguration);
			Log.LogDebugTaskItems ("  ProguardCommonXamarinConfiguration:", ProguardCommonXamarinConfiguration);
			Log.LogDebugTaskItems ("  ProguardConfigurationFiles:", ProguardConfigurationFiles);
			Log.LogDebugTaskItems ("  ProguardMappingOutput:", ProguardMappingOutput);
			Log.LogDebugMessage ("  EnableMultiDex: {0}", EnableMultiDex);
			Log.LogDebugTaskItems ("  MultiDexMainDexListFile: ", MultiDexMainDexListFile);
			Log.LogDebugTaskItems ("  R8ExtraArguments: ", R8ExtraArguments);

			return base.Execute ();
		}

		protected override string GenerateCommandLineCommands ()
		{
			var cmd = new CommandLineBuilder ();

			cmd.AppendSwitchIfNotNull ("-jar ", R8JarPath);

			if (!string.IsNullOrEmpty (R8ExtraArguments))
				cmd.AppendSwitch (R8ExtraArguments); // it should contain "--dex".
			if (Configuration.Equals ("Debug", StringComparison.OrdinalIgnoreCase))
				cmd.AppendSwitch ("--debug");

			// generating proguard application configuration
			if (EnableTreeShaking) {
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
				var enclosingChar = "\"";
				foreach (var file in configs) {
					if (File.Exists (file))
						cmd.AppendSwitchIfNotNull ("--pg-conf ", file);
					else
						Log.LogWarning ("Proguard configuration file '{0}' was not found.", file);
				}
				cmd.AppendSwitchIfNotNull ("--pg-map-output ", ProguardMappingOutput);

				// multidexing
				if (EnableMultiDex) {
					if (!string.IsNullOrWhiteSpace (MultiDexMainDexListFile) && File.Exists (MultiDexMainDexListFile))
						cmd.AppendSwitchIfNotNull ("--main-dex-list ", MultiDexMainDexListFile);
					else
						Log.LogWarning ($"MultiDex is enabled, but main dex list file '{MultiDexMainDexListFile}' does not exist.");
				}
			}

			// desugaring
			var doc = AndroidAppManifest.Load (AndroidManifestFile, MonoAndroidHelper.SupportedVersions);
			int minApiVersion = doc.MinSdkVersion == null ? 4 : (int)doc.MinSdkVersion;
			cmd.AppendSwitchIfNotNull ("--min-api ", minApiVersion.ToString ());

			if (!EnableTreeShaking)
				cmd.AppendSwitch ("--no-tree-shaking");
			if (!EnableDesugar)
				cmd.AppendSwitch ("--no-desugaring");
			if (!EnableMinify)
				cmd.AppendSwitch ("--no-minification");

			var injars = new List<string> ();
			var libjars = new List<string> ();
			injars.Add (ClassesZip);
			if (JavaLibrariesToEmbed != null)
				foreach (var jarfile in JavaLibrariesToEmbed)
					injars.Add (jarfile.ItemSpec);
			libjars.Add (JavaPlatformJarPath);
			if (JavaLibrariesToReference != null)
				foreach (var jarfile in JavaLibrariesToReference.Select (p => p.ItemSpec))
					libjars.Add (jarfile);

			cmd.AppendSwitchIfNotNull ("--output ", OutputDirectory);
			foreach (var jar in libjars)
				cmd.AppendSwitchIfNotNull ("--lib ", jar);
			foreach (var jar in injars)
				cmd.AppendFileNameIfNotNull (jar);

			return cmd.ToString ();
		}
	}
	
}
