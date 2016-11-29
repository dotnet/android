using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	public class ExportApiMergedXml : AndroidToolTask
	{
		public ITaskItem [] ApiVersioningDescriptors { get; set; }
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
		public string OutputDirectory { get; set; }
		public ITaskItem [] SourceJars { get; set; }
		public ITaskItem [] ReferenceJars { get; set; }
		public string DroidDocPaths { get; set; }
		public string JavaDocPaths { get; set; }
		public string Java7DocPaths { get; set; }
		public string Java8DocPaths { get; set; }
		public ITaskItem [] JavaDocs { get; set; }
		public ITaskItem [] LibraryProjectJars { get; set; }
		public ITaskItem [] ReferencedManagedLibraries { get; set; }

		public override bool Execute ()
		{
			Log.LogDebugMessage ("ExportApiMergedXml Task");
			Log.LogDebugMessage ("  OutputDirectory: {0}", OutputDirectory);
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
			Log.LogDebugTaskItems ("  ReferencedManagedLibraries:", ReferencedManagedLibraries);

			Directory.CreateDirectory (OutputDirectory);

			return base.Execute ();
		}

		protected override string GenerateCommandLineCommands ()
		{
			var cmd = new CommandLineBuilder ();

			cmd.AppendSwitchIfNotNull ("--output-dir=", OutputDirectory);

			if (ReferencedManagedLibraries != null)
				foreach (var lib in ReferencedManagedLibraries)
					cmd.AppendSwitchIfNotNull ("--dll=", Path.GetFullPath (lib.ItemSpec));

			cmd.AppendSwitchIfNotNull ("--api-level=", AndroidApiLevel);

			foreach (var jar in SourceJars)
				cmd.AppendSwitchIfNotNull ("--jar=", Path.GetFullPath (jar.ItemSpec));

			var libraryProjectJars = MonoAndroidHelper.ExpandFiles (LibraryProjectJars);

			foreach (var jar in libraryProjectJars) {
				if (MonoAndroidHelper.IsEmbeddedReferenceJar (jar))
					cmd.AppendSwitchIfNotNull ("--ref=", Path.GetFullPath (jar));
				else
					cmd.AppendSwitchIfNotNull ("--jar=", Path.GetFullPath (jar));
			}

			var jarpath = Path.Combine (AndroidSdkDirectory, "platforms", "android-" + MonoAndroidHelper.GetPlatformApiLevelName (AndroidApiLevel), "android.jar");
			cmd.AppendSwitchIfNotNull ("--ref=", Path.GetFullPath (jarpath));

			cmd.AppendSwitchIfNotNull ("--out=", Path.GetFullPath (OutputFile));
			cmd.AppendSwitchIfNotNull ("--outdir=", Path.GetFullPath (OutputDirectory));

			if (ReferenceJars != null)
				foreach (var jar in ReferenceJars)
					cmd.AppendSwitchIfNotNull ("--ref=", Path.GetFullPath (jar.ItemSpec));

			if (DroidDocPaths != null)
				foreach (var path in DroidDocPaths.Split (';'))
					cmd.AppendSwitchIfNotNull ("--docs=", Path.GetFullPath (path));

			if (JavaDocPaths != null)
				foreach (var path in JavaDocPaths.Split (';'))
					cmd.AppendSwitchIfNotNull ("--docs=", Path.GetFullPath (path));

			if (Java7DocPaths != null)
				foreach (var path in Java7DocPaths.Split (';'))
					cmd.AppendSwitchIfNotNull ("--docs=", Path.GetFullPath (path));

			if (Java8DocPaths != null)
				foreach (var path in Java8DocPaths.Split (';'))
					cmd.AppendSwitchIfNotNull ("--docs=", Path.GetFullPath (path));

			if (JavaDocs != null) {
				foreach (var doc in JavaDocs)
					cmd.AppendSwitchIfNotNull ("--docs=", Path.GetFullPath (Path.GetDirectoryName (doc.ItemSpec)));
			}

			return cmd.ToString ();
		}

		protected override string ToolName {
			get { return OS.IsWindows ? "export-api-merged-xml.exe" : "export-api-merged-xml"; }
		}

		protected override string GenerateFullPathToTool ()
		{
			return Path.Combine (ToolPath, ToolExe);
		}
	}
}
