#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;
using Xamarin.Tools.Zip;
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

		public override bool RunTask ()
		{
			if (Libraries == null || Libraries.Length == 0)
				return true;

			var memoryStream = MemoryStreamPool.Shared.Rent ();
			try {
				var jars = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
				var annotations = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
				foreach (var library in Libraries) {
					bool isAar = library.EndsWith (".aar", StringComparison.OrdinalIgnoreCase);
					var jarOutputDirectory = Path.Combine (OutputJarsDirectory, Path.GetFileName (library));
					var annotationOutputDirectory = Path.Combine (OutputAnnotationsDirectory, Path.GetFileName (library));
					using (var zip = MonoAndroidHelper.ReadZipFile (library)) {
						foreach (var entry in zip) {
							if (entry.IsDirectory)
								continue;
							var entryFullName = entry.FullName;
							var fileName = Path.GetFileName (entryFullName);
							if (string.Equals (fileName, "annotations.zip", StringComparison.OrdinalIgnoreCase)) {
								var path = Path.GetFullPath (Path.Combine (annotationOutputDirectory, entryFullName));
								Extract (entry, memoryStream, path);
								annotations.Add (path);
							} else if (!entryFullName.EndsWith (".jar", StringComparison.OrdinalIgnoreCase)) {
								continue;
							} else if (isAar && Files.ShouldSkipEntryInAar (entryFullName)) {
								continue;
							} else {
								var path = Path.GetFullPath (Path.Combine (jarOutputDirectory, entryFullName));
								Extract (entry, memoryStream, path);
								jars.Add (path);
							}
						}
					}
				}
				DeleteUnknownFiles (OutputJarsDirectory, jars);
				DeleteUnknownFiles (OutputAnnotationsDirectory, annotations);
			} finally {
				MemoryStreamPool.Shared.Return (memoryStream);
			}

			return !Log.HasLoggedErrors;
		}

		static void Extract (ZipEntry entry, MemoryStream stream, string destination)
		{
			stream.SetLength (0); //Reuse the stream
			entry.Extract (stream);
			Files.CopyIfStreamChanged (stream, destination);
		}

		void DeleteUnknownFiles (string directory, HashSet<string> knownFiles)
		{
			if (!Directory.Exists (directory))
				return;
			foreach (var file in Directory.GetFiles (directory, "*", SearchOption.AllDirectories)) {
				var path = Path.GetFullPath (file);
				if (!knownFiles.Contains (path)) {
					Log.LogDebugMessage ($"Deleting unknown file: {path}");
					File.Delete (path);
				}
			}
		}
	}
}
