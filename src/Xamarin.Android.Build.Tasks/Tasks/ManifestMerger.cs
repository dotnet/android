using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	public class ManifestMerger : JavaToolTask
	{
		public override string TaskPrefix => "AMM";

		public override string DefaultErrorCode => $"{TaskPrefix}0000";

		[Required]
		public string ManifestMergerJarPath { get; set; }

		[Required]
		public string AndroidManifest { get; set; }

		[Required]
		public string OutputManifestFile { get; set; }

		public string [] LibraryManifestFiles { get; set; }

		public string [] ManifestPlaceholders { get; set; }

		string tempFile;
		string responseFile;

		public override bool Execute ()
		{
			tempFile = OutputManifestFile + ".tmp";
			responseFile = Path.Combine (Path.GetDirectoryName (OutputManifestFile), "manifestmerger.rsp");
			try {
				bool result = base.Execute ();
				if (!result)
					return result;
				var m = new ManifestDocument (tempFile);
				var ms = MemoryStreamPool.Shared.Rent ();
				try {
					m.Save (Log, ms);
					MonoAndroidHelper.CopyIfStreamChanged (ms, OutputManifestFile);
					return result;
				} finally {
					MemoryStreamPool.Shared.Return (ms);
				}
			} finally {
				if (File.Exists (tempFile))
					File.Delete (tempFile);
				if (File.Exists (responseFile))
					File.Delete (responseFile);
			}
		}

		protected override string GenerateCommandLineCommands ()
		{
			string cmd = GetCommandLineBuilder ().ToString ();
			Log.LogDebugMessage (cmd);
			return cmd;
		}

		protected virtual CommandLineBuilder GetCommandLineBuilder ()
		{
			var cmd = new CommandLineBuilder ();

			if (!string.IsNullOrEmpty (JavaOptions)) {
				cmd.AppendSwitch (JavaOptions);
			}
			cmd.AppendSwitchIfNotNull ("-Xmx", JavaMaximumHeapSize);
			cmd.AppendSwitchIfNotNull ("-cp ", $"{ManifestMergerJarPath}");
			cmd.AppendSwitch ("com.xamarin.manifestmerger.Main");
			StringBuilder sb = new StringBuilder ();
			sb.AppendLine ("--main");
			sb.AppendLine (AndroidManifest);
			if (LibraryManifestFiles != null) {
				sb.AppendLine ("--libs");
				sb.AppendLine ($"{string.Join ($"{Path.PathSeparator}", LibraryManifestFiles)}");
			}
			if (ManifestPlaceholders != null) {
				foreach (var entry in ManifestPlaceholders.Select (e => e.Split (new char [] { '=' }, 2, StringSplitOptions.None))) {
					if (entry.Length == 2) {
						sb.AppendLine ("--placeholder");
						sb.AppendLine ($"{entry [0]}={entry [1]}");
					} else
						Log.LogCodedWarning ("XA1010", string.Format (Properties.Resources.XA1010, string.Join (";", ManifestPlaceholders)));
				}
			}
			sb.AppendLine ("--out");
			sb.AppendLine (tempFile);
			File.WriteAllText (responseFile, sb.ToString ());
			cmd.AppendFileNameIfNotNull (responseFile);
			return cmd;
		}

		protected override void LogEventsFromTextOutput (string singleLine, MessageImportance messageImportance)
		{
			if (ExitCode != 0)
				Log.LogCodedError (DefaultErrorCode, singleLine);
			base.LogEventsFromTextOutput (singleLine, messageImportance);
		}
	}
}
