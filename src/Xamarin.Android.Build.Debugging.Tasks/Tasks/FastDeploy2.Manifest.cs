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
		const string OverrideSymlinkReadyMarker = ".fastdeploy2-symlinks";

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
				if (!await ResetRemoteStagingDirectory (remoteStagingPath)) {
					return false;
				}
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
			bool overrideSymlinksReady = await IsOverrideSymlinkReady (overridePath);
			var previousSymlinkManifest = overrideSymlinksReady ? previousManifest : null;
			var newFiles = previousSymlinkManifest == null ?
				new HashSet<string> (currentManifest.Keys, StringComparer.Ordinal) :
				new HashSet<string> (currentManifest.Keys.Where (file => !previousSymlinkManifest.ContainsKey (file)), StringComparer.Ordinal);
			SetDiagnosticProperty ("deploy.fastdeploy2.changed.files", newFiles.Count);
			SetDiagnosticProperty ("deploy.symlink.created.files", newFiles.Count);
			SetDiagnosticProperty ("deploy.symlink.removed.files", removedFiles.Count + newFiles.Count);
			SetDiagnosticProperty ("deploy.fastdeploy2.stale.files", removedFiles.Count);
			SetDiagnosticProperty ("deploy.symlink.tool.result", "shell");

			var phase = Stopwatch.StartNew ();
			if (!await RunCombinedShellSymlinkUpdate (remoteStagingPath, overridePath, currentManifest, previousSymlinkManifest, newFiles, removedFiles)) {
				SetDiagnosticElapsed ("deploy.symlink.shell.update.ms", phase);
				return await FallbackToCopy (remoteStagingPath, overridePath);
			}
			SetDiagnosticElapsed ("deploy.symlink.shell.update.ms", phase);

			if (!await MarkOverrideSymlinkReady (overridePath)) {
				return await FallbackToCopy (remoteStagingPath, overridePath);
			}

			return true;
		}

		async Task<bool> RunCombinedShellSymlinkUpdate (string remoteStagingPath, string overridePath, Dictionary<string, ManifestEntry> currentManifest, Dictionary<string, ManifestEntry> previousManifest, HashSet<string> newFiles, List<string> removedFiles)
		{
			var currentByDirectory = GroupFilesByDirectory (currentManifest.Keys);
			var newByDirectory = GroupFilesByDirectory (newFiles);
			var removedByDirectory = GroupFilesByDirectory (removedFiles);
			var directories = new HashSet<string> (currentByDirectory.Keys, StringComparer.Ordinal);
			directories.UnionWith (removedByDirectory.Keys);

			foreach (string directory in directories) {
				currentByDirectory.TryGetValue (directory, out List<string> currentInDirectory);
				newByDirectory.TryGetValue (directory, out List<string> newInDirectory);
				removedByDirectory.TryGetValue (directory, out List<string> removedInDirectory);
				currentInDirectory = currentInDirectory ?? [];
				newInDirectory = newInDirectory ?? [];
				removedInDirectory = removedInDirectory ?? [];
				string targetDirectory = CombineRemotePath (overridePath, directory);
				string sourceDirectory = CombineRemotePath (remoteStagingPath, directory);

				if (currentInDirectory.Count > 0 && (previousManifest == null || newInDirectory.Count == currentInDirectory.Count)) {
					string script = $"d={QuoteShellArgument (targetDirectory)};s={QuoteShellArgument (sourceDirectory)};mkdir -p \"$d\"&&cd \"$d\"&&rm -f ./*&&ln -sf \"$s\"/* .";
					string output = await RunAsShell (script);
					if (RaiseRunAsError (output) || IsShellError (output, "rm") || IsShellError (output, "mkdir") || IsShellError (output, "ln")) {
						LogDiagnostic ($"Shell symlink glob update failed with '{output}'.");
						return false;
					}
					continue;
				}

				foreach (string script in CreateShellSymlinkScripts (remoteStagingPath, overridePath, newInDirectory, removedInDirectory)) {
					string output = await RunAsShell (script);
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
			foreach (var group in removedFiles.Concat (newFiles).GroupBy (GetDirectoryName, StringComparer.Ordinal)) {
				string targetDirectory = CombineRemotePath (overridePath, group.Key);
				var prefix = $"d={QuoteShellArgument (targetDirectory)};mkdir -p \"$d\"&&cd \"$d\"&&rm -f";
				foreach (var batch in BatchShellWords (prefix, group.Select (file => QuoteShellArgument (Path.GetFileName (file))))) {
					yield return batch;
				}
			}

			foreach (var group in newFiles.GroupBy (GetDirectoryName, StringComparer.Ordinal)) {
				string targetDirectory = CombineRemotePath (overridePath, group.Key);
				string sourceDirectory = CombineRemotePath (remoteStagingPath, group.Key);
				var prefix = $"d={QuoteShellArgument (targetDirectory)};s={QuoteShellArgument (sourceDirectory)};mkdir -p \"$d\"&&cd \"$d\"&&ln -sf";
				var sources = group.Select (file => "\"$s\"/" + QuoteShellArgument (Path.GetFileName (file)));
				foreach (var batch in BatchShellWords (prefix, sources, " .")) {
					yield return batch;
				}
			}
		}

		IEnumerable<string> BatchShellWords (string prefix, IEnumerable<string> words, string suffix = "")
		{
			var builder = new StringBuilder (prefix);
			int count = 0;
			foreach (string word in words) {
				string argument = " " + word;
				if (count > 0 && builder.Length + argument.Length + suffix.Length >= MaxAdbCommandLength) {
					if (!string.IsNullOrEmpty (suffix)) {
						builder.Append (suffix);
					}
					yield return builder.ToString ();
					builder.Clear ();
					builder.Append (prefix);
					count = 0;
				}
				builder.Append (argument);
				count++;
			}
			if (count > 0) {
				if (!string.IsNullOrEmpty (suffix)) {
					builder.Append (suffix);
				}
				yield return builder.ToString ();
			}
		}

		async Task<bool> FallbackToCopy (string remoteStagingPath, string overridePath)
		{
			SetDiagnosticProperty ("deploy.symlink.tool.result", "shell fallback to copy");
			return await UpdateOverrideCopies (remoteStagingPath, overridePath, clearOverrideDirectory: true);
		}

		async Task<bool> UpdateOverrideCopies (string remoteStagingPath, string overridePath, bool clearOverrideDirectory = false)
		{
			var phase = Stopwatch.StartNew ();
			if (clearOverrideDirectory) {
				if (!await ClearOverrideDirectory (overridePath)) {
					return false;
				}
			} else if (!await ClearOverrideSymlinkState (overridePath)) {
				return false;
			}

			var stagedFileData = await GetRemoteFileData (remoteStagingPath, runAs: false);
			SetDiagnosticElapsed ("deploy.fastdeploy2.staging.stat.ms", phase);
			if (stagedFileData == null) {
				return false;
			}
			stagedFileData.Remove (RemoteReadyMarker);

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
			foreach (var group in changedFileList.GroupBy (file => GetDirectoryName (file.RelativePath), StringComparer.Ordinal)) {
				string remoteDirectory = CombineRemotePath (remoteStagingPath, group.Key);
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
			foreach (var batch in BatchArguments ("rm", "-f", removedFiles.Select (file => CombineRemotePath (remoteStagingPath, file)))) {
				var args = new [] { "shell" }.Concat (batch).ToArray ();
				var result = await RunAdbCommand (args);
				if (result.ExitCode != 0 || IsShellError (result.Output, "rm")) {
					LogFastDeploy2Error ("XA0129", result.Output, remoteStagingPath);
					return false;
				}
			}
			return true;
		}

		async Task<bool> ResetRemoteStagingDirectory (string remoteStagingPath)
		{
			var result = await RunAdbCommand ("shell", "rm", "-rf", remoteStagingPath);
			if (result.ExitCode != 0 || IsShellError (result.Output, "rm")) {
				LogFastDeploy2Error ("XA0129", result.Output, remoteStagingPath);
				return false;
			}
			return true;
		}

		IEnumerable<List<string>> BatchPushFilesWithoutSync (List<DirectPushFile> files, string remoteDirectory)
		{
			var batch = CreatePushArgsPrefix ();
			int prefixCount = batch.Count;
			int length = EstimateCommandLength (batch) + remoteDirectory.Length + 4;
			foreach (var file in files) {
				if (Path.GetFileName (file.LocalPath) != Path.GetFileName (file.RelativePath)) {
					yield return CreatePushArgs (file.LocalPath, $"{remoteDirectory}/{Path.GetFileName (file.RelativePath)}");
					continue;
				}

				int itemLength = file.LocalPath.Length + 3;
				if (batch.Count > prefixCount && length + itemLength >= MaxAdbCommandLength) {
					batch.Add (remoteDirectory);
					yield return batch;
					batch = CreatePushArgsPrefix ();
					length = EstimateCommandLength (batch) + remoteDirectory.Length + 4;
				}
				batch.Add (file.LocalPath);
				length += itemLength;
			}
			if (batch.Count > prefixCount) {
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
			var result = await RunAdbCommand ("shell", "test", "-f", CombineRemotePath (remoteStagingPath, RemoteReadyMarker));
			return result.ExitCode == 0;
		}

		async Task<bool> IsOverrideSymlinkReady (string overridePath)
		{
			string output = await RunAsShell ($"test -f {QuoteShellArgument (CombineRemotePath (overridePath, OverrideSymlinkReadyMarker))} && echo yes || true");
			if (RaiseRunAsError (output)) {
				return false;
			}
			return string.Equals (output?.Trim (), "yes", StringComparison.Ordinal);
		}

		async Task<bool> MarkOverrideSymlinkReady (string overridePath)
		{
			string output = await RunAsShell ($"mkdir -p {QuoteShellArgument (overridePath)}; touch {QuoteShellArgument (CombineRemotePath (overridePath, OverrideSymlinkReadyMarker))}");
			if (RaiseRunAsError (output) || IsShellError (output, "mkdir") || IsShellError (output, "touch")) {
				LogFastDeploy2Error ("XA0129", output, overridePath);
				return false;
			}
			return true;
		}

		async Task<bool> ClearOverrideSymlinkState (string overridePath)
		{
			string markerPath = CombineRemotePath (overridePath, OverrideSymlinkReadyMarker);
			string output = await RunAsShell ($"if test -f {QuoteShellArgument (markerPath)}; then rm -rf {QuoteShellArgument (overridePath)}; else rm -f {QuoteShellArgument (markerPath)}; fi");
			if (RaiseRunAsError (output) || IsShellError (output, "rm")) {
				LogFastDeploy2Error ("XA0129", output, overridePath);
				return false;
			}
			return true;
		}

		async Task<bool> ClearOverrideDirectory (string overridePath)
		{
			string output = await RunAs ("rm", "-rf", overridePath);
			if (RaiseRunAsError (output) || IsShellError (output, "rm")) {
				LogFastDeploy2Error ("XA0129", output, overridePath);
				return false;
			}
			return true;
		}

		async Task MarkRemoteReady (string remoteStagingPath)
		{
			await RunAdbCommand ("shell", "touch", CombineRemotePath (remoteStagingPath, RemoteReadyMarker));
		}

		Dictionary<string, ManifestEntry> LoadPreviousManifest ()
		{
			string manifestFile = GetManifestFilePath ();
			if (!File.Exists (manifestFile)) {
				return null;
			}

			try {
				var manifest = JsonSerializer.Deserialize (File.ReadAllText (manifestFile), typeof (Dictionary<string, ManifestEntry>), FastDeploy2JsonSerializerContext.Default);
				return manifest is Dictionary<string, ManifestEntry> entries ? new Dictionary<string, ManifestEntry> (entries, StringComparer.Ordinal) : null;
			} catch (Exception ex) {
				LogDiagnostic ($"Ignoring FastDeploy2 manifest '{manifestFile}'. {ex}");
				return null;
			}
		}

		void WriteManifest (Dictionary<string, ManifestEntry> manifest)
		{
			string manifestFile = GetManifestFilePath ();
			Directory.CreateDirectory (Path.GetDirectoryName (manifestFile));
			File.WriteAllText (manifestFile, JsonSerializer.Serialize (manifest, typeof (Dictionary<string, ManifestEntry>), FastDeploy2JsonSerializerContext.Default));
		}

		string GetManifestFilePath ()
		{
			return Path.Combine (
				GetFullPath (IntermediateOutputPath),
				"fastdeploy2",
				GetSafeFileName (GetDeviceId ()),
				GetSafeFileName (PackageName),
				GetSafeFileName (GetUserId ()),
				GetSafeFileName (PrimaryCpuAbi),
				"manifest.json");
		}

		static string GetSafeFileName (string value)
		{
			return string.IsNullOrEmpty (value) ? "_" : Uri.EscapeDataString (value);
		}

		internal class ManifestEntry {
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
