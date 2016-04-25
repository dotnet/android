using System;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Monodroid;
using System.Collections.Generic;
using System.IO;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	public class CopyAndConvertResources : Task
	{
		[Required]
		public ITaskItem[] SourceFiles { get; set; }

		[Required]
		public ITaskItem[] DestinationFiles { get; set; }

		[Required]
		public string AcwMapFile { get; set; }

		[Required]
		public string CacheFile { get; set; }

		[Output]
		public ITaskItem[] ModifiedFiles { get; set; }

		private List<ITaskItem> modifiedFiles = new List<ITaskItem>();

		public override bool Execute ()
		{
			Log.LogDebugMessage ("CopyAndConvertResources Task");
			Log.LogDebugTaskItems ("  SourceFiles:", SourceFiles);
			Log.LogDebugTaskItems ("  DestinationFiles:", DestinationFiles);
			Log.LogDebugMessage ("  AcwMapFile: {0}", AcwMapFile);

			if (SourceFiles.Length != DestinationFiles.Length)
				throw new ArgumentException ("source and destination count mismatch");

			var acw_map = MonoAndroidHelper.LoadAcwMapFile (AcwMapFile);

			var xmlFilesToUpdate = new Dictionary<string,string> ();
			for (int i = 0; i < SourceFiles.Length; i++) {
				var filename = SourceFiles [i].ItemSpec;

				if (File.Exists (filename)) {
					var ext = Path.GetExtension (filename);
					var destfilename = DestinationFiles [i].ItemSpec;
					var srcmodifiedDate = File.GetLastWriteTimeUtc (filename);
					var dstmodifiedDate = File.Exists (destfilename) ? File.GetLastAccessTimeUtc (destfilename) : DateTime.MinValue;
					var isXml = ext == ".xml" || ext == ".axml";

					Directory.CreateDirectory (Path.GetDirectoryName (destfilename));

					if (isXml) {
						xmlFilesToUpdate.Add (filename, DestinationFiles [i].ItemSpec);
						continue;
					}
					if (dstmodifiedDate < srcmodifiedDate && MonoAndroidHelper.CopyIfChanged (filename, destfilename)) {
						MonoAndroidHelper.SetWriteable (destfilename);

						// If the resource is not part of a raw-folder we strip away an eventual UTF-8 BOM
						// This is a requirement for the Android designer because the desktop Java renderer
						// doesn't support those type of BOM (it really wants the document to start
						// with "<?"). Since there is no way to plug into the file saving mechanism in X.S
						// we strip those here and point the designer to use resources from obj/
						if (isXml && !MonoAndroidHelper.IsRawResourcePath (filename))
							MonoAndroidHelper.CleanBOM (destfilename);

						MonoAndroidHelper.SetLastAccessAndWriteTimeUtc (destfilename, srcmodifiedDate, Log);
						modifiedFiles.Add (new TaskItem (destfilename));
					}
				} else
					Log.LogMessage ("    Warning: input resource '{0}' was not found", filename);
			}

			var merger = new ResourceMerger () {
				CacheFile = CacheFile,
				Log = Log,
			};
			merger.Load ();

			foreach (var p in xmlFilesToUpdate) {
				string filename = p.Key;
				var destfilename = p.Value;
				var srcmodifiedDate = File.GetLastWriteTimeUtc (filename);
				var dstmodifiedDate = File.Exists (destfilename) ? File.GetLastAccessTimeUtc (destfilename) : DateTime.MinValue;
				var tmpdest = p.Value + ".tmp";
				MonoAndroidHelper.CopyIfChanged (filename, tmpdest);
				MonoAndroidHelper.SetWriteable (tmpdest);
				try {
					AndroidResource.UpdateXmlResource (tmpdest, acw_map);
					if (MonoAndroidHelper.CopyIfChanged (tmpdest, destfilename)) {
						MonoAndroidHelper.SetWriteable (destfilename);
						MonoAndroidHelper.SetLastAccessAndWriteTimeUtc (destfilename, srcmodifiedDate, Log);
						if (!modifiedFiles.Any (i => i.ItemSpec == destfilename))
							modifiedFiles.Add (new TaskItem (destfilename));
					}
				} finally {
					File.Delete (tmpdest);
				}
			}
			merger.Save ();
			ModifiedFiles = modifiedFiles.ToArray ();

			Log.LogDebugTaskItems (" ModifiedFiles:", ModifiedFiles);

			return true;
		}
	}
}
