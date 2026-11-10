using Microsoft.Build.Framework;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Collections.Generic;
using MTask = Microsoft.Build.Utilities.Task;
using TTask = System.Threading.Tasks.Task;
using Files = Microsoft.Android.Build.Tasks.Files;

namespace Xamarin.Android.Tools.BootstrapTasks
{

	public class UnzipDirectoryChildren : MTask
	{
		[Required]
		public  ITaskItem[]     SourceFiles         { get; set; }

		public  string          EntryNameEncoding   { get; set; }

		[Required]
		public  ITaskItem       DestinationFolder   { get; set; }

		public ITaskItem[]      FilesToExtract      { get; set; }

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

			var filesToExtract = new HashSet<string> ();
			if (FilesToExtract != null) {
				foreach (var file in FilesToExtract) {
					filesToExtract.Add (file.ItemSpec);
				}
			}

			var tasks = new TTask [SourceFiles.Length];
			for (int i = 0; i < SourceFiles.Length; ++i) {
				var sourceFile      = SourceFiles [i];
				var relativeDestDir = sourceFile.GetMetadata ("DestDir");
				var enc             = encoding;
				var destFolder      = DestinationFolder.ItemSpec;
				tasks [i] = TTask.Run (() => ExtractFile (sourceFile.ItemSpec, relativeDestDir, destFolder, enc, filesToExtract));
			}

			TTask.WaitAll (tasks);

			return !Log.HasLoggedErrors;
		}

		void ExtractFile (string sourceFile, string relativeDestDir, string destinationFolder, Encoding encoding, HashSet<string> filesToExtract)
		{
			relativeDestDir = relativeDestDir?.Replace ('\\', Path.DirectorySeparatorChar);

			using (var zip = ZipFile.Open (sourceFile, ZipArchiveMode.Read, encoding)) {
				var entries = new List<(ZipArchiveEntry Entry, string Path)> ();
				foreach (var entry in zip.Entries) {
					var entryPath = entry.FullName.Replace ('/', Path.DirectorySeparatorChar).Replace ('\\', Path.DirectorySeparatorChar);
					var isDirectory = entryPath.EndsWith (Path.DirectorySeparatorChar);
					Files.GetArchiveExtractionPath (destinationFolder, entryPath, isDirectory);
					if (!isDirectory) {
						if (filesToExtract.Count > 0 && !filesToExtract.Contains (Path.GetFileName (entryPath)))
							continue;
						if (!NoSubdirectory) {
							entryPath = entryPath.Substring (entryPath.IndexOf (Path.DirectorySeparatorChar) + 1);
						}
						var relativePath = Path.Combine (relativeDestDir ?? "", entryPath);
						Files.GetArchiveExtractionPath (destinationFolder, relativePath);
						entries.Add ((entry, relativePath));
					}
				}
				foreach (var (entry, relativePath) in entries) {
					var destinationPath = Files.GetArchiveExtractionPath (destinationFolder, relativePath);
					Log.LogMessage (MessageImportance.Low, $"Extracting {entry.FullName} to {destinationPath}");
					Directory.CreateDirectory (Path.GetDirectoryName (destinationPath));
					File.Delete (destinationPath);
					entry.ExtractToFile (destinationPath);
				}
			}
		}
	}
}
