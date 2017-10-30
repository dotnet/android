// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System;
using System.Linq;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Text;
using System.Collections.Generic;
using Xamarin.Android.Tools;
using Xamarin.Android.Tools.Aidl;

namespace Xamarin.Android.Tasks
{

	public class Dx : JavaToolTask
	{
		[Required]
		public string DxJarPath { get; set; }

		public string DxExtraArguments { get; set; }

		[Required]
		public string OutputDexDirectory { get; set; }

		public string [] Jars { get; set; }

		[Output]
		public string [] OutputDexFiles { get; set; }

		[Output]
		public string OutputPlatformDexPath { get; set; }

		protected override string ToolName {
			get { return OS.IsWindows ? "java.exe" : "java"; }
		}

		public override bool Execute ()
		{
			Log.LogDebugMessage ("Dx Task");
			Log.LogDebugMessage ("  DxJarPath: ", DxJarPath);
			Log.LogDebugMessage ("  OutputJackDirectory: ", OutputDexDirectory);
			Log.LogDebugTaskItems ("  Jars: ", Jars);

			if (!Directory.Exists (OutputDexDirectory))
				Directory.CreateDirectory (OutputDexDirectory);

			bool retval = true;
			var outputJackFiles = new List<string> ();
			var md5 = System.Security.Cryptography.MD5.Create ();
			foreach (var jar in Jars) {
				context_jar = jar;
				context_dex = Path.Combine (OutputDexDirectory, BitConverter.ToString (md5.ComputeHash (Encoding.UTF8.GetBytes (jar))) + Path.GetFileNameWithoutExtension (context_jar) + ".dex");
				retval &= base.Execute ();
				outputJackFiles.Add (context_dex);
			}
			OutputDexFiles = outputJackFiles.ToArray ();

			Log.LogDebugTaskItems ("  OutputJackFiles: ", OutputDexFiles);
			return retval;
		}

		string context_jar;
		string context_dex;

		protected override string GenerateCommandLineCommands ()
		{
			//   Running command: C:\Program Files (x86)\Java\jdk1.6.0_20\bin\java.exe
			//     "-jar" "C:\Program Files (x86)\Android\android-sdk-windows\platform-tools\jill.jar"
			//     "--output-dex" "bin\classes"
			//     "-classpath" "C:\Users\Jonathan\Documents\Visual Studio 2010\Projects\AndroidMSBuildTest\AndroidMSBuildTest\obj\Debug\android\bin\mono.android.jar"
			//     "@C:\Users\Jonathan\AppData\Local\Temp\tmp79c4ac38.tmp"

			//var android_dir = MonoDroid.MonoDroidSdk.GetAndroidProfileDirectory (TargetFrameworkDirectory);

			var cmd = new CommandLineBuilder ();

			cmd.AppendSwitchIfNotNull ("-jar ", DxJarPath);

			//cmd.AppendSwitchIfNotNull ("-J-Dfile.encoding=", "UTF8");

			if (!string.IsNullOrEmpty (DxExtraArguments))
				cmd.AppendSwitch (DxExtraArguments); // it should contain "--dex".
			cmd.AppendSwitch ("--verbose");

			cmd.AppendSwitchIfNotNull ("--output=", context_dex);
			cmd.AppendFileNameIfNotNull (context_jar);

			return cmd.ToString ();
		}
	}
	
}
