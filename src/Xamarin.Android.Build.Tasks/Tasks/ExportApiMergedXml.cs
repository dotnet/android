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
			var orderedJars = GetOrderedSourceJars ().ToArray ();
			bool hasConflicts = GetOrderedSourceJars ().Select (s => Path.GetFileName (s.ItemSpec)).Distinct ().Count () != orderedJars.Length;
			var classParseXmls = new List<string> ();
			var apiAdjustedXmls = new List<string> ();

			foreach (var jarItem in orderedJars) {
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
			foreach (var xmlInput in classParseXmls) {
				var outfile = Path.ChangeExtension (xmlInput, ".xml");
				apiAdjustedXmls.Add (outfile);

				var aargs = new List<string> ();
				aargs.Add (xmlInput);
				aargs.Add ("--assembly=dummy");
				aargs.Add ("--only-xml-adjuster");
				aargs.Add ("--xml-adjuster-output=" + outfile);
				aargs.Add ("--api-level=" + AndroidApiLevel);
				foreach (var dll in ReferencedManagedLibraries)
					aargs.Add ("--ref=" + dll.ItemSpec);

				Log.LogDebugMessage ("Running {0} {1}", GetGeneratorToolFullPath (), string.Join (" ", aargs));
				var apsi = new ProcessStartInfo (GetGeneratorToolFullPath (), string.Join (" ", aargs)) {
					/*UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true*/
				};
				var aproc = Process.Start (apsi);
				aproc.WaitForExit ();
				if (aproc.ExitCode != 0)
					return false;
			}

			// api-merge
			var margs = new List<string> ();
			margs.Add ("-o:" + OutputFile);
			margs.AddRange (apiAdjustedXmls);
			Log.LogDebugMessage ("Running {0} {1}", GetApiMergeToolFullPath (), string.Join (" ", margs));
			var mpsi = new ProcessStartInfo (GetApiMergeToolFullPath (), string.Join (" ", margs)) {
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true
			};
			var mproc = Process.Start (mpsi);
			mproc.WaitForExit ();
			return mproc.ExitCode == 0;
#endif
		}

		IEnumerable<ITaskItem> GetOrderedSourceJars ()
		{
			Func<string, string, string> filterEmpty = (s, fallback) => string.IsNullOrWhiteSpace (s) ? fallback : s;
			var dic = SourceJars.ToDictionary (s => filterEmpty (s.GetMetadata ("OriginalProjectPath"), s.ItemSpec));
			var artifactPaths = dic.Keys.Select (p => Path.GetDirectoryName (Path.GetDirectoryName (p))).Distinct ();
			if (artifactPaths.Count () != 1) {
				Log.LogWarning ("Input source jar files are not in the expected directory layout. The Source Jar/Aar files should be '{{groupId}}\\{{artifactId}}\\{{version}}\\{{library.aar}}' and should share the same {{artifactId}} directory, like an artifact in 'm2repository'.");
				return SourceJars;
			} else {
				var artifactPath = artifactPaths.First ();
				return dic.OrderBy (p => p.Key.Substring (artifactPath.Length), StringComparer.OrdinalIgnoreCase).Select (p => p.Value);
			}
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

			foreach (var jar in GetOrderedSourceJars ())
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
