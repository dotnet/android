using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class AndroidZipAlign : AndroidRunToolTask
	{
		public override string TaskPrefix => "AZA";

		[Required]
		public ITaskItem Source { get; set; }

		[Required]
		public ITaskItem DestinationDirectory { get; set; }

		int alignment = 4;
		public int Alignment {
			get {return alignment;}
			set {alignment = value;}
		}

		static readonly string strSignedUnaligned = "-Signed-Unaligned";

		protected override string DefaultErrorCode => "ANDZA0000";

		protected override string GenerateCommandLineCommands ()
		{
			string sourceFilename = Path.GetFileNameWithoutExtension (Source.ItemSpec);
			if (sourceFilename.EndsWith (strSignedUnaligned, StringComparison.OrdinalIgnoreCase))
				sourceFilename = sourceFilename.Remove (sourceFilename.Length - strSignedUnaligned.Length);
			return string.Format ("-p {0} \"{1}\" \"{2}{3}{4}-Signed.apk\"",
				Alignment, Source.ItemSpec, DestinationDirectory.ItemSpec, Path.DirectorySeparatorChar, sourceFilename);
		}

		protected override string GenerateFullPathToTool ()
		{
			return Path.Combine (ToolPath, ToolExe);
		}

		protected override string ToolName
		{
			get { return IsWindows ? "zipalign.exe" : "zipalign"; }
		}

		protected override void LogEventsFromTextOutput (string singleLine, MessageImportance messageImportance)
		{
			if (ExitCode != 0)
				Log.LogCodedError (DefaultErrorCode, singleLine);
			else
				base.LogEventsFromTextOutput (singleLine, messageImportance);
		}
	}
}

