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

		protected override string GenerateCommandLineCommands ()
		{
			return GetCommandLineBuilder ().ToString ();
		}

		protected virtual string MainClass => "com.android.tools.r8.D8";

		protected int MinSdkVersion { get; set; }

		protected virtual CommandLineBuilder GetCommandLineBuilder ()
		{
			var cmd = new CommandLineBuilder ();

			if (JavaOptions is { Length: > 0 }) {
				cmd.AppendSwitch (JavaOptions);
			}
			cmd.AppendSwitchIfNotNull ("-Xmx", JavaMaximumHeapSize);
			cmd.AppendSwitchIfNotNull ("-classpath ", JarPath);
			cmd.AppendSwitch (MainClass);

			if (ExtraArguments is { Length: > 0 })
				cmd.AppendSwitch (ExtraArguments); // it should contain "--dex".
			if (Debug)
				cmd.AppendSwitch ("--debug");
			else
				cmd.AppendSwitch ("--release");

			//NOTE: if this is blank, we can omit --min-api in this call
			if (AndroidManifestFile is { Length: > 0 }) {
				var doc = AndroidAppManifest.Load (AndroidManifestFile, MonoAndroidHelper.SupportedVersions);
				if (doc.MinSdkVersion.HasValue) {
					MinSdkVersion = doc.MinSdkVersion.Value;
					cmd.AppendSwitchIfNotNull ("--min-api ", MinSdkVersion.ToString ());
				}
			}

			if (!EnableDesugar)
				cmd.AppendSwitch ("--no-desugaring");

			var injars = new List<string> ();
			var libjars = new List<string> ();
			if (AlternativeJarLibrariesToEmbed?.Length > 0) {
				Log.LogDebugMessage ("  processing AlternativeJarLibrariesToEmbed...");
				foreach (var jar in AlternativeJarLibrariesToEmbed) {
					injars.Add (jar.ItemSpec);
				}
			} else if (JavaLibrariesToEmbed != null) {
				Log.LogDebugMessage ("  processing ClassesZip, JavaLibrariesToEmbed...");
				if (ClassesZip is { Length: > 0 } && File.Exists (ClassesZip)) {
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

			cmd.AppendSwitchIfNotNull ("--output ", OutputDirectory);
			foreach (var jar in libjars)
				cmd.AppendSwitchIfNotNull ("--lib ", jar);
			foreach (var jar in injars)
				cmd.AppendFileNameIfNotNull (jar);

			if (MapDiagnostics != null) {
				foreach (var diagnostic in MapDiagnostics) {
					var from = diagnostic.ItemSpec;
					var to = diagnostic.GetMetadata ("To");
					if (from is not { Length: > 0 } || to is not { Length: > 0 })
						continue;
					cmd.AppendSwitch ("--map-diagnostics");
					cmd.AppendSwitch (from);
					cmd.AppendSwitch (to);
				}
			}

			return cmd;
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
