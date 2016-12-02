using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
		public string GeneratorToolPath { get; set; }
		[Required]
		public string ApiMergeToolPath { get; set; }
		public string GeneratorToolExe { get; set; }
		public string ApiMergeToolExe { get; set; }

		[Required]
		public string AndroidApiLevel { get; set; }
		[Required]
		public string OutputFile { get; set; }
		[Required]
		public string OutputDirectory { get; set; }
		public ITaskItem [] SourceJars { get; set; }
		public ITaskItem [] ReferenceJars { get; set; }
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
			Log.LogDebugTaskItems ("  JavaDocs: {0}", JavaDocs);
			Log.LogDebugTaskItems ("  LibraryProjectJars:", LibraryProjectJars);
			Log.LogDebugTaskItems ("  SourceJars:", SourceJars);
			Log.LogDebugTaskItems ("  ReferenceJars:", ReferenceJars);
			Log.LogDebugTaskItems ("  ReferencedManagedLibraries:", ReferencedManagedLibraries);

			Directory.CreateDirectory (OutputDirectory);

#if STANDALONE_TOOLCHAIN
			return base.Execute ();
#else
			// class-parse
			bool hasConflicts = SourceJars.Select (s => Path.GetFileName (s.ItemSpec)).Distinct ().Count () != SourceJars.Length;
			var classParseXmls = new List<string> ();
			var apiAdjustedXmls = new List<string> ();

			foreach (var jarItem in SourceJars) {
				var jar = jarItem.ItemSpec;
				var classPath = new Tools.Bytecode.ClassPath () {
					ApiSource = "class-parse",
					DocumentationPaths = JavaDocs.Select (s => s.ItemSpec)
				};
				if (Tools.Bytecode.ClassPath.IsJarFile (jar))
					classPath.Load (jar);
				var outname = hasConflicts ? Path.GetFileName (jar) : Path.GetFileName (Path.GetDirectoryName (jar)) + '_' + Path.GetFileName (jar);
				var outfile = Path.Combine (OutputDirectory, Path.ChangeExtension (outname, ".class-parse"));
				classParseXmls.Add (outfile);
				classPath.SaveXmlDescription (outfile);
			}

			// api-xml-adjuster
			foreach (var xml in classParseXmls) {
				var outfile = Path.ChangeExtension (xml, ".xml");
				apiAdjustedXmls.Add (outfile);

				var aargs = new List<string> ();
				aargs.Add (xml);
				aargs.Add ("--assembly=dummy");
				aargs.Add ("--only-xml-adjuster");
				aargs.Add ("--xml-adjuster-output=" + outfile);
				aargs.Add ("--api-level=" + AndroidApiLevel);
				foreach (var dll in ReferencedManagedLibraries)
					aargs.Add ("--ref=" + dll.ItemSpec);
				foreach (var docs in JavaDocs)
					aargs.Add ("--docs=" + docs.ItemSpec);

				Log.LogDebugMessage ("Running {0} {1}", GeneratorToolExe, string.Join (" ", apiAdjustedXmls));
				var aproc = Process.Start (GeneratorToolExe, string.Join (" ", aargs));
				aproc.WaitForExit ();
				if (aproc.ExitCode != 0)
					return false;
			}

			// api-merge
			Log.LogDebugMessage ("Running {0} {1}", ApiMergeToolExe, string.Join (" ", apiAdjustedXmls));
			var margs = new List<string> ();
			margs.Add ("-o:" + OutputFile);
			margs.AddRange (apiAdjustedXmls);
			var mproc = Process.Start (ApiMergeToolExe, string.Join (" ", margs));
			mproc.WaitForExit ();
			return mproc.ExitCode == 0;
#endif
		}

		string GetGeneratorToolFullPath ()
		{
			return GeneratorToolExe ?? Path.Combine (GeneratorToolPath, GeneratorToolName);
		}

		string GetApiMergeToolFullPath ()
		{
			return ApiMergeToolExe ?? Path.Combine (ApiMergeToolPath, ApiMergeToolName);
		}

		public string GeneratorToolName {
			get { return OS.IsWindows ? "generator.exe" : "generator"; }
		}

		public string ApiMergeToolName {
			get { return OS.IsWindows ? "api-merge.exe" : "api-merge"; }
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
