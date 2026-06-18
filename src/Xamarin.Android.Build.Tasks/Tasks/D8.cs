#nullable enable

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Collections.Generic;
using System.IO;
using Xamarin.Android.Tools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// This task invokes d8, and is also subclassed by the R8 task
	/// </summary>
	public class D8 : JavaToolTask
	{
		public override string TaskPrefix => "DX8";

		string? responseFilePath;

		[Required]
		public string JarPath { get; set; } = "";

		/// <summary>
		/// Output for *.dex files. R8 can be invoked for just --main-dex-list-output, so this is not [Required]
		/// </summary>
		public string? OutputDirectory { get; set; }

		/// <summary>
		/// It is loaded to calculate --min-api, which is used by desugaring part to determine which levels of desugaring it performs.
		/// </summary>
		public string? AndroidManifestFile { get; set; }

		// general d8 feature options.
		public bool Debug { get; set; }
		public bool EnableDesugar { get; set; } = true;

		// Java libraries to embed or reference
		public string? ClassesZip { get; set; }
		[Required]
		public string JavaPlatformJarPath { get; set; } = "";
		public ITaskItem []? JavaLibrariesToEmbed { get; set; }
		public ITaskItem []? AlternativeJarLibrariesToEmbed { get; set; }
		public ITaskItem []? JavaLibrariesToReference { get; set; }
		public ITaskItem []? MapDiagnostics { get; set; }

		public string? ExtraArguments { get; set; }

		public override bool RunTask ()
		{
			try {
				return base.RunTask ();
			} finally {
				if (!responseFilePath.IsNullOrEmpty () && File.Exists (responseFilePath)) {
					File.Delete (responseFilePath);
				}
			}
		}

		protected override string GenerateCommandLineCommands ()
		{
			return GetCommandLineBuilder ().ToString ();
		}

		protected virtual string MainClass => "com.android.tools.r8.D8";

		protected int MinSdkVersion { get; set; }

		protected virtual CommandLineBuilder GetCommandLineBuilder ()
		{
			var cmd = new CommandLineBuilder ();

			// Only JVM arguments go on the command line, everything else goes in the response file
			if (!JavaOptions.IsNullOrEmpty ()) {
				cmd.AppendSwitch (JavaOptions);
			}
			cmd.AppendSwitchIfNotNull ("-Xmx", JavaMaximumHeapSize);
			cmd.AppendSwitchIfNotNull ("-classpath ", JarPath);
			cmd.AppendSwitch (MainClass);

			// Create response file with all D8/R8 arguments to avoid command line length limits
			responseFilePath = CreateResponseFile ();
			cmd.AppendSwitch ($"@{responseFilePath}");

			return cmd;
		}

		/// <summary>
		/// Creates a response file containing all D8/R8 arguments.
		/// This avoids command line length limits that can occur with many jar libraries.
		/// </summary>
		protected virtual string CreateResponseFile ()
		{
			var responseFile = Path.GetTempFileName ();
			Log.LogDebugMessage ($"[{MainClass}] response file: {responseFile}");

			using var response = new StreamWriter (responseFile, append: false, encoding: Files.UTF8withoutBOM);

			// D8/R8 switches
			if (!ExtraArguments.IsNullOrEmpty ())
				WriteArg (response, ExtraArguments); // it should contain "--dex".
			if (Debug)
				WriteArg (response, "--debug");
			else
				WriteArg (response, "--release");

			//NOTE: if this is blank, we can omit --min-api in this call
			if (!AndroidManifestFile.IsNullOrEmpty ()) {
				var doc = AndroidAppManifest.Load (AndroidManifestFile, MonoAndroidHelper.SupportedVersions);
				if (doc.MinSdkVersion.HasValue) {
					MinSdkVersion = doc.MinSdkVersion.Value;
					WriteArg (response, "--min-api");
					WriteArg (response, MinSdkVersion.ToString ());
				}
			}

			if (!EnableDesugar)
				WriteArg (response, "--no-desugaring");

			if (!OutputDirectory.IsNullOrEmpty ()) {
				WriteArg (response, "--output");
				WriteArg (response, OutputDirectory);
			}

			// --map-diagnostics
			if (MapDiagnostics != null) {
				foreach (var diagnostic in MapDiagnostics) {
					var from = diagnostic.ItemSpec;
					var to = diagnostic.GetMetadata ("To");
					if (from.IsNullOrEmpty () || to.IsNullOrEmpty ())
						continue;
					WriteArg (response, "--map-diagnostics");
					WriteArg (response, from);
					WriteArg (response, to);
				}
			}

			// --lib and input jars
			var injars = new List<string> ();
			var libjars = new List<string> ();
			if (AlternativeJarLibrariesToEmbed?.Length > 0) {
				Log.LogDebugMessage ("  processing AlternativeJarLibrariesToEmbed...");
				foreach (var jar in AlternativeJarLibrariesToEmbed) {
					injars.Add (jar.ItemSpec);
				}
			} else if (JavaLibrariesToEmbed != null) {
				Log.LogDebugMessage ("  processing ClassesZip, JavaLibrariesToEmbed...");
				if (!ClassesZip.IsNullOrEmpty () && File.Exists (ClassesZip)) {
					injars.Add (ClassesZip);
				}
				foreach (var jar in JavaLibrariesToEmbed) {
					injars.Add (jar.ItemSpec);
				}
			}
			libjars.Add (JavaPlatformJarPath);
			if (JavaLibrariesToReference != null) {
				foreach (var jar in JavaLibrariesToReference) {
					libjars.Add (jar.ItemSpec);
				}
			}

			foreach (var jar in libjars) {
				WriteArg (response, "--lib");
				WriteArg (response, jar);
			}
			foreach (var jar in injars) {
				WriteArg (response, jar);
			}

			return responseFile;
		}

		/// <summary>
		/// Writes a single argument to the response file.
		/// R8/D8 response files treat each line as a complete argument, so no quoting is needed.
		/// </summary>
		protected void WriteArg (StreamWriter writer, string arg)
		{
			writer.WriteLine (arg);
			Log.LogDebugMessage ($"  {arg}");
		}

		// Note: We do not want to call the base.LogEventsFromTextOutput as it will incorrectly identify
		// Warnings and Info messages as errors.
		protected override void LogEventsFromTextOutput (string singleLine, MessageImportance messageImportance)
		{
			CheckForError (singleLine);
			Log.LogMessage (messageImportance, singleLine);
		}
	}
}
