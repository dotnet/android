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
			foreach (var pair in Sources.Zip (DestinationDirectories, (s, d) => new { Source = s, Destination = d })) {
				if (!Directory.Exists (pair.Destination.ItemSpec))
					Directory.CreateDirectory (pair.Destination.ItemSpec);
				using (var z = ZipFile.OpenRead (pair.Source.ItemSpec)) {
					if (Files == null || Files.Length == 0) {
						Microsoft.Android.Build.Tasks.Files.ExtractAll (z, pair.Destination.ItemSpec, deleteCallback: _ => false, log: Log);
					} else {
						foreach (var file in Files) {
							var entry = z.GetEntry (file.ItemSpec);
							if (entry == null) {
								Log.LogDebugMessage ($"Skipping not existant file {file.ItemSpec}");
								continue;
							}
							string destinationFileName = file.GetMetadata ("DestinationFileName");
							if (destinationFileName.IsNullOrEmpty ())
								destinationFileName = file.ItemSpec;
							var fullDestination = Path.GetFullPath (pair.Destination.ItemSpec + Path.DirectorySeparatorChar);
							var outputPath = Path.GetFullPath (Path.Combine (fullDestination, destinationFileName.Replace ('\\', Path.DirectorySeparatorChar)));
							if (!outputPath.StartsWith (fullDestination, StringComparison.OrdinalIgnoreCase)) {
								Log.LogDebugMessage ($"Skipping archive entry '{file.ItemSpec}': resolves outside target directory.");
								continue;
							}
							Log.LogDebugMessage ($"Extracting {file.ItemSpec} to {destinationFileName}");
							Directory.CreateDirectory (Path.GetDirectoryName (outputPath));
							entry.ExtractToFile (outputPath, overwrite: true);
						}
					}
				}
			}

			return true;
		}
	}
}
