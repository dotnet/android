using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using TTask = System.Threading.Tasks.Task;
using MTask = Microsoft.Build.Utilities.Task;

namespace Xamarin.Android.Tools.BootstrapTasks {

	public class UnzipDirectoryChildren : MTask
	{
		public UnzipDirectoryChildren ()
		{
		}

		[Required]
		public  ITaskItem[]     SourceFiles         { get; set; }

		public  string          EntryNameEncoding   { get; set; }

		public  string          HostOS              { get; set; }

		[Required]
		public  ITaskItem       DestinationFolder   { get; set; }

		public bool NoSubdirectory { get; set; }

		public override bool Execute ()
		{
			Log.LogMessage (MessageImportance.Low, $"{nameof (UnzipDirectoryChildren)}:");
			Log.LogMessage (MessageImportance.Low, "  DestinationFolder: {0}", DestinationFolder.ItemSpec);
			Log.LogMessage (MessageImportance.Low, "  EntryNameEncoding: {0}", EntryNameEncoding);
			Log.LogMessage (MessageImportance.Low, "  SourceFiles:");
			for (int i = 0; i < SourceFiles.Length; ++i) {
				var sf  = SourceFiles [i].ItemSpec;
				var rp  = SourceFiles [i].GetMetadata ("DestDir");
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

			var tempDir = Path.Combine (Path.GetTempPath (), Path.GetRandomFileName ());
			Directory.CreateDirectory (tempDir);
			Log.LogMessage ($"# jonp: tempDir: '{tempDir}'");

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
			relativeDestDir = relativeDestDir?.Replace ('\\', Path.DirectorySeparatorChar);

			if (string.Equals (HostOS, "Windows", StringComparison.OrdinalIgnoreCase)) {
				//NOTE: to avoid MAX_PATH, we are not using %TEMP% on Windows
				using (var source = File.OpenRead (sourceFile))
				using (var zip = new ZipArchive (source)) {
					foreach (var entry in zip.Entries) {
						//Directory entries have empty names
						if (!string.IsNullOrEmpty (entry.Name)) {
							//entry.FullName can have / or \ depending on your .NET version
							var entryPath = entry.FullName.Replace ('/', Path.DirectorySeparatorChar);
							if (!NoSubdirectory) {
								entryPath = entryPath.Substring (entryPath.IndexOf (Path.DirectorySeparatorChar) + 1);
							}
							var destinationPath = Path.Combine (destinationFolder, relativeDestDir, entryPath);
							var destinationDir = Path.GetDirectoryName (destinationPath);
							if (!Directory.Exists (destinationDir))
								Directory.CreateDirectory (destinationDir);

							entry.ExtractToFile (destinationPath, overwrite: true);
						}
					}
				}
			} else {
				var tempName    = Path.GetRandomFileName ();
				var nestedTemp  = Path.Combine (tempDir, tempName);
				Directory.CreateDirectory (nestedTemp);

				//NOTE: using unzip to preserve file attributes
				var start   = new ProcessStartInfo ("unzip", $"\"{sourceFile}\" -d \"{nestedTemp}\"") {
					CreateNoWindow  = true,
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
				};
				Log.LogMessage (MessageImportance.Low, $"unzip \"{sourceFile}\" -d \"{nestedTemp}\"");
				using (var p = Process.Start (start)) {
					p.OutputDataReceived += (sender, e) => {
						if (string.IsNullOrEmpty(e.Data))
							return;
						Console.Out.Write(e.Data + "\n");
					};
					p.ErrorDataReceived += (sender, e) => {
						if (string.IsNullOrEmpty(e.Data))
							return;
						Console.Error.Write(e.Data + "\n");
					};
					p.BeginOutputReadLine ();
					p.BeginErrorReadLine ();
					p.WaitForExit ();
				}


				var dirs = NoSubdirectory ? new string [] { nestedTemp } : Directory.EnumerateDirectories (nestedTemp, "*");

				foreach (var dir in dirs) {
					foreach (var fse in Directory.EnumerateFileSystemEntries (dir)) {
						var name    = Path.GetFileName (fse);
						var destDir = string.IsNullOrEmpty (relativeDestDir)
							? destinationFolder
							: Path.Combine (destinationFolder, relativeDestDir);
						Directory.CreateDirectory (destDir);
						var dest    = Path.Combine (destDir, name);
						Log.LogMessage (MessageImportance.Low, $"mv '{fse}' '{dest}'");
						if (Directory.Exists (fse))
							Process.Start ("/bin/mv", $@"""{fse}"" ""{dest}""").WaitForExit ();
						else {
							if (File.Exists (dest))
								File.Delete (dest);
							File.Move (fse, dest);
						}
					}
				}
			}
		}
#pragma warning restore 1998
	}
}

