// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System;
using System.Linq;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Text;
using System.Collections.Generic;
using Xamarin.Tools.Zip;

namespace Xamarin.Android.Tasks
{
	public class Javac : JavaCompileToolTask
	{
		[Required]
		public string ClassesOutputDirectory { get; set; }

		public string JavaPlatformJarPath { get; set; }

		public string JavacTargetVersion { get; set; }
		public string JavacSourceVersion { get; set; }

		public override void OnLogStarted ()
		{
			Log.LogDebugMessage ("  ClassesOutputDirectory: {0}", ClassesOutputDirectory);
			Log.LogDebugMessage ("  JavacTargetVersion: {0}", JavacTargetVersion);
			Log.LogDebugMessage ("  JavacSourceVersion: {0}", JavacSourceVersion);
		}

		public override bool Execute ()
		{
			if (!Directory.Exists (ClassesOutputDirectory))
				Directory.CreateDirectory (ClassesOutputDirectory);
			var result = base.Execute ();
			if (!result)
				return result;
			// compress all the class files
			using (var zip = new ZipArchiveEx (Path.Combine (ClassesOutputDirectory, "..", "classes.zip"), FileMode.OpenOrCreate))
				zip.AddDirectory (ClassesOutputDirectory, "", CompressionMethod.Store);
			return result;
		}

		protected override string GenerateCommandLineCommands ()
		{
			//   Running command: C:\Program Files (x86)\Java\jdk1.6.0_20\bin\javac.exe
			//     "-J-Dfile.encoding=UTF8"
			//     "-d" "bin\classes"
			//     "-classpath" "C:\Users\Jonathan\Documents\Visual Studio 2010\Projects\AndroidMSBuildTest\AndroidMSBuildTest\obj\Debug\android\bin\mono.android.jar"
			//     "-bootclasspath" "C:\Program Files (x86)\Android\android-sdk-windows\platforms\android-8\android.jar"
			//     "-encoding" "UTF-8"
			//     "@C:\Users\Jonathan\AppData\Local\Temp\tmp79c4ac38.tmp"

			//var android_dir = MonoDroid.MonoDroidSdk.GetAndroidProfileDirectory (TargetFrameworkDirectory);

			var cmd = new CommandLineBuilder ();

			cmd.AppendSwitchIfNotNull ("-J-Dfile.encoding=", "UTF8");

			cmd.AppendSwitchIfNotNull ("-d ", ClassesOutputDirectory);

			cmd.AppendSwitchIfNotNull ("-classpath ", Jars == null || !Jars.Any () ? null : string.Join (Path.PathSeparator.ToString (), Jars.Select (i => i.ItemSpec)));
			cmd.AppendSwitchIfNotNull ("-bootclasspath ", JavaPlatformJarPath);
			cmd.AppendSwitchIfNotNull ("-encoding ", "UTF-8");
			cmd.AppendFileNameIfNotNull (string.Format ("@{0}", TemporarySourceListFile));
			cmd.AppendSwitchIfNotNull ("-target ", JavacTargetVersion);
			cmd.AppendSwitchIfNotNull ("-source ", JavacSourceVersion);

			return cmd.ToString ();
		}
	}
}
