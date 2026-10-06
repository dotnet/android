#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Extracts .jar files from @(AndroidLibrary) .aar files or @(LibraryProjectZip) for bindings
	/// </summary>
	public class ExtractJarsFromAar : AndroidTask
	{
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
			var memoryStream = MemoryStreamPool.Shared.Rent ();
			try {
				ExtractLibraries (Libraries, OutputJarsDirectory, OutputAnnotationsDirectory, memoryStream);
				ExtractLibraries (ReferenceLibraries, OutputReferenceJarsDirectory, OutputReferenceAnnotationsDirectory, memoryStream);
			} finally {
				MemoryStreamPool.Shared.Return (memoryStream);
			}

			return !Log.HasLoggedErrors;
		}

		void ExtractLibraries (string []? libraries, string outputJarsDirectory, string outputAnnotationsDirectory, MemoryStream memoryStream)
		{
			var comparer = Path.DirectorySeparatorChar == '\\' ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
			var jars = new HashSet<string> (comparer);
			var annotations = new HashSet<string> (comparer);
			if (libraries != null) {
				foreach (var library in libraries) {
					bool isAar = library.EndsWith (".aar", StringComparison.OrdinalIgnoreCase);
					var jarOutputDirectory = Path.Combine (outputJarsDirectory, Path.GetFileName (library));
					var annotationOutputDirectory = Path.Combine (outputAnnotationsDirectory, Path.GetFileName (library));
					using (var zip = MonoAndroidHelper.ReadZipFile (library)) {
						var entries = new List<(ZipArchiveEntry Entry, bool IsAnnotation)> ();
						foreach (var entry in zip.Entries) {
							if (entry.IsDirectory ())
								continue;
							var entryFullName = entry.FullName.Replace ('\\', '/');
							var fileName = Path.GetFileName (entryFullName);
							if (string.Equals (fileName, "annotations.zip", StringComparison.OrdinalIgnoreCase)) {
								Files.GetArchiveExtractionPath (annotationOutputDirectory, entryFullName);
								entries.Add ((entry, true));
							} else if (!entryFullName.EndsWith (".jar", StringComparison.OrdinalIgnoreCase)) {
								continue;
							} else {
								Files.GetArchiveExtractionPath (jarOutputDirectory, entryFullName);
								if (isAar && Files.ShouldSkipEntryInAar (entryFullName))
									continue;
								entries.Add ((entry, false));
							}
						}
						foreach (var (entry, isAnnotation) in entries) {
							var path = Files.GetArchiveExtractionPath (isAnnotation ? annotationOutputDirectory : jarOutputDirectory, entry.FullName);
							Extract (entry, memoryStream, path);
							(isAnnotation ? annotations : jars).Add (path);
						}
					}
				}
			}
			DeleteUnknownFiles (outputJarsDirectory, jars);
			DeleteUnknownFiles (outputAnnotationsDirectory, annotations);
		}

		static void Extract (ZipArchiveEntry entry, MemoryStream stream, string destination)
		{
			stream.SetLength (0); //Reuse the stream
			using (var source = entry.Open ())
				source.CopyTo (stream);
			stream.Position = 0;
			Files.CopyIfStreamChanged (stream, destination);
		}

		void DeleteUnknownFiles (string directory, HashSet<string> knownFiles)
		{
			if (!Directory.Exists (directory))
				return;
			var prefix = Path.GetFullPath (directory);
			if (!prefix.EndsWith (Path.DirectorySeparatorChar.ToString (), StringComparison.Ordinal))
				prefix += Path.DirectorySeparatorChar;
			foreach (var file in Directory.GetFiles (directory, "*", SearchOption.AllDirectories)) {
				var fullPath = Path.GetFullPath (file);
				var path = Files.GetArchiveExtractionPath (directory, fullPath.Substring (prefix.Length));
				if (!knownFiles.Contains (path)) {
					Log.LogDebugMessage ($"Deleting unknown file: {path}");
					File.Delete (path);
				}
			}
		}
	}
}
