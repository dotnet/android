// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System;
using System.Linq;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Text;
using System.Collections.Generic;
using Xamarin.AndroidTools;
using Xamarin.Android.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	public class Jack : JavaCompileToolTask
	{
		[Required]
		public string JackJarPath { get; set; }

		[Required]
		public string JavaPlatformJackPath { get; set; }

		[Required]
		public string [] InputJackFiles { get; set; }

		[Required]
		public string OutputDexDirectory { get; set; }

		public bool EnableProguard { get; set; }

		public bool EnableMultiDex { get; set; }

		// not to run it, but to get some config file path.
		public string ProguardJarPath { get; set; }

		public string AcwMapFile { get; set; }
		public string ProguardGeneratedApplicationConfiguration { get; set; }

		public string ProguardGeneratedReferenceConfiguration { get; set; }

		public string ProguardCommonXamarinConfiguration { get; set; }

		public string ProguardConfigurationFiles { get; set; }

		public ITaskItem[] CustomMainDexListFiles { get; set; }

		protected override string ToolName {
			get { return OS.IsWindows ? "java.exe" : "java"; }
		}

		public override void OnLogStarted ()
		{
			Log.LogDebugMessage ("  JackJarPath:", JackJarPath);
			Log.LogDebugMessage ("  JavaPlatformJackPath:", JavaPlatformJackPath);
			Log.LogDebugTaskItems ("  InputJackFiles:", InputJackFiles);
			Log.LogDebugMessage ("  OutputDexDirectory:", OutputDexDirectory);
			Log.LogDebugMessage ("  AcwMapFile: {0}", AcwMapFile);
			Log.LogDebugMessage ("  ProguardGeneratedApplicationConfiguration: {0}", ProguardGeneratedApplicationConfiguration);
			Log.LogDebugTaskItems ("  ProguardGeneratedReferenceConfiguration:", ProguardGeneratedReferenceConfiguration);
			Log.LogDebugTaskItems ("  ProguardCommonXamarinConfiguration:", ProguardCommonXamarinConfiguration);
			Log.LogDebugTaskItems ("  ProguardConfigurationFiles:", ProguardConfigurationFiles);
			Log.LogDebugTaskItems ("  CustomMainDexListFiles:", CustomMainDexListFiles);
		}

		public override bool Execute ()
		{
			if (!Directory.Exists (OutputDexDirectory))
				Directory.CreateDirectory (OutputDexDirectory);
			return base.Execute ();
		}

		protected override string GenerateCommandLineCommands ()
		{
			if (CustomMainDexListFiles != null && CustomMainDexListFiles.Any ())
				// FIXME: add error code.
				Log.LogWarning ("Jack compiler does not accept custom main dex list file. You would have to either disable Jack or remove MultiDexMainDexList items.");
			
			//   Running command: C:\Program Files (x86)\Java\jdk1.6.0_20\bin\java.exe
			//     "-jar" "C:\Program Files (x86)\Android\android-sdk-windows\platform-tools\jack.jar"
			//     "--output-dex" "bin"
			//     "-classpath" "C:\Users\Jonathan\Documents\Visual Studio 2010\Projects\AndroidMSBuildTest\AndroidMSBuildTest\obj\Debug\android\bin\mono.android.jar"
			//     "@C:\Users\Jonathan\AppData\Local\Temp\tmp79c4ac38.tmp"

			//var android_dir = MonoDroid.MonoDroidSdk.GetAndroidProfileDirectory (TargetFrameworkDirectory);

			var cmd = new CommandLineBuilder ();

			cmd.AppendSwitchIfNotNull ("-jar ", JackJarPath);

			//cmd.AppendSwitchIfNotNull ("-J-Dfile.encoding=", "UTF8");

			cmd.AppendSwitchIfNotNull ("--output-dex ", OutputDexDirectory);

			cmd.AppendSwitchIfNotNull ("-cp ", JavaPlatformJackPath);
			foreach (var jack in InputJackFiles)
				cmd.AppendSwitchIfNotNull ("--import ", jack);
			//cmd.AppendSwitchIfNotNull ("-bootclasspath ", JavaPlatformJarPath);
			//cmd.AppendSwitchIfNotNull ("-encoding ", "UTF-8");
			cmd.AppendFileNameIfNotNull (string.Format ("@{0}", TemporarySourceListFile));

			if (EnableMultiDex)
				cmd.AppendSwitchIfNotNull ("--multi-dex ", "NATIVE");
			// Proguard settings
			if (EnableProguard) {
				var acwLines = File.ReadAllLines (AcwMapFile);
				using (var appcfg = File.CreateText (ProguardGeneratedApplicationConfiguration))
					for (int i = 0; i + 3 < acwLines.Length; i += 4)
						try {
						var java = acwLines [i + 3].Substring (acwLines [i + 3].IndexOf (';') + 1);
						appcfg.WriteLine ("-keep class " + java + " { *; }");
					} catch {
					// skip invalid lines
				}
				var configs = ProguardConfigurationFiles
					.Replace ("{sdk.dir}", Path.GetDirectoryName (Path.GetDirectoryName (ProguardJarPath)) + Path.DirectorySeparatorChar)
					.Replace ("{intermediate.common.xamarin}", ProguardCommonXamarinConfiguration)
					.Replace ("{intermediate.references}", ProguardGeneratedReferenceConfiguration)
					.Replace ("{intermediate.application}", ProguardGeneratedApplicationConfiguration)
					.Replace ("{project}", string.Empty) // current directory anyways.
					.Split (';')
					.Select (s => s.Trim ())
					.Where (s => !string.IsNullOrWhiteSpace (s));

				foreach (var file in configs) {
					if (File.Exists (file))
						cmd.AppendSwitchIfNotNull ("--config-proguard ", file);
					else
						Log.LogWarning ("Proguard configuration file '{0}' was not found.", file);
				}
			}

			return cmd.ToString ();
		}
	}
	
}
