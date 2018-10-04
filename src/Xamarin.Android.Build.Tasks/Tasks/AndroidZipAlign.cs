using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	public class AndroidZipAlign : AndroidToolTask
	{
		public AndroidZipAlign ()
		{
		}

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
			if (sourceFilename.EndsWith (strSignedUnaligned))
				sourceFilename = sourceFilename.Remove (sourceFilename.Length - strSignedUnaligned.Length);
			return string.Format ("-p {0} \"{1}\" \"{2}{3}{4}-Signed.apk\"",
				Alignment, Source.ItemSpec, DestinationDirectory.ItemSpec, Path.DirectorySeparatorChar, sourceFilename);
		}

		public override bool Execute ()
		{
			Log.LogDebugMessage ("AndroidZipAlign Task");
			Log.LogDebugMessage ("  Alignment: {0}", Alignment);
			Log.LogDebugMessage ("  Source: {0}", Source.ItemSpec);
			Log.LogDebugMessage ("  DestinationDirectory: {0}", DestinationDirectory.ItemSpec);

			return base.Execute ();
		}

		protected override string GenerateFullPathToTool ()
		{
			return Path.Combine (ToolPath, ToolExe);
		}

		protected override string ToolName
		{
			get { return IsWindows ? "zipalign.exe" : "zipalign"; }
		}
	}
}

