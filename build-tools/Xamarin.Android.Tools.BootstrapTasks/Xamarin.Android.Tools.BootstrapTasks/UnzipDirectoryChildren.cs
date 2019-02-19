using Microsoft.Build.Framework;
using System.IO;
using System.Text;
using Xamarin.Tools.Zip;
using MTask = Microsoft.Build.Utilities.Task;
using TTask = System.Threading.Tasks.Task;

namespace Xamarin.Android.Tools.BootstrapTasks
{

	public class UnzipDirectoryChildren : MTask
	{
		[Required]
		public  ITaskItem[]     SourceFiles         { get; set; }

		public  string          EntryNameEncoding   { get; set; }

		[Required]
		public  ITaskItem       DestinationFolder   { get; set; }

		public bool NoSubdirectory { get; set; }

		public override bool Execute ()
		{
			for (int i = 0; i < SourceFiles.Length; ++i) {
				var sf  = SourceFiles [i];
				var rp  = sf.GetMetadata ("DestDir");
				rp      = string.IsNullOrEmpty (rp)
					? ""
					: " [ " + rp + " ]";
				Log.LogMessage (MessageImportance.Low, "    {0}{1}", sf, rp);
			}

			if (File.Exists (DestinationFolder.ItemSpec)) {
				Log.LogError ("DestinationFolder must be a directory!");
				return false;
			}

			Directory.CreateDirectory (DestinationFolder.ItemSpec);

			var encoding = string.IsNullOrEmpty (EntryNameEncoding)
				? null
				: Encoding.GetEncoding (EntryNameEncoding);

			var tasks = new TTask [SourceFiles.Length];
			for (int i = 0; i < SourceFiles.Length; ++i) {
				var sourceFile      = SourceFiles [i];
				var relativeDestDir = sourceFile.GetMetadata ("DestDir");
				var enc             = encoding;
				var destFolder      = DestinationFolder.ItemSpec;
				tasks [i] = TTask.Run (() => ExtractFile (sourceFile.ItemSpec, relativeDestDir, destFolder, enc));
			}

			TTask.WaitAll (tasks);

			return !Log.HasLoggedErrors;
		}

		void ExtractFile (string sourceFile, string relativeDestDir, string destinationFolder, Encoding encoding)
		{
			relativeDestDir = relativeDestDir?.Replace ('\\', Path.DirectorySeparatorChar);

			using (var zip = ZipArchive.Open (sourceFile, FileMode.Open)) {
				foreach (var entry in zip) {
					if (!entry.IsDirectory) {
						var entryPath = entry.NativeFullName;
						if (!NoSubdirectory) {
							entryPath = entryPath.Substring (entryPath.IndexOf (Path.DirectorySeparatorChar) + 1);
						}
						var destinationPath = Path.Combine (destinationFolder, relativeDestDir, entryPath);
						entry.Extract (Path.GetDirectoryName (destinationPath), Path.GetFileName (destinationPath));
					}
				}
			}
		}
	}
}

