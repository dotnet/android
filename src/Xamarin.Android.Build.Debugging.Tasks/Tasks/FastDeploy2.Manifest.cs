using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Xamarin.Android.Tasks
{
	public class FastDeploy2 : FastDeploy2Base
	{
		const string RemoteStagingRootPath = "/tmp/fastdeploy2";
		const string RemoteReadyMarker = ".fastdeploy2-ready";
		const int MaxAdbCommandLength = 4096;

		public override string TaskPrefix => "FD2";

		protected override string RemoteStagingRoot => RemoteStagingRootPath;

		protected override async Task<bool> DeployFastDevFilesWithAdbPush (string overridePath)
		{
			var phase = Stopwatch.StartNew ();
			var files = PrepareDirectPushFiles ();
			var expectedFiles = new HashSet<string> (files.Select (file => file.RelativePath), StringComparer.Ordinal);
			var currentManifest = CreateManifest (files);
			SetDiagnosticElapsed ("deploy.fastdeploy2.local.stage.ms", phase);
			if (files.Count == 0) {
				LogDiagnostic ("No FastDev files were prepared for adb push deployment.");
				return true;
			}

			string remoteStagingPath = GetRemoteAdbPushStagingPath ();
			bool remoteReady = await IsRemoteReady (remoteStagingPath);
			var previousManifest = remoteReady ? LoadPreviousManifest () : null;
			if (previousManifest == null) {
				SetDiagnosticProperty ("deploy.fastdeploy2.manifest.full.push", 1);
			}

			var changedFiles = GetChangedFiles (currentManifest, previousManifest);
			var removedFiles = GetRemovedFiles (currentManifest, previousManifest);
			SetDiagnosticProperty ("deploy.fastdeploy2.manifest.changed.files", changedFiles.Count);
			SetDiagnosticProperty ("deploy.fastdeploy2.manifest.removed.files", removedFiles.Count);

			phase.Restart ();
			string output = await CreateRemoteStagingDirectories (remoteStagingPath, expectedFiles);
			SetDiagnosticElapsed ("deploy.fastdeploy2.remote.mkdir.ms", phase);
			if (!string.IsNullOrEmpty (output) && IsShellError (output, "mkdir")) {
				LogFastDeploy2Error ("XA0129", output, remoteStagingPath);
				return false;
			}

			phase.Restart ();
			if (!await RemoveRemoteStaleFiles (remoteStagingPath, removedFiles)) {
				return false;
			}
			SetDiagnosticElapsed ("deploy.fastdeploy2.remote.staging.cleanup.ms", phase);

			phase.Restart ();
			if (!await UploadChangedFiles (remoteStagingPath, files, changedFiles)) {
				return false;
			}
			SetDiagnosticElapsed ("deploy.fastdeploy2.upload.ms", phase);

			bool result;
			if (UseShellSymlinkAppFileTransfer ()) {
				result = await UpdateOverrideShellSymlinks (remoteStagingPath, overridePath, currentManifest, previousManifest, removedFiles);
			} else {
				result = await UpdateOverrideCopies (remoteStagingPath, overridePath);
			}

			if (result) {
				WriteManifest (currentManifest);
				await MarkRemoteReady (remoteStagingPath);
			}
			return result;
		}

		bool UseShellSymlinkAppFileTransfer ()
		{
			return string.Equals (AppFileTransferMode, "Symlink", StringComparison.OrdinalIgnoreCase);
		}

		async Task<bool> UpdateOverrideShellSymlinks (string remoteStagingPath, string overridePath, Dictionary<string, ManifestEntry> currentManifest, Dictionary<string, ManifestEntry> previousManifest, List<string> removedFiles)
		{
			var newFiles = previousManifest == null ?
				new HashSet<string> (currentManifest.Keys, StringComparer.Ordinal) :
				new HashSet<string> (currentManifest.Keys.Where (file => !previousManifest.ContainsKey (file)), StringComparer.Ordinal);
			SetDiagnosticProperty ("deploy.fastdeploy2.changed.files", newFiles.Count);
			SetDiagnosticProperty ("deploy.symlink.created.files", newFiles.Count);
			SetDiagnosticProperty ("deploy.symlink.removed.files", removedFiles.Count + newFiles.Count);
			SetDiagnosticProperty ("deploy.fastdeploy2.stale.files", removedFiles.Count);
			SetDiagnosticProperty ("deploy.symlink.tool.result", "shell");

			var phase = Stopwatch.StartNew ();
			if (!await RunCombinedShellSymlinkUpdate (remoteStagingPath, overridePath, currentManifest, previousManifest, newFiles, removedFiles)) {
				SetDiagnosticElapsed ("deploy.symlink.shell.update.ms", phase);
				return await FallbackToCopy (remoteStagingPath, overridePath);
			}
			SetDiagnosticElapsed ("deploy.symlink.shell.update.ms", phase);

			return true;
		}

		async Task<bool> RunCombinedShellSymlinkUpdate (string remoteStagingPath, string overridePath, Dictionary<string, ManifestEntry> currentManifest, Dictionary<string, ManifestEntry> previousManifest, HashSet<string> newFiles, List<string> removedFiles)
		{
			var directories = new HashSet<string> (StringComparer.Ordinal);
			foreach (string file in currentManifest.Keys.Concat (removedFiles)) {
				directories.Add (GetDirectoryName (file));
			}

			foreach (string directory in directories) {
				var currentInDirectory = currentManifest.Keys.Where (file => GetDirectoryName (file) == directory).ToList ();
				var newInDirectory = newFiles.Where (file => GetDirectoryName (file) == directory).ToList ();
				var removedInDirectory = removedFiles.Where (file => GetDirectoryName (file) == directory).ToList ();
				string targetDirectory = string.IsNullOrEmpty (directory) ? overridePath : $"{overridePath}/{directory}";
				string sourceDirectory = string.IsNullOrEmpty (directory) ? remoteStagingPath : $"{remoteStagingPath}/{directory}";

				if (previousManifest == null || newInDirectory.Count == currentInDirectory.Count) {
					string script = $"rm -f {ShellQuote (targetDirectory)}/*; mkdir -p {ShellQuote (targetDirectory)}; ln -sf {ShellQuote (sourceDirectory)}/* {ShellQuote (targetDirectory)}/";
					string output = await RunAs ("sh", "-c", script);
					if (RaiseRunAsError (output) || IsShellError (output, "rm") || IsShellError (output, "mkdir") || IsShellError (output, "ln")) {
						LogDiagnostic ($"Shell symlink glob update failed with '{output}'.");
						return false;
					}
					continue;
				}

				foreach (string script in CreateShellSymlinkScripts (remoteStagingPath, overridePath, newInDirectory, removedInDirectory)) {
					string output = await RunAs ("sh", "-c", script);
					if (RaiseRunAsError (output) || IsShellError (output, "rm") || IsShellError (output, "mkdir") || IsShellError (output, "ln")) {
						LogDiagnostic ($"Shell symlink batch update failed with '{output}'.");
						return false;
					}
				}
			}

			return true;
		}

		IEnumerable<string> CreateShellSymlinkScripts (string remoteStagingPath, string overridePath, List<string> newFiles, List<string> removedFiles)
		{
			var filesToRemove = removedFiles.Concat (newFiles).Select (file => $"{overridePath}/{file}").ToList ();
			foreach (var batch in BatchShellArguments ("rm -f", filesToRemove)) {
				yield return batch;
			}

			foreach (var group in newFiles.GroupBy (GetDirectoryName, StringComparer.Ordinal)) {
				string targetDirectory = string.IsNullOrEmpty (group.Key) ? overridePath : $"{overridePath}/{group.Key}";
				var prefix = $"mkdir -p {ShellQuote (targetDirectory)}; ln -sf";
				var suffix = ShellQuote (targetDirectory) + "/";
				var sources = group.Select (file => $"{remoteStagingPath}/{file}");
				foreach (var batch in BatchShellArguments (prefix, sources, suffix)) {
					yield return batch;
				}
			}
		}

		IEnumerable<string> BatchShellArguments (string prefix, IEnumerable<string> arguments, string suffix = "")
		{
			var builder = new StringBuilder (prefix);
			int count = 0;
			foreach (string argument in arguments) {
				string quoted = " " + ShellQuote (argument);
				if (count > 0 && builder.Length + quoted.Length + suffix.Length >= MaxAdbCommandLength) {
					if (!string.IsNullOrEmpty (suffix)) {
						builder.Append (' ').Append (suffix);
					}
					yield return builder.ToString ();
					builder.Clear ();
					builder.Append (prefix);
					count = 0;
				}
				builder.Append (quoted);
				count++;
			}
			if (count > 0) {
				if (!string.IsNullOrEmpty (suffix)) {
					builder.Append (' ').Append (suffix);
				}
				yield return builder.ToString ();
			}
		}

		static string GetDirectoryName (string file)
		{
			return Path.GetDirectoryName (file)?.Replace ("\\", "/") ?? "";
		}

		static string ShellQuote (string value)
		{
			return "'" + value.Replace ("'", "'\"'\"'") + "'";
		}

		async Task<bool> RemoveOverridePaths (string overridePath, IEnumerable<string> paths)
		{
			foreach (var batch in BatchArguments ("rm", "-f", paths.Select (file => $"{overridePath}/{file}"))) {
				string output = await RunAs (batch.ToArray ());
				if (RaiseRunAsError (output) || IsShellError (output, "rm")) {
					LogDiagnostic ($"Shell symlink remove failed with '{output}'.");
					return false;
				}
			}
			return true;
		}

		async Task<bool> CreateOverrideShellSymlinks (string remoteStagingPath, string overridePath, HashSet<string> newFiles)
		{
			var filesByDirectory = new Dictionary<string, List<string>> (StringComparer.Ordinal);
			foreach (string file in newFiles) {
				string directory = Path.GetDirectoryName (file)?.Replace ("\\", "/") ?? "";
				if (!filesByDirectory.TryGetValue (directory, out List<string> files)) {
					files = new List<string> ();
					filesByDirectory.Add (directory, files);
				}
				files.Add (file);
			}

			var phase = Stopwatch.StartNew ();
			foreach (var group in filesByDirectory) {
				string targetDirectory = string.IsNullOrEmpty (group.Key) ? overridePath : $"{overridePath}/{group.Key}";
				phase.Restart ();
				string output = await RunAs ("mkdir", "-p", targetDirectory);
				AddDiagnosticElapsed ("deploy.fastdeploy2.override.mkdir.ms", phase);
				if (RaiseRunAsError (output) || IsShellError (output, "mkdir")) {
					LogDiagnostic ($"Shell symlink mkdir failed with '{output}'.");
					return false;
				}

				for (int i = 0; i < group.Value.Count; i += 25) {
					var args = new List<string> { "ln", "-sf" };
					foreach (string file in group.Value.Skip (i).Take (25)) {
						args.Add ($"{remoteStagingPath}/{file}");
					}
					args.Add (targetDirectory);
					phase.Restart ();
					output = await RunAs (args.ToArray ());
					AddDiagnosticElapsed ("deploy.fastdeploy2.override.copy.ms", phase);
					if (RaiseRunAsError (output) || IsShellError (output, "ln")) {
						LogDiagnostic ($"Shell symlink ln failed with '{output}'.");
						return false;
					}
				}
			}

			return true;
		}

		async Task<bool> FallbackToCopy (string remoteStagingPath, string overridePath)
		{
			SetDiagnosticProperty ("deploy.symlink.tool.result", "shell fallback to copy");
			return await UpdateOverrideCopies (remoteStagingPath, overridePath);
		}

		async Task<bool> UpdateOverrideCopies (string remoteStagingPath, string overridePath)
		{
			var phase = Stopwatch.StartNew ();
			var stagedFileData = await GetRemoteFileData (remoteStagingPath, runAs: false);
			SetDiagnosticElapsed ("deploy.fastdeploy2.staging.stat.ms", phase);
			if (stagedFileData == null) {
				return false;
			}

			phase.Restart ();
			var overrideFileData = await GetRemoteFileData (overridePath, runAs: true);
			SetDiagnosticElapsed ("deploy.fastdeploy2.override.stat.ms", phase);
			if (overrideFileData == null) {
				return false;
			}

			if (!await RemoveStaleOverrideFiles (overridePath, stagedFileData, overrideFileData)) {
				return false;
			}

			return await CopyChangedFiles (remoteStagingPath, overridePath, stagedFileData, overrideFileData);
		}

		Dictionary<string, ManifestEntry> CreateManifest (List<DirectPushFile> files)
		{
			var manifest = new Dictionary<string, ManifestEntry> (StringComparer.Ordinal);
			foreach (var file in files) {
				var info = new FileInfo (file.LocalPath);
				manifest [file.RelativePath] = new ManifestEntry {
					RelativePath = file.RelativePath,
					LocalPath = file.LocalPath,
					Size = info.Length,
					LastWriteTimeUtcTicks = info.LastWriteTimeUtc.Ticks,
				};
			}
			return manifest;
		}

		HashSet<string> GetChangedFiles (Dictionary<string, ManifestEntry> currentManifest, Dictionary<string, ManifestEntry> previousManifest)
		{
			if (previousManifest == null) {
				return new HashSet<string> (currentManifest.Keys, StringComparer.Ordinal);
			}

			var changedFiles = new HashSet<string> (StringComparer.Ordinal);
			foreach (var entry in currentManifest) {
				if (!previousManifest.TryGetValue (entry.Key, out ManifestEntry previous) ||
						previous.Size != entry.Value.Size ||
						previous.LastWriteTimeUtcTicks != entry.Value.LastWriteTimeUtcTicks) {
					changedFiles.Add (entry.Key);
				}
			}
			return changedFiles;
		}

		List<string> GetRemovedFiles (Dictionary<string, ManifestEntry> currentManifest, Dictionary<string, ManifestEntry> previousManifest)
		{
			var removedFiles = new List<string> ();
			if (previousManifest == null) {
				return removedFiles;
			}

			foreach (var entry in previousManifest.Keys) {
				if (!currentManifest.ContainsKey (entry)) {
					removedFiles.Add (entry);
				}
			}
			return removedFiles;
		}

		async Task<bool> UploadChangedFiles (string remoteStagingPath, List<DirectPushFile> files, HashSet<string> changedFiles)
		{
			int pushed = 0;
			int skipped = 0;
			int batches = 0;
			var changedFileList = files.Where (file => changedFiles.Contains (file.RelativePath)).ToList ();
			foreach (var group in changedFileList.GroupBy (file => Path.GetDirectoryName (file.RelativePath)?.Replace ("\\", "/") ?? "", StringComparer.Ordinal)) {
				string remoteDirectory = string.IsNullOrEmpty (group.Key) ? remoteStagingPath : $"{remoteStagingPath}/{group.Key}";
				foreach (var batch in BatchPushFilesWithoutSync (group.ToList (), remoteDirectory)) {
					var result = await RunAdbCommand (batch.ToArray ());
					if (result.ExitCode != 0) {
						LogFastDeploy2Error ("XA0129", result.Output, remoteDirectory);
						return false;
					}
					var counts = TryParsePushSummary (result.Output);
					pushed += counts.pushed;
					skipped += counts.skipped;
					batches++;
					LogDiagnostic (result.Output);
				}
			}
			SetDiagnosticProperty ("deploy.fastdeploy2.adb.pushed.files", pushed);
			SetDiagnosticProperty ("deploy.fastdeploy2.adb.skipped.files", skipped);
			SetDiagnosticProperty ("deploy.fastdeploy2.bulk.batches", batches);
			SetDiagnosticProperty ("deploy.fastdeploy2.changed.files", changedFiles.Count);
			return true;
		}

		async Task<bool> RemoveRemoteStaleFiles (string remoteStagingPath, List<string> removedFiles)
		{
			foreach (var batch in BatchArguments ("rm", "-f", removedFiles.Select (file => $"{remoteStagingPath}/{file}"))) {
				var args = new [] { "shell" }.Concat (batch).ToArray ();
				var result = await RunAdbCommand (args);
				if (result.ExitCode != 0 || IsShellError (result.Output, "rm")) {
					LogFastDeploy2Error ("XA0129", result.Output, remoteStagingPath);
					return false;
				}
			}
			return true;
		}

		IEnumerable<List<string>> BatchPushFilesWithoutSync (List<DirectPushFile> files, string remoteDirectory)
		{
			var batch = CreatePushArgsPrefix ();
			int length = EstimateCommandLength (batch) + remoteDirectory.Length + 4;
			foreach (var file in files) {
				if (Path.GetFileName (file.LocalPath) != Path.GetFileName (file.RelativePath)) {
					yield return CreatePushArgs (file.LocalPath, $"{remoteDirectory}/{Path.GetFileName (file.RelativePath)}");
					continue;
				}

				int itemLength = file.LocalPath.Length + 3;
				if (batch.Count > 1 && length + itemLength >= MaxAdbCommandLength) {
					batch.Add (remoteDirectory);
					yield return batch;
					batch = CreatePushArgsPrefix ();
					length = EstimateCommandLength (batch) + remoteDirectory.Length + 4;
				}
				batch.Add (file.LocalPath);
				length += itemLength;
			}
			if (batch.Count > 1) {
				batch.Add (remoteDirectory);
				yield return batch;
			}
		}

		List<string> CreatePushArgs (string localPath, string remotePath)
		{
			var args = CreatePushArgsPrefix ();
			args.Add (localPath);
			args.Add (remotePath);
			return args;
		}

		List<string> CreatePushArgsPrefix ()
		{
			var args = new List<string> { "push" };
			if (!string.IsNullOrEmpty (AdbPushCompressionAlgorithm)) {
				args.Add ("-z");
				args.Add (AdbPushCompressionAlgorithm);
			}
			return args;
		}

		int EstimateCommandLength (List<string> args)
		{
			int length = 0;
			foreach (var arg in args) {
				length += arg.Length + 3;
			}
			return length;
		}

		async Task<bool> IsRemoteReady (string remoteStagingPath)
		{
			var result = await RunAdbCommand ("shell", "test", "-f", $"{remoteStagingPath}/{RemoteReadyMarker}");
			return result.ExitCode == 0;
		}

		async Task MarkRemoteReady (string remoteStagingPath)
		{
			await RunAdbCommand ("shell", "touch", $"{remoteStagingPath}/{RemoteReadyMarker}");
		}

		Dictionary<string, ManifestEntry> LoadPreviousManifest ()
		{
			string manifestFile = GetManifestFilePath ();
			if (!File.Exists (manifestFile)) {
				return null;
			}

			try {
				var manifest = JsonSerializer.Deserialize<Dictionary<string, ManifestEntry>> (File.ReadAllText (manifestFile));
				return manifest == null ? null : new Dictionary<string, ManifestEntry> (manifest, StringComparer.Ordinal);
			} catch (Exception ex) {
				LogDiagnostic ($"Ignoring FastDeploy2 manifest '{manifestFile}'. {ex}");
				return null;
			}
		}

		void WriteManifest (Dictionary<string, ManifestEntry> manifest)
		{
			string manifestFile = GetManifestFilePath ();
			Directory.CreateDirectory (Path.GetDirectoryName (manifestFile));
			File.WriteAllText (manifestFile, JsonSerializer.Serialize (manifest, new JsonSerializerOptions { WriteIndented = true }));
		}

		string GetManifestFilePath ()
		{
			return Path.Combine (GetFullPath (IntermediateOutputPath), "fastdeploy2", GetSafeFileName (PackageName), GetSafeFileName (GetUserId ()), GetSafeFileName (PrimaryCpuAbi), "manifest.json");
		}

		static string GetSafeFileName (string value)
		{
			if (string.IsNullOrEmpty (value)) {
				return "_";
			}

			var builder = new StringBuilder (value.Length);
			foreach (char c in value) {
				builder.Append (char.IsLetterOrDigit (c) || c == '.' || c == '-' || c == '_' ? c : '_');
			}
			return builder.ToString ();
		}

		class ManifestEntry {
			[JsonPropertyName ("relativePath")]
			public string RelativePath { get; set; }

			[JsonPropertyName ("localPath")]
			public string LocalPath { get; set; }

			[JsonPropertyName ("size")]
			public long Size { get; set; }

			[JsonPropertyName ("lastWriteTimeUtcTicks")]
			public long LastWriteTimeUtcTicks { get; set; }
		}
	}
}
