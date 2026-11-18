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

	[MSBuildMultiThreadableTask]
	public class CollectNonEmptyDirectories : MultithreadedAndroidTask {
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
			string libraryProjectDir = TaskEnvironment.GetAbsolutePath (LibraryProjectIntermediatePath).GetCanonicalForm ();
			foreach (var directory in Directories) {
				var directoryPath = TaskEnvironment.GetAbsolutePath (directory.ItemSpec).GetCanonicalForm ();
				if (!Directory.Exists (directoryPath)) {
					Log.LogDebugMessage ($"Directory does not exist, skipping: {directory.ItemSpec}");
					continue;
				}
				string stampFile = directory.GetMetadata ("StampFile");
				string directoryHash = Files.HashString (directory.ItemSpec);
				bool isLibraryProject = directoryPath.Value.StartsWith (libraryProjectDir, StringComparison.OrdinalIgnoreCase);
				if (stampFile.IsNullOrEmpty ()) {
					if (isLibraryProject) {
						// If inside the `lp` directory
						stampFile = TaskEnvironment.GetAbsolutePath (Path.Combine (directory.ItemSpec, "..", "..")).GetCanonicalForm ().Value + ".stamp";
					} else {
						// Otherwise use a hashed stamp file
						stampFile = Path.Combine (StampDirectory, $"{directoryHash}.stamp");
					}
				}

				bool generateArchive = false;
				bool.TryParse (directory.GetMetadata (LibraryProjectImportsMetadata.AndroidSkipResourceProcessing), out generateArchive);

				string [] files;
				string filesCache = directory.GetMetadata ("FilesCache");
				if (filesCache.IsNullOrEmpty ()) {
					if (isLibraryProject) {
						filesCache = Path.Combine (directory.ItemSpec, "..", "files.cache");
					} else {
						filesCache = Path.Combine (directory.ItemSpec, "..", $"{directoryHash}-files.cache");
					}
				}
				var stampPath = TaskEnvironment.GetAbsolutePath (stampFile);
				var cachePath = TaskEnvironment.GetAbsolutePath (filesCache);
				DateTime lastwriteTime = File.Exists (stampPath) ? File.GetLastWriteTimeUtc (stampPath) : DateTime.MinValue;
				DateTime cacheLastWriteTime = File.Exists (cachePath) ? File.GetLastWriteTimeUtc (cachePath) : DateTime.MinValue;

				if (File.Exists (cachePath) && cacheLastWriteTime >= lastwriteTime) {
					Log.LogDebugMessage ($"Reading cached Library resources list from  {filesCache}");
					files = File.ReadAllLines (cachePath);
				} else {
					if (!File.Exists (cachePath))
						Log.LogDebugMessage ($"Cached Library resources list {filesCache} does not exist.");
					else
						Log.LogDebugMessage ($"Cached Library resources list {filesCache} is out of date.");
					if (generateArchive) {
						files = [stampFile];
					} else {
						files = Directory.EnumerateFiles (directoryPath, "*.*", SearchOption.AllDirectories)
							.Select (file => Path.Combine (directory.ItemSpec, file.Substring (directoryPath.Value.Length).TrimStart (Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)))
							.ToArray ();
					}
				}

				if (files.Length > 0) {
					if (!File.Exists (cachePath) || cacheLastWriteTime < lastwriteTime)
						File.WriteAllLines (cachePath, files, Encoding.UTF8);
					var taskItem = new TaskItem (directory.ItemSpec, new Dictionary<string, string> () {
						{"FileFound", files [0] },
					});
					directory.CopyMetadataTo (taskItem);

					if (directory.GetMetadata ("StampFile").IsNullOrEmpty ()) {
						taskItem.SetMetadata ("StampFile", stampFile);
					} else {
						Log.LogDebugMessage ($"%(StampFile) already set: {stampFile}");
					}
					if (directory.GetMetadata ("FilesCache").IsNullOrEmpty ()) {
						taskItem.SetMetadata ("FilesCache", filesCache);
					} else {
						Log.LogDebugMessage ($"%(FilesCache) already set: {filesCache}");
					}
					output.Add (taskItem);
					foreach (var file in files) {
						if (MonoAndroidHelper.IsInvalidFilename (file)) {
							Log.LogDebugMessage ($"Invalid filename, ignoring: {file}");
							continue;
						}
						var fileTaskItem = new TaskItem (file, new Dictionary<string, string> () {
							{ "ResourceDirectory", directory.ItemSpec },
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
