#nullable enable
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class UnzipToFolder : AndroidTask
	{
		public override string TaskPrefix => "UNZ";

		public ITaskItem []? Sources { get; set; }
		public ITaskItem []? DestinationDirectories { get; set; }
		public ITaskItem []? Files { get; set; }

		public override bool RunTask ()
		{
			if (Sources == null)
				throw new ArgumentNullException (nameof (Sources));
			if (DestinationDirectories == null)
				throw new ArgumentNullException (nameof (DestinationDirectories));
			if (Sources.Length != DestinationDirectories.Length)
				throw new ArgumentException ("Each source archive must have a destination directory.", nameof (DestinationDirectories));

			foreach (var pair in Sources.Zip (DestinationDirectories, (s, d) => new { Source = s, Destination = d })) {
				if (!Directory.Exists (pair.Destination.ItemSpec))
					Directory.CreateDirectory (pair.Destination.ItemSpec);
				using (var z = ZipFile.OpenRead (pair.Source.ItemSpec)) {
					if (Files == null || Files.Length == 0) {
						Microsoft.Android.Build.Tasks.Files.ExtractAll (z, pair.Destination.ItemSpec, deleteCallback: _ => false, log: Log);
					} else {
						var entries = Files.Select (file => {
							var entry = z.GetEntry (file.ItemSpec);
							if (entry == null) {
								Log.LogDebugMessage ($"Skipping nonexistent file {file.ItemSpec}");
								return (Entry: entry, OutputName: "", OutputPath: "", IsDirectory: false);
							}
							var isDirectory = entry.FullName.EndsWith ("/", StringComparison.Ordinal) || entry.FullName.EndsWith ("\\", StringComparison.Ordinal);
							Microsoft.Android.Build.Tasks.Files.GetArchiveExtractionPath (pair.Destination.ItemSpec, entry.FullName, isDirectory);
							var name = file.GetMetadata ("DestinationFileName");
							if (name.IsNullOrEmpty ())
								name = file.ItemSpec;
							return (Entry: entry, OutputName: name,
								OutputPath: Microsoft.Android.Build.Tasks.Files.GetArchiveExtractionPath (pair.Destination.ItemSpec, name, isDirectory), IsDirectory: isDirectory);
						}).ToArray ();
						foreach (var (entry, outputName, outputPath, isDirectory) in entries) {
							if (entry == null) {
								continue;
							}
							Microsoft.Android.Build.Tasks.Files.GetArchiveExtractionPath (pair.Destination.ItemSpec, outputName, isDirectory);
							Log.LogDebugMessage ($"Extracting {entry.FullName} to {outputPath}");
							if (isDirectory) {
								Directory.CreateDirectory (outputPath);
								continue;
							}
							var parent = Path.GetDirectoryName (outputPath);
							if (parent == null)
								throw new InvalidDataException ($"Archive entry '{entry.FullName}' has no destination directory.");
							Directory.CreateDirectory (parent);
							File.Delete (outputPath);
							entry.ExtractToFile (outputPath);
						}
					}
				}
			}

			return !Log.HasLoggedErrors;
		}
	}
}
