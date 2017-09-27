// Copyright (C) 2012 Xamarin, Inc. All rights reserved.

using System;
using System.Linq;
using System.IO;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	public class JarToXml : ToolTask
	{
		[Required]
		public string AndroidSdkDirectory { get; set; }
		
		[Required]
		public string MonoAndroidToolsDirectory { get; set; }
		
		[Required]
		public string JavaSdkDirectory { get; set; }

		[Required]
		public string AndroidApiLevel { get; set; }

		[Required]
		public string OutputFile { get; set; }

		[Required]
		public ITaskItem[] SourceJars { get; set; }

		public ITaskItem[] ReferenceJars { get; set; }
		public string DroidDocPaths { get; set; }
		public string JavaDocPaths { get; set; }
		public string Java7DocPaths { get; set; }
		public string Java8DocPaths { get; set; }
		public ITaskItem[] JavaDocs { get; set; }

		public ITaskItem[] LibraryProjectJars { get; set; }

		public string JavaOptions { get; set; }

		public string JavaMaximumHeapSize { get; set; }

		public override bool Execute ()
		{
			Log.LogDebugMessage ("JarToXml Task");
			Log.LogDebugMessage ("  JavaOptions: {0}", JavaOptions);
			Log.LogDebugMessage ("  JavaMaximumHeapSize: {0}", JavaMaximumHeapSize);
			Log.LogDebugMessage ("  AndroidSdkDirectory: {0}", AndroidSdkDirectory);
			Log.LogDebugMessage ("  AndroidApiLevel: {0}", AndroidApiLevel);
			Log.LogDebugMessage ("  MonoAndroidToolsDirectory: {0}", MonoAndroidToolsDirectory);
			Log.LogDebugMessage ("  JavaSdkDirectory: {0}", JavaSdkDirectory);
			Log.LogDebugMessage ("  OutputFile: {0}", OutputFile);
			Log.LogDebugMessage ("  DroidDocPaths: {0}", DroidDocPaths);
			Log.LogDebugMessage ("  JavaDocPaths: {0}", JavaDocPaths);
			Log.LogDebugMessage ("  Java7DocPaths: {0}", Java7DocPaths);
			Log.LogDebugMessage ("  Java8DocPaths: {0}", Java8DocPaths);
			Log.LogDebugTaskItems ("  JavaDocs: {0}", JavaDocs);
			Log.LogDebugTaskItems ("  LibraryProjectJars:", LibraryProjectJars);
			Log.LogDebugTaskItems ("  SourceJars:", SourceJars);
			Log.LogDebugTaskItems ("  ReferenceJars:", ReferenceJars);

			if (SourceJars == null || SourceJars.Count () == 0) {
				Log.LogError ("At least one Java library is required for binding, this must be either 'EmbeddedJar', 'InputJar' (for jar), 'LibraryProjectZip' (for aar or zip) or 'LibraryProjectProperties' (project.properties) build action.");
				return false;
			}

			// Ensure that the user has the platform they are targeting installed
			var jarpath = Path.Combine (MonoAndroidHelper.AndroidSdk.TryGetPlatformDirectoryFromApiLevel (AndroidApiLevel, MonoAndroidHelper.SupportedVersions), "android.jar");

			if (!File.Exists (jarpath)) {
				Log.LogError ("Could not find android.jar for API Level {0}.  This means the Android SDK platform for API Level {0} is not installed.  Either install it in the Android SDK Manager, or change your Android Bindings project to target an API version that is installed. ({1} missing.)", AndroidApiLevel, jarpath);
				return false;
			}

			// Ensure that all requested jars exist
			foreach (var jar in SourceJars)
				if (!File.Exists (jar.ItemSpec))
					Log.LogError ("Specified source jar not found: {0}", jar.ItemSpec);

			if (ReferenceJars != null)
				foreach (var jar in ReferenceJars)
					if (!File.Exists (jar.ItemSpec))
						Log.LogError ("Specified reference jar not found: {0}", jar.ItemSpec);

			if (Log.HasLoggedErrors)
				return false;

			// Ensure our output directory exists
			Directory.CreateDirectory (Path.GetDirectoryName (OutputFile));

			return base.Execute ();
		}

		protected override string GenerateCommandLineCommands ()
		{
			var cmd = new CommandLineBuilder ();

			// Add the JavaOptions if they are not null
			// These could be any of the additional options
			if (!string.IsNullOrEmpty (JavaOptions)) {
				cmd.AppendSwitch (JavaOptions);		
			}

			// Add the specific -XmxN to override the default heap size for the JVM
			// N can be in the form of Nm or NGB (e.g 100m or 1GB ) 
			cmd.AppendSwitchIfNotNull("-Xmx", JavaMaximumHeapSize);

			// See https://bugzilla.xamarin.com/show_bug.cgi?id=21096
			cmd.AppendSwitch ("-XX:-UseSplitVerifier");

			// Arguments sent to java.exe
			cmd.AppendSwitchIfNotNull ("-jar ", Path.Combine (MonoAndroidToolsDirectory, "jar2xml.jar"));

			foreach (var jar in SourceJars)
				cmd.AppendSwitchIfNotNull ("--jar=", Path.GetFullPath (jar.ItemSpec));

			var libraryProjectJars  = MonoAndroidHelper.ExpandFiles (LibraryProjectJars);

			foreach (var jar in libraryProjectJars) {
				if (MonoAndroidHelper.IsEmbeddedReferenceJar (jar))
					cmd.AppendSwitchIfNotNull ("--ref=", Path.GetFullPath (jar));
				else
					cmd.AppendSwitchIfNotNull ("--jar=", Path.GetFullPath (jar));
			}

			// Arguments sent to jar2xml
			var jarpath = Path.Combine (MonoAndroidHelper.AndroidSdk.TryGetPlatformDirectoryFromApiLevel (AndroidApiLevel, MonoAndroidHelper.SupportedVersions), "android.jar");
			cmd.AppendSwitchIfNotNull ("--ref=", Path.GetFullPath (jarpath));

			cmd.AppendSwitchIfNotNull ("--out=", Path.GetFullPath (OutputFile));

			if (ReferenceJars != null)
				foreach (var jar in ReferenceJars)
					cmd.AppendSwitchIfNotNull ("--ref=", Path.GetFullPath (jar.ItemSpec));

			if (DroidDocPaths != null)
				foreach (var path in DroidDocPaths.Split (';'))
					cmd.AppendSwitchIfNotNull ("--droiddocpath=", Path.GetFullPath (path));

			if (JavaDocPaths != null)
				foreach (var path in JavaDocPaths.Split (';'))
					cmd.AppendSwitchIfNotNull ("--javadocpath=", Path.GetFullPath (path));
			
			if (Java7DocPaths != null)
				foreach (var path in Java7DocPaths.Split (';'))
					cmd.AppendSwitchIfNotNull ("--java7docpath=", Path.GetFullPath (path));

			if (Java8DocPaths != null)
				foreach (var path in Java8DocPaths.Split (';'))
					cmd.AppendSwitchIfNotNull ("--java8docpath=", Path.GetFullPath (path));

			if (JavaDocs != null)
				foreach (var doc in JavaDocs) {
					var opt = GetJavadocOption (doc.ItemSpec);
					if (opt != null)
						cmd.AppendSwitchIfNotNull (opt, Path.GetFullPath (Path.GetDirectoryName (doc.ItemSpec)));
				}
			return cmd.ToString ();
		}

		string GetJavadocOption (string file)
		{
			string rawHTML = File.ReadAllText (file);
			if (rawHTML.Length < 500)
				return null;
			if (rawHTML.Substring (0, 500).IndexOf ("Generated by javadoc (build 1.6", StringComparison.Ordinal) > 0)
				return "--javadocpath=";
			if (rawHTML.Substring (0, 500).IndexOf ("Generated by javadoc (version 1.7", StringComparison.Ordinal) > 0)
				return "--java7docpath=";
			return "--droiddocpath=";
		}

		protected override string ToolName { get { return "jar2xml"; } }

		public override string ToolExe {
			get { return OS.IsWindows ? "java.exe" : "java"; }
			set { base.ToolExe = value; }
		}

		protected override string GenerateFullPathToTool ()
		{
			return Path.Combine (JavaSdkDirectory, "bin", ToolExe);
		}

		private string GetMsBuildDirectory ()
		{
			return Path.GetDirectoryName (Assembly.GetExecutingAssembly ().Location);
		}
	}
}
