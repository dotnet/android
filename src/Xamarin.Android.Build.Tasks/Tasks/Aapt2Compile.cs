// Copyright (C) 2011 Xamarin, Inc. All rights reserved.
#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Xamarin.Android.Tools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks {

	public class Aapt2Compile : Aapt2 {
		public override string TaskPrefix => "A2C";

		List<ITaskItem> archives = new List<ITaskItem> ();
		List<ITaskItem> files = new List<ITaskItem> ();

		public string? ExtraArgs { get; set; }

		public string? FlatArchivesDirectory { get; set; }

		public string? FlatFilesDirectory { get; set; }
		public ITaskItem []? ResourcesToCompile { get; set; }

		[Output]
		public ITaskItem [] CompiledResourceFlatArchives => archives.ToArray ();

		[Output]
		public ITaskItem [] CompiledResourceFlatFiles => files.ToArray ();

		protected override int GetRequiredDaemonInstances ()
		{
			return Math.Min ((ResourcesToCompile ?? ResourceDirectories)?.Length ?? 1, DaemonMaxInstanceCount);
		}

		public async override System.Threading.Tasks.Task RunTaskAsync ()
		{
			await this.WhenAllWithLock (ResourcesToCompile ?? ResourceDirectories, ProcessDirectory);

			ProcessOutput ();

			for (int i = archives.Count -1; i > 0; i-- ) {
				if (!File.Exists (archives[i].ItemSpec)) {
					archives.RemoveAt (i);
				}
			}
		}

		void ProcessDirectory (ITaskItem item, object lockObject)
		{
			var flatFile = item.GetMetadata ("_FlatFile");
			bool isArchive = false;
			bool isDirectory = flatFile.EndsWith (".flata", StringComparison.OrdinalIgnoreCase);
			if (flatFile.IsNullOrEmpty ()) {
				FileAttributes fa = File.GetAttributes (item.ItemSpec);
				isDirectory = (fa & FileAttributes.Directory) == FileAttributes.Directory;
			}

			string fileOrDirectory = item.GetMetadata ("ResourceDirectory");
			if (fileOrDirectory.IsNullOrEmpty () || !isDirectory)
				fileOrDirectory = item.ItemSpec;
			if (isDirectory && !Directory.Exists (fileOrDirectory)) {
				LogWarning ($"Ignoring directory '{fileOrDirectory}' as it does not exist!");
				return;
			}
			if (isDirectory && !Directory.EnumerateDirectories (fileOrDirectory).Any ())
				return;

			string outputArchive = isDirectory ?  GetFullPath (FlatArchivesDirectory ?? "") : GetFullPath (FlatFilesDirectory ?? "");
			string targetDir = item.GetMetadata ("_ArchiveDirectory");
			if (!targetDir.IsNullOrEmpty ()) {
				outputArchive = GetFullPath (targetDir);
			}
			Directory.CreateDirectory (outputArchive);
			string expectedOutputFile;
			if (isDirectory) {
				if (flatFile.IsNullOrEmpty ())
					flatFile = item.GetMetadata ("Hash");
				var filename = !flatFile.IsNullOrEmpty () ? flatFile : "compiled";
				if (!filename.EndsWith (".flata", StringComparison.OrdinalIgnoreCase))
					filename = $"{filename}.flata";
				outputArchive = Path.Combine (outputArchive, filename);
				expectedOutputFile = outputArchive;
				string archive = item.GetMetadata (ResolveLibraryProjectImports.ResourceDirectoryArchive);
				if (!archive.IsNullOrEmpty () && File.Exists (archive)) {
					LogDebugMessage ($"Found Compressed Resource Archive '{archive}'.");
					fileOrDirectory = archive;
					isArchive = true;
				}
			} else {
				if (IsInvalidFilename (fileOrDirectory)) {
					LogDebugMessage ($"Invalid filename, ignoring: {fileOrDirectory}");
					return;
				}
				expectedOutputFile = Path.Combine (outputArchive, flatFile);
			}
			RunAapt (GenerateCommandLineCommands (fileOrDirectory, isDirectory, isArchive, outputArchive), expectedOutputFile);
			if (isDirectory) {
				lock (lockObject)
					archives.Add (new TaskItem (expectedOutputFile));
			} else {
				lock (lockObject)
					files.Add (new TaskItem (expectedOutputFile));
			}
		}

		protected string[] GenerateCommandLineCommands (string fileOrDirectory, bool isDirectory, bool isArchive, string outputArchive)
		{
			List<string> cmd = new List<string> ();
			cmd.Add ("compile");
			cmd.Add ($"-o");
			cmd.Add (GetFullPath (outputArchive));
			if (!ResourceSymbolsTextFile.IsNullOrEmpty ()) {
				cmd.Add ($"--output-text-symbols");
				cmd.Add (GetFullPath (ResourceSymbolsTextFile ?? ""));
			}
			if (isDirectory) {
				cmd.Add (isArchive ? "--zip" : "--dir");
				cmd.Add (GetFullPath (fileOrDirectory).TrimEnd ('\\'));
			} else
				cmd.Add (GetFullPath (fileOrDirectory));
			if (!ExtraArgs.IsNullOrEmpty ())
				cmd.Add (ExtraArgs);
			if (MonoAndroidHelper.LogInternalExceptions)
				cmd.Add ("-v");
			return cmd.ToArray ();
		}

	}
}
