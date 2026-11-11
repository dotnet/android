#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Synchronizes files that <c>_ExtractAar</c> staged with the built-in Unzip task into the final output directories.
	/// Archive entry separators are normalized, destinations must not traverse links, unchanged files keep their
	/// timestamps, and files that are no longer staged are deleted.
	/// </summary>
	[MSBuildMultiThreadableTask]
	public class SyncExtractedAarFiles : MultithreadedAndroidTask
	{
		public override string TaskPrefix => "SEAF";

		static readonly StringComparer PathComparer = Path.DirectorySeparatorChar == '\\' ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

		[Required]
		public string SourceDirectory { get; set; } = "";

		[Required]
		public string DestinationDirectory { get; set; } = "";

		[Required]
		public string [] Directories { get; set; } = [];

		public override bool RunTask ()
		{
			var source = TaskEnvironment.GetAbsolutePath (SourceDirectory).GetCanonicalForm ().Value;
			var destination = TaskEnvironment.GetAbsolutePath (DestinationDirectory).GetCanonicalForm ().Value;
			foreach (var directory in Directories)
				Sync (source, destination, directory);
			return !Log.HasLoggedErrors;
		}

		void Sync (string source, string destination, string directory)
		{
			// Resolve paths under the destination root so links anywhere below it, including the category directory, are rejected.
			var files = new Dictionary<string, string> (PathComparer);
			var sourceDirectory = Path.Combine (source, directory);
			if (Directory.Exists (TaskEnvironment.GetAbsolutePath (sourceDirectory))) {
				foreach (var (file, relativePath) in EnumerateFiles (sourceDirectory))
					files [Files.GetArchiveExtractionPath (destination, Path.Combine (directory, relativePath))] = file;
			}

			// Delete before copying so a case-only rename is recreated with its new name on case-sensitive comparisons.
			var destinationDirectory = Path.Combine (destination, directory);
			if (Directory.Exists (TaskEnvironment.GetAbsolutePath (destinationDirectory))) {
				foreach (var (_, relativePath) in EnumerateFiles (destinationDirectory)) {
					var file = Files.GetArchiveExtractionPath (destination, Path.Combine (directory, relativePath));
					if (files.ContainsKey (file))
						continue;
					Log.LogDebugMessage ($"Deleting unknown file: {file}");
					Files.SetWriteable (file, checkExists: false);
					File.Delete (TaskEnvironment.GetAbsolutePath (file));
				}
			}

			foreach (var file in files) {
				using var stream = File.OpenRead (TaskEnvironment.GetAbsolutePath (file.Value));
				if (Files.CopyIfStreamChanged (stream, file.Key, TaskEnvironment))
					Log.LogDebugMessage ($"Updated: {file.Key}");
			}
		}

		IEnumerable<(string File, string RelativePath)> EnumerateFiles (string directory)
		{
			var prefixLength = directory.Length + 1;
			foreach (var file in Directory.GetFiles (TaskEnvironment.GetAbsolutePath (directory), "*", SearchOption.AllDirectories))
				yield return (file, file.Substring (prefixLength));
		}
	}
}
