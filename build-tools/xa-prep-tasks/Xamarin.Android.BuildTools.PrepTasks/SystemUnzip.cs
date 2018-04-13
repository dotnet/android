using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using TTask = System.Threading.Tasks.Task;
using MTask = Microsoft.Build.Utilities.Task;

namespace Xamarin.Android.BuildTools.PrepTasks {

	public class SystemUnzip : MTask
	{
		[Required]
		public  ITaskItem[]     SourceFiles         { get; set; }

		public  string          SourceEntryGlob     { get; set; }

		public  string          EntryNameEncoding   { get; set; }

		public  string          HostOS              { get; set; }

		public  string          TempUnzipDir        { get; set; }

		[Required]
		public  ITaskItem       DestinationFolder   { get; set; }

		string[]    SourceEntryGlobParts;

		public override bool Execute ()
		{
			Log.LogMessage (MessageImportance.Low, $"{nameof (SystemUnzip)}:");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (DestinationFolder)}: {DestinationFolder.ItemSpec}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (EntryNameEncoding)}: {EntryNameEncoding}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (HostOS)}: {HostOS}");

			Log.LogMessage (MessageImportance.Low, $"  {nameof (SourceEntryGlob)}: {SourceEntryGlob}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (SourceFiles)}:");
			for (int i = 0; i < SourceFiles.Length; ++i) {
				var sf  = SourceFiles [i].ItemSpec;
				var rp  = SourceFiles [i].GetMetadata ("DestDir");
				rp      = string.IsNullOrEmpty (rp)
					? ""
					: " [ " + rp + " ]";
				Log.LogMessage (MessageImportance.Low, "    {0}{1}", sf, rp);
			}

			if (File.Exists (DestinationFolder.ItemSpec)) {
				Log.LogError ($"{nameof (DestinationFolder)} must be a directory!");
				return false;
			}

			SourceEntryGlobParts    = (SourceEntryGlob ?? "*").Split ('/', '\\');

			Directory.CreateDirectory (DestinationFolder.ItemSpec);

			var tempDir = TempUnzipDir ?? Path.Combine (Path.GetTempPath (), Path.GetRandomFileName ());
			Directory.CreateDirectory (tempDir);
			Log.LogMessage (MessageImportance.Low, $"  Extracting into temporary directory: {tempDir}");

			var encoding = string.IsNullOrEmpty (EntryNameEncoding)
				? null
				: Encoding.GetEncoding (EntryNameEncoding);

			var tasks = new TTask [SourceFiles.Length];
			for (int i = 0; i < SourceFiles.Length; ++i) {
				var td              = tempDir;
				var sourceFile      = SourceFiles [i].ItemSpec;
				var relativeDestDir = SourceFiles [i].GetMetadata ("DestDir");
				var enc             = encoding;
				var destFolder      = DestinationFolder.ItemSpec;
				tasks [i] = TTask.Run (() => ExtractFile (td, sourceFile, relativeDestDir, destFolder, enc));
			}

			TTask.WaitAll (tasks);

			Directory.Delete (tempDir, recursive: true);

			return !Log.HasLoggedErrors;
		}

		// Ignore CS1998 because there's no async System.IO APIs to use here.
		// Instead, we're using Tasks so that we can extract multiple files
		// in parallel via Task.Run() and Task.WaitAll().
#pragma warning disable 1998
		async TTask ExtractFile (string tempDir, string sourceFile, string relativeDestDir, string destinationFolder, Encoding encoding)
		{
			var tempName = Path.GetRandomFileName ();
			var nestedTemp = Path.Combine (tempDir, tempName);
			Directory.CreateDirectory (nestedTemp);

			relativeDestDir = relativeDestDir?.Replace ('\\', Path.DirectorySeparatorChar);

			bool isWindows = string.Equals (HostOS, "Windows", StringComparison.OrdinalIgnoreCase);
			if (isWindows) {
				ZipFile.ExtractToDirectory (sourceFile, nestedTemp, encoding);
			} else {
				var start = new ProcessStartInfo ("unzip", $"\"{sourceFile}\" -d \"{nestedTemp}\"") {
					CreateNoWindow = true,
					UseShellExecute = false,
				};
				Log.LogMessage (MessageImportance.Low, $"unzip \"{sourceFile}\" -d \"{nestedTemp}\"");
				var p = Process.Start (start);
				p.WaitForExit ();
			}

			var entries = GetExtractedSourceDirectories (nestedTemp);

			var seenDestFiles   = new HashSet<string> ();

			// "merge" directories from `name`/within `sourceFile` and `destinationFolder`.
			// If we did e.g. `mv foo/lib destination/lib` *and* `destination/lib` *already exists*,
			// we'd create `destination/lib/lib`, which isn't intended.
			// If we did e.g. `mv foo/lib destination`, **mv**(1) may *overwrite* `destination/lib`
			// if it already exists, which *also* isn't intended.
			// If `destination/lib/example` and `sourceFile` contains a `lib/another` entry,
			// then we want to create a `destination/lib/another` file.
			foreach (var entry in entries) {
				var name    = Path.GetFileName (entry);
				var destDir = string.IsNullOrEmpty (relativeDestDir)
					? destinationFolder
					: Path.Combine (destinationFolder, relativeDestDir);
				destDir = Path.Combine (destDir, name);
				foreach (var file in Directory.EnumerateFiles (entry, "*", SearchOption.AllDirectories)) {
					var relPath = file.Substring (entry.Length + 1);
					var dest    = Path.Combine (destDir, relPath);
					seenDestFiles.Add (dest);
					var destMdb = dest + ".mdb";
					if (File.Exists (destMdb) && !seenDestFiles.Contains (destMdb)) {
						Log.LogMessage (MessageImportance.Low, $"rm \"{destMdb}\"");
						File.Delete (destMdb);
					}
					var destPdb = Path.ChangeExtension (dest, ".pdb");
					if (File.Exists (destPdb) && !seenDestFiles.Contains (destPdb)) {
						Log.LogMessage (MessageImportance.Low, $"rm \"{destPdb}\"");
						File.Delete (destPdb);
					}
					Directory.CreateDirectory (Path.GetDirectoryName (dest));
					Log.LogMessage (MessageImportance.Low, $"mv '{file}' '{dest}'");
					if (Directory.Exists (entry)) {
						ProcessStartInfo psi;
						if (isWindows) {
							psi = new ProcessStartInfo ("cmd", $@"/C move ""{file}"" ""{dest}""") {
								CreateNoWindow = true,
								UseShellExecute = false,
							};
						} else {
							psi = new ProcessStartInfo ("/bin/mv", $@"""{file}"" ""{dest}""");
						}
						using (var p = Process.Start (psi)) {
							p.WaitForExit ();
						}
					}
					else {
						if (File.Exists (dest))
							File.Delete (dest);
						File.Move (file, dest);
					}
				}
			}
		}

		IEnumerable<string> GetExtractedSourceDirectories (string root)
		{
			var entries = Directory.EnumerateDirectories (root, SourceEntryGlobParts [0], SearchOption.TopDirectoryOnly);
			for (int i = 1; i < SourceEntryGlobParts.Length; ++i) {
				entries = entries
					.SelectMany (e => Directory.EnumerateDirectories (e, SourceEntryGlobParts [i], SearchOption.TopDirectoryOnly));
			}
			return entries;
		}
#pragma warning restore 1998
	}
}

