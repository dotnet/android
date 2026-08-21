using System;
using System.IO;
using System.Linq;
using System.Text;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Android.Build.Tasks;

using IOFile        = System.IO.File;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	public sealed class Zip : Task
	{
		[Required]
		public  ITaskItem           File            { get; set; }

		public  ITaskItem[]         Entries         { get; set; }

		public  ITaskItem           Prefix          { get; set; }

		public  bool                Overwrite       { get; set; }

		public override bool Execute ()
		{
			Log.LogMessage (MessageImportance.Low, $"Task {nameof (Zip)}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (File)}: {File.ItemSpec}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (Entries)}:");
			foreach (var p in Entries ?? Array.Empty<ITaskItem> ()) {
				Log.LogMessage (MessageImportance.Low, $"    {p.ItemSpec}");
			}
			Log.LogMessage (MessageImportance.Low, $"  {nameof (Overwrite)}: {Overwrite}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (Prefix)}: {Prefix?.ItemSpec}");

			if (Overwrite) {
				IOFile.Delete (File.ItemSpec);
			}

			var prefix  = Prefix == null ? null : Path.GetFullPath (Prefix.ItemSpec);
			if (!string.IsNullOrEmpty (prefix) && !prefix.EndsWith (Path.DirectorySeparatorChar.ToString (), StringComparison.OrdinalIgnoreCase)) {
				prefix  += Path.DirectorySeparatorChar;
			}

			using (var zip  = ZipArchiveExtensions.OpenZipUpdate (File.ItemSpec)) {
				if (Entries == null)
					return !Log.HasLoggedErrors;
				foreach (var entry in Entries) {
					if (!IOFile.Exists (entry.ItemSpec)) {
						Log.LogWarning ($"Could not add file '{entry.ItemSpec}' to file '{File.ItemSpec}'. Skipping...");
						continue;
					}
					var zipDir      = (string) null;
					var entryPath   = Path.GetFullPath (entry.ItemSpec);
					var entryDir    = Path.GetDirectoryName (entryPath);
					if (prefix != null && entryDir.StartsWith (prefix, StringComparison.OrdinalIgnoreCase)) {
						zipDir = entryDir.Substring (prefix.Length);
					}
					var archivePath = string.IsNullOrEmpty (zipDir)
						? Path.GetFileName (entryPath)
						: Path.Combine (zipDir, Path.GetFileName (entryPath));
					zip.AddFile (entryPath, archivePath.Replace ('\\', '/'));
				}
			}
			return !Log.HasLoggedErrors;
		}
	}
}
