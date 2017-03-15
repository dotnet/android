// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System;
using System.Linq;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Text;
using System.Collections.Generic;
using Xamarin.Android.Build.Utilities;
using Xamarin.Android.Tools.Aidl;

namespace Xamarin.Android.Tasks
{

	public class Jill : JavaToolTask
	{
		[Required]
		public string JillJarPath { get; set; }

		[Required]
		public string OutputJackDirectory { get; set; }

		public string OutputFileName { get; set; }

		public string [] Jars { get; set; }

		[Output]
		public string [] OutputJackFiles { get; set; }

		[Output]
		public string OutputPlatformJackPath { get; set; }

		protected override string ToolName {
			get { return OS.IsWindows ? "java.exe" : "java"; }
		}

		public override bool Execute ()
		{
			Log.LogDebugMessage ("Jill Task");
			Log.LogDebugMessage ("  JillJarPath: ", JillJarPath);
			Log.LogDebugMessage ("  OutputJackDirectory: ", OutputJackDirectory);
			Log.LogDebugTaskItems ("  Jars: ", Jars);

			if (!Directory.Exists (OutputJackDirectory))
				Directory.CreateDirectory (OutputJackDirectory);

			bool retval = true;
			var outputJackFiles = new List<string> ();
			var md5 = System.Security.Cryptography.MD5.Create ();
			foreach (var jar in Jars) {
				if (File.Exists (Path.Combine (Path.GetDirectoryName (jar), "xamarin_cache.jack")))
					continue;
				context_jar = jar;
				context_jack = Path.Combine (OutputJackDirectory, OutputFileName ?? BitConverter.ToString (md5.ComputeHash (Encoding.UTF8.GetBytes (jar))) + Path.GetFileNameWithoutExtension (context_jar) + ".jack");
				retval &= base.Execute ();
				outputJackFiles.Add (context_jack);
			}
			OutputJackFiles = outputJackFiles.ToArray ();

			Log.LogDebugTaskItems ("  OutputJackFiles: ", OutputJackFiles);
			return retval;
		}

		string context_jar;
		string context_jack;

		protected override string GenerateCommandLineCommands ()
		{
			//   Running command: C:\Program Files (x86)\Java\jdk1.6.0_20\bin\java.exe
			//     "-jar" "C:\Program Files (x86)\Android\android-sdk-windows\platform-tools\jill.jar"
			//     "--output-dex" "bin\classes"
			//     "-classpath" "C:\Users\Jonathan\Documents\Visual Studio 2010\Projects\AndroidMSBuildTest\AndroidMSBuildTest\obj\Debug\android\bin\mono.android.jar"
			//     "@C:\Users\Jonathan\AppData\Local\Temp\tmp79c4ac38.tmp"

			//var android_dir = MonoDroid.MonoDroidSdk.GetAndroidProfileDirectory (TargetFrameworkDirectory);

			var cmd = new CommandLineBuilder ();

			cmd.AppendSwitchIfNotNull ("-jar ", JillJarPath);

			//cmd.AppendSwitchIfNotNull ("-J-Dfile.encoding=", "UTF8");

			cmd.AppendSwitchIfNotNull ("--output ", context_jack);
			cmd.AppendFileNameIfNotNull (context_jar);
			cmd.AppendSwitch ("--verbose");

			return cmd.ToString ();
		}
	}
	
}
