
#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks {

	public class CollectNonEmptyDirectories : AndroidTask {
		public override string TaskPrefix => "CNE";

		List<ITaskItem> output = new List<ITaskItem> ();
		List<ITaskItem> libraryResourceFiles = new List<ITaskItem> ();

		[Required]
		public ITaskItem[] Directories { get; set; } = [];

		[Required]
		public string LibraryProjectIntermediatePath { get; set; } = "";

		[Required]
		public string StampDirectory { get; set; } = "";

		[Output]
		public ITaskItem[] Output => output.ToArray ();

		[Output]
		public ITaskItem[] LibraryResourceFiles => libraryResourceFiles.ToArray ();

		public override bool RunTask ()
		{
			var libraryProjectDir = Path.GetFullPath (LibraryProjectIntermediatePath);
			foreach (var directory in Directories) {
				if (!Directory.Exists (directory.ItemSpec)) {
					Log.LogDebugMessage ($"Directory does not exist, skipping: {directory.ItemSpec}");
					continue;
				}
				string stampFile = directory.GetMetadata ("StampFile");
				string directoryHash = Files.HashString (directory.ItemSpec);
				if (stampFile.IsNullOrEmpty ()) {
					if (Path.GetFullPath (directory.ItemSpec).StartsWith (libraryProjectDir, StringComparison.OrdinalIgnoreCase)) {
						// If inside the `lp` directory
						stampFile = Path.GetFullPath (Path.Combine (directory.ItemSpec, "..", "..")) + ".stamp";
					} else {
						// Otherwise use a hashed stamp file
						stampFile = Path.Combine (StampDirectory, $"{directoryHash}.stamp");
					}
				}

				bool generateArchive = false;
				bool.TryParse (directory.GetMetadata (ResolveLibraryProjectImports.AndroidSkipResourceProcessing), out generateArchive);

				IEnumerable<string> files;
				string filesCache = directory.GetMetadata ("FilesCache");
				if (filesCache.IsNullOrEmpty ()) {
					if (Path.GetFullPath (directory.ItemSpec).StartsWith (libraryProjectDir, StringComparison.OrdinalIgnoreCase)) {
						filesCache = Path.Combine (directory.ItemSpec, "..", "files.cache");
					} else {
						filesCache = Path.Combine (directory.ItemSpec, "..", $"{directoryHash}-files.cache");
					}
				}
				DateTime lastwriteTime = File.Exists (stampFile) ? File.GetLastWriteTimeUtc (stampFile) : DateTime.MinValue;
				DateTime cacheLastWriteTime = File.Exists (filesCache) ? File.GetLastWriteTimeUtc (filesCache) : DateTime.MinValue;

				if (File.Exists (filesCache) && cacheLastWriteTime >= lastwriteTime) {
					Log.LogDebugMessage ($"Reading cached Library resources list from  {filesCache}");
					files = File.ReadAllLines (filesCache);
				} else {
					if (!File.Exists (filesCache))
						Log.LogDebugMessage ($"Cached Library resources list {filesCache} does not exist.");
					else
						Log.LogDebugMessage ($"Cached Library resources list {filesCache} is out of date.");
					if (generateArchive) {
						files = new string[1] { stampFile };
					} else {
						files = Directory.EnumerateFiles(directory.ItemSpec, "*.*", SearchOption.AllDirectories);
					}
				}

				if (files.Any ()) {
					if (!File.Exists (filesCache) || cacheLastWriteTime < lastwriteTime)
						File.WriteAllLines (filesCache, files, Encoding.UTF8);
					var taskItem = new TaskItem (directory.ItemSpec, new Dictionary<string, string> () {
						{"FileFound", files.First () },
					});
					directory.CopyMetadataTo (taskItem);

					if (string.IsNullOrEmpty (directory.GetMetadata ("StampFile"))) {
						taskItem.SetMetadata ("StampFile", stampFile);
					} else {
						Log.LogDebugMessage ($"%(StampFile) already set: {stampFile}");
					}
					if (string.IsNullOrEmpty (directory.GetMetadata ("FilesCache"))) {
						taskItem.SetMetadata ("FilesCache", filesCache);
					} else {
						Log.LogDebugMessage ($"%(FilesCache) already set: {filesCache}");
					}
					output.Add (taskItem);
					foreach (var file in files) {
						if (Aapt2.IsInvalidFilename (file)) {
							Log.LogDebugMessage ($"Invalid filename, ignoring: {file}");
							continue;
						}
						var fileTaskItem = new TaskItem (file, new Dictionary<string, string> () {
							{ "ResourceDirectory", directory.ItemSpec },
							{ ResolveLibraryProjectImports.ResourceDirectoryArchive, directory.GetMetadata (ResolveLibraryProjectImports.ResourceDirectoryArchive) },
							{ "StampFile", generateArchive ? stampFile : file },
							{ "FilesCache", filesCache},
							{ "Hash", stampFile },
							{ "_ArchiveDirectory", Path.Combine (directory.ItemSpec, "..", "flat" + Path.DirectorySeparatorChar) },
							{ "_FlatFile", generateArchive ?  $"{Path.GetFileNameWithoutExtension (stampFile)}.flata"  : Monodroid.AndroidResource.CalculateAapt2FlatArchiveFileName (file) },
						});
						libraryResourceFiles.Add (fileTaskItem);
					}
				}
			}
			return !Log.HasLoggedErrors;
		}
	}
}
