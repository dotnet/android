using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Pdb2Mdb;

namespace Xamarin.Android.Tasks
{
	public class ConvertDebuggingFiles : AndroidTask
	{
		public override string TaskPrefix => "CDF";

		// The .pdb files we need to convert
		[Required]
		public ITaskItem[] Files { get; set; }

		[Output]
		public ITaskItem[] ConvertedFiles { get; set; }

		public override bool RunTask ()
		{
			var convertedFiles = new List<ITaskItem> ();
			foreach (var file in Files) {
				var pdb = file.ItemSpec;

				if (!File.Exists (pdb))
					continue;

				try {
					MonoAndroidHelper.SetWriteable (pdb);
					Converter.Convert (Path.ChangeExtension (pdb, ".dll"));
					convertedFiles.Add (new TaskItem (Path.ChangeExtension (pdb, ".dll")));
				} catch (Exception ex) {
					Log.LogWarningFromException (ex, true);
				}
			}
			ConvertedFiles = convertedFiles.ToArray ();
			Log.LogDebugTaskItems ("[Output] ConvertedFiles:", ConvertedFiles);
			return true;
		}
	}
}
