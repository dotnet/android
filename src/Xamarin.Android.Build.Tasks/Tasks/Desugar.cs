// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

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

	public class Desugar : JavaToolTask
	{
		[Required]
		public string DesugarJarPath { get; set; }

		[Required]
		public string JavaPlatformJarPath { get; set; }

		public string DesugarExtraArguments { get; set; }

		[Required]
		public string ManifestFile { get; set; }

		[Required]
		public string OutputDirectory { get; set; }


		public string [] InputJars { get; set; }

		[Output]
		public string [] OutputJars { get; set; }

		public override bool Execute ()
		{
			if (!Directory.Exists (OutputDirectory))
				Directory.CreateDirectory (OutputDirectory);

			return base.Execute ();
		}

		protected override string GenerateCommandLineCommands ()
		{
			//   Running command: C:\Program Files (x86)\Java\jdk1.6.0_20\bin\java.exe
			//     "-jar" "C:\Program Files (x86)\Android\android-sdk-windows\platform-tools\jill.jar"
			//     "--output-dex" "bin\classes"
			//     "-classpath" "C:\Users\Jonathan\Documents\Visual Studio 2010\Projects\AndroidMSBuildTest\AndroidMSBuildTest\obj\Debug\android\bin\mono.android.jar"
			//     "@C:\Users\Jonathan\AppData\Local\Temp\tmp79c4ac38.tmp"

			//var android_dir = MonoDroid.MonoDroidSdk.GetAndroidProfileDirectory (TargetFrameworkDirectory);

			var doc = AndroidAppManifest.Load (ManifestFile, MonoAndroidHelper.SupportedVersions);
			int minApiVersion = doc.MinSdkVersion == null ? 4 : (int)doc.MinSdkVersion;

			var cmd = new CommandLineBuilder ();

			// Add the JavaOptions if they are not null
			// These could be any of the additional options
			if (!string.IsNullOrEmpty (JavaOptions)) {
				cmd.AppendSwitch (JavaOptions);
			}

			// Add the specific -XmxN to override the default heap size for the JVM
			// N can be in the form of Nm or NGB (e.g 100m or 1GB ) 
			cmd.AppendSwitchIfNotNull ("-Xmx", JavaMaximumHeapSize);

			cmd.AppendSwitchIfNotNull ("-jar ", DesugarJarPath);

			cmd.AppendSwitch ("--bootclasspath_entry ");
			cmd.AppendFileNameIfNotNull (JavaPlatformJarPath);

			cmd.AppendSwitch ("--min_sdk_version ");
			cmd.AppendSwitch (minApiVersion.ToString ());
			
			if (minApiVersion < 24) {
				cmd.AppendSwitch("--desugar_try_with_resources_omit_runtime_classes ");
			}

			//cmd.AppendSwitchIfNotNull ("-J-Dfile.encoding=", "UTF8");

			if (!string.IsNullOrEmpty (DesugarExtraArguments))
				cmd.AppendSwitch (DesugarExtraArguments); // it should contain "--dex".

			var outputs = new List<string> ();
			var md5 = System.Security.Cryptography.MD5.Create ();
			foreach (var jar in InputJars) {
				var output = Path.Combine (OutputDirectory, BitConverter.ToString (md5.ComputeHash (Encoding.UTF8.GetBytes (jar))) + Path.GetFileName (jar));
				outputs.Add (output);
				cmd.AppendSwitch ("--classpath_entry ");
				cmd.AppendFileNameIfNotNull (jar);
				cmd.AppendSwitch ("--input ");
				cmd.AppendFileNameIfNotNull (jar);
				cmd.AppendSwitch ("--output ");
				cmd.AppendFileNameIfNotNull (output);
			}

			OutputJars = outputs.ToArray ();

			return cmd.ToString ();
		}
	}

}
