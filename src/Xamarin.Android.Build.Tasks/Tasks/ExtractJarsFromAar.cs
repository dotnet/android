#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	/// <summary>Extracts JARs and annotations from Android library archives for bindings.</summary>
	[MSBuildMultiThreadableTask]
	public class ExtractJarsFromAar : MultithreadedAndroidTask
	{
		const int MaxEntryDepth = 8;
		static readonly StringComparer PathComparer = Path.DirectorySeparatorChar == '\\' ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

		public override string TaskPrefix => "ELPJ";

		[Required]
		public string OutputJarsDirectory { get; set; } = "";

		[Required]
		public string OutputAnnotationsDirectory { get; set; } = "";

		public string []? Libraries { get; set; }

		[Required]
		public string OutputReferenceJarsDirectory { get; set; } = "";

		[Required]
		public string OutputReferenceAnnotationsDirectory { get; set; } = "";

		public string []? ReferenceLibraries { get; set; }

		public override bool RunTask ()
		{
			var outputJars = TaskEnvironment.GetAbsolutePath (OutputJarsDirectory).GetCanonicalForm ().Value;
			var outputAnnotations = TaskEnvironment.GetAbsolutePath (OutputAnnotationsDirectory).GetCanonicalForm ().Value;
			var referenceJars = TaskEnvironment.GetAbsolutePath (OutputReferenceJarsDirectory).GetCanonicalForm ().Value;
			var referenceAnnotations = TaskEnvironment.GetAbsolutePath (OutputReferenceAnnotationsDirectory).GetCanonicalForm ().Value;
			var existingFiles = new List<string> ();
			foreach (var directory in new [] { outputJars, outputAnnotations, referenceJars, referenceAnnotations })
				FindExistingFiles (directory, existingFiles);
			if (Log.HasLoggedErrors)
				return false;

			var archives = new List<string> ();
			var selected = new Dictionary<string, (int Archive, int Entry, bool Backslash)> (PathComparer);
			SelectEntries (Libraries, outputJars, outputAnnotations, archives, selected);
			SelectEntries (ReferenceLibraries, referenceJars, referenceAnnotations, archives, selected);
			if (Log.HasLoggedErrors)
				return false;

			// Remove stale files first, including case-only renames on case-sensitive platforms.
			foreach (var file in existingFiles) {
				if (selected.ContainsKey (file))
					continue;
				Log.LogDebugMessage ($"Deleting unknown file: {file}");
				Files.SetWriteable (file, checkExists: false);
				File.Delete (TaskEnvironment.GetAbsolutePath (file));
			}

			var byArchive = new SortedDictionary<int, List<(string Path, int Entry, bool Backslash)>> ();
			foreach (var selection in selected) {
				if (!byArchive.TryGetValue (selection.Value.Archive, out var files))
					byArchive.Add (selection.Value.Archive, files = new List<(string, int, bool)> ());
				files.Add ((selection.Key, selection.Value.Entry, selection.Value.Backslash));
			}
			var memoryStream = MemoryStreamPool.Shared.Rent ();
			try {
				foreach (var group in byArchive) {
					// Ordinally distinct paths can still alias on a case-insensitive Unix volume.
					// Preserve archive precedence and write slash entries last without merging case-sensitive paths.
					group.Value.Sort ((left, right) => {
						int order = right.Backslash.CompareTo (left.Backslash);
						return order != 0 ? order : left.Entry.CompareTo (right.Entry);
					});
					using var archive = Files.ReadZipFile (archives [group.Key]);
					foreach (var (path, entryIndex, _) in group.Value) {
						memoryStream.SetLength (0);
						using (var source = archive.Entries [entryIndex].Open ())
							source.CopyTo (memoryStream);
						memoryStream.Position = 0;
						if (Files.CopyIfStreamChanged (memoryStream, path, TaskEnvironment))
							Log.LogDebugMessage ($"Updated: {path}");
					}
				}
			} finally {
				MemoryStreamPool.Shared.Return (memoryStream);
			}
			return !Log.HasLoggedErrors;
		}

		void SelectEntries (string []? libraries, string jars, string annotations, List<string> archives,
			Dictionary<string, (int Archive, int Entry, bool Backslash)> selected)
		{
			if (libraries == null)
				return;
			foreach (var library in libraries) {
				var archivePath = TaskEnvironment.GetAbsolutePath (library).GetCanonicalForm ().Value;
				var archiveIndex = archives.Count;
				archives.Add (archivePath);
				var isAar = library.EndsWith (".aar", StringComparison.OrdinalIgnoreCase);
				using var archive = Files.ReadZipFile (archivePath);
				for (int i = 0; i < archive.Entries.Count; i++) {
					var entry = archive.Entries [i];
					if (entry.IsDirectory ())
						continue;
					var name = entry.FullName.Replace ('\\', '/');
					bool isAnnotation = string.Equals (Path.GetFileName (name), "annotations.zip", StringComparison.OrdinalIgnoreCase);
					if (!isAnnotation && !name.EndsWith (".jar", StringComparison.OrdinalIgnoreCase))
						continue;
					var destination = Path.Combine (isAnnotation ? annotations : jars, Path.GetFileName (archivePath));
					string path;
					try {
						if (name.Split ('/').Length > MaxEntryDepth)
							throw new InvalidDataException ();
						path = Files.GetArchiveExtractionPath (destination, name);
					} catch (InvalidDataException) {
						Log.LogCodedError ("XA4332", Properties.Resources.XA4332, library, entry.FullName);
						continue;
					}
					if (!isAnnotation && isAar && Files.ShouldSkipEntryInAar (name))
						continue;
					bool backslash = entry.FullName.IndexOf ('\\') >= 0;
					// Within one archive a forward-slash entry wins; otherwise the later entry wins.
					if (selected.TryGetValue (path, out var previous) && previous.Archive == archiveIndex && !previous.Backslash && backslash)
						continue;
					selected [path] = (archiveIndex, i, backslash);
				}
			}
		}

		void FindExistingFiles (string root, List<string> files)
		{
			var pending = new Stack<string> ();
			pending.Push (root);
			while (pending.Count > 0) {
				var path = pending.Pop ();
				FileAttributes attributes;
				try {
					attributes = File.GetAttributes (TaskEnvironment.GetAbsolutePath (path));
				} catch (FileNotFoundException) {
					continue;
				} catch (DirectoryNotFoundException) {
					continue;
				}
				if ((attributes & FileAttributes.ReparsePoint) != 0) {
					Log.LogCodedError ("XA4333", Properties.Resources.XA4333, path);
				} else if ((attributes & FileAttributes.Directory) != 0) {
					foreach (var child in Directory.EnumerateFileSystemEntries (TaskEnvironment.GetAbsolutePath (path)))
						pending.Push (child);
				} else {
					files.Add (path);
				}
			}
		}
	}
}
