// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System;
using System.Linq;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Text;
using System.Collections.Generic;
using Xamarin.Tools.Zip;
using Xamarin.Android.Tools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class Javac : JavaCompileToolTask
	{
		public override string TaskPrefix => "JVC";

		[Required]
		public string ClassesOutputDirectory { get; set; }

		public string ClassesZip { get; set; }

		public string JavaPlatformJarPath { get; set; }

		public string JavacTargetVersion { get; set; }
		public string JavacSourceVersion { get; set; }

		public string JdkVersion { get; set; }

		public override string DefaultErrorCode => "JAVAC0000";

		public override bool RunTask ()
		{
			if (!Directory.Exists (ClassesOutputDirectory))
				Directory.CreateDirectory (ClassesOutputDirectory);
			var result = base.RunTask ();
			if (!result)
				return result;
			// compress all the class files
			if (!string.IsNullOrEmpty (ClassesZip)) {
				using (var zip = new ZipArchiveEx (ClassesZip, FileMode.OpenOrCreate)) {
					zip.AutoFlush = false;
					zip.AddDirectory (ClassesOutputDirectory, "", CompressionMethod.Store);
				}
			}
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

			var cmd = new CommandLineBuilder ();

			cmd.AppendSwitchIfNotNull ("-J-Dfile.encoding=", "UTF8");

			cmd.AppendFileNameIfNotNull (string.Format ("@{0}", TemporarySourceListFile));

			if (int.TryParse (JavacSourceVersion, out int sourceVersion) &&
					int.TryParse (JavacTargetVersion, out int targetVersion) &&
					JavacSupportsRelease ()) {
				cmd.AppendSwitchIfNotNull ("--release ", Math.Max (sourceVersion, targetVersion).ToString ());
			} else {
				cmd.AppendSwitchIfNotNull ("-target ", JavacTargetVersion);
				cmd.AppendSwitchIfNotNull ("-source ", JavacSourceVersion);
			}

			return cmd.ToString ();
		}

		bool JavacSupportsRelease ()
		{
			if (string.IsNullOrEmpty (JdkVersion)) {
				return false;
			}
			var jdkVersion  = Version.Parse (JdkVersion);
			return jdkVersion.Major >= 17;
		}

		protected override void WriteOptionsToResponseFile (StreamWriter sw)
		{
			sw.WriteLine ($"-d \"{ClassesOutputDirectory.Replace (@"\", @"\\")}\"");
			sw.WriteLine ("-classpath \"{0}\"", Jars == null || !Jars.Any () ? null : string.Join (Path.PathSeparator.ToString (), Jars.Select (i => i.ItemSpec.Replace (@"\", @"\\"))));
			sw.WriteLine ("-bootclasspath \"{0}\"", JavaPlatformJarPath.Replace (@"\", @"\\"));
			sw.WriteLine ($"-encoding UTF8");
		}
	}
}
