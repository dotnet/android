using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Pdb2Mdb;

namespace Xamarin.Android.Tasks
{
	public class ConvertDebuggingFiles : Task
	{
		// The .pdb files we need to convert
		[Required]
		public ITaskItem[] Files { get; set; }

		public override bool Execute ()
		{
			Log.LogDebugMessage ("ConvertDebuggingFiles Task");
			Log.LogDebugMessage ("  InputFiles: {0}", Files);

			foreach (var file in Files) {
				var pdb = file.ToString ();

				if (!File.Exists (pdb))
					continue;

				try {
					MonoAndroidHelper.SetWriteable (pdb);
					Converter.Convert (Path.ChangeExtension (pdb, ".dll"));
				} catch (Exception ex) {
					Log.LogWarningFromException (ex, true);
				}
			}

			return true;
		}
	}
}
