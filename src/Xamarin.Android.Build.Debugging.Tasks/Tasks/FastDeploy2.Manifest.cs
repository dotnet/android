using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Xamarin.Android.Tasks
{
	public partial class FastDeploy2
	{
		const string RemoteStagingRootPath = "/data/local/tmp/fastdeploy2";
		const string ManifestHashMarker = ".fastdeploy2-manifest-hash";

		string RemoteStagingRoot => RemoteStagingRootPath;

		async Task<bool> DeployFastDevFilesWithAdbPush (string overridePath)
		{
			var files = PrepareDirectPushFiles ();
			var expectedFiles = new HashSet<string> (files.Select (file => file.RelativePath), StringComparer.Ordinal);
			var currentManifest = CreateManifest (files);
			if (files.Count == 0) {
				LogDiagnostic ("No FastDev files were prepared for adb push deployment.");
				return true;
			}

			string remoteStagingPath = GetRemoteAdbPushStagingPath ();
			var previousManifest = LoadPreviousManifest ();
			string previousManifestHash = previousManifest == null ? "" : ComputeManifestHash (previousManifest);
			var deviceManifestState = previousManifest == null ? new DeviceManifestState () : await GetDeviceManifestState (remoteStagingPath, overridePath);
			bool remoteReady = previousManifest != null && string.Equals (deviceManifestState.RemoteHash, previousManifestHash, StringComparison.Ordinal);
			bool overrideSymlinksReady = previousManifest != null && string.Equals (deviceManifestState.OverrideHash, previousManifestHash, StringComparison.Ordinal);
			if (!remoteReady) {
				previousManifest = null;
				overrideSymlinksReady = false;
				if (!await ResetRemoteStagingDirectory (remoteStagingPath)) {
					return false;
				}
			}

			var changedFiles = GetChangedFiles (currentManifest, previousManifest);
			var removedFiles = GetRemovedFiles (currentManifest, previousManifest);
			LogDiagnostic ($"FastDeploy2 manifest changed files: {changedFiles.Count}; removed files: {removedFiles.Count}.");

			foreach (var file in files) {
				if (changedFiles.Contains (file.RelativePath)) {
					LogDebugMessage ($"NotifySync CopyFile {file.RelativePath}.");
				} else {
					LogDebugMessage ($"NotifySync SkipCopyFile {file.RelativePath} file is up to date.");
				}
			}

			string output = await CreateRemoteStagingDirectories (remoteStagingPath, expectedFiles);
			if (!string.IsNullOrEmpty (output) && IsShellError (output, "mkdir")) {
				LogFastDeploy2Error ("XA0129", output, remoteStagingPath);
				return false;
			}

			if (!await RemoveRemoteStaleFiles (remoteStagingPath, removedFiles)) {
				return false;
			}

			if (!await UploadChangedFiles (remoteStagingPath, files, changedFiles)) {
				return false;
			}

			bool result;
			if (UseShellSymlinkAppFileTransfer ()) {
				result = await UpdateOverrideShellSymlinks (remoteStagingPath, overridePath, currentManifest, previousManifest, overrideSymlinksReady, removedFiles);
			} else {
				result = await UpdateOverrideCopies (remoteStagingPath, overridePath);
			}

			if (result) {
				string currentManifestHash = ComputeManifestHash (currentManifest);
				if (!await MarkRemoteManifest (remoteStagingPath, currentManifestHash)) {
					return false;
				}
				if (UseShellSymlinkAppFileTransfer () && !await MarkOverrideManifest (overridePath, currentManifestHash)) {
					return false;
				}
				WriteManifest (currentManifest);
			}
			return result;
		}

		bool UseShellSymlinkAppFileTransfer ()
		{
			return string.Equals (AppFileTransferMode, "Symlink", StringComparison.OrdinalIgnoreCase);
		}

		async Task<bool> UpdateOverrideShellSymlinks (string remoteStagingPath, string overridePath, ManifestData currentManifest, ManifestData previousManifest, bool overrideSymlinksReady, List<string> removedFiles)
		{
			var previousSymlinkManifest = overrideSymlinksReady ? previousManifest : null;
			var newFiles = previousSymlinkManifest == null ?
				new HashSet<string> (currentManifest.Files.Keys, StringComparer.Ordinal) :
				new HashSet<string> (currentManifest.Files.Keys.Where (file => !previousSymlinkManifest.Files.ContainsKey (file)), StringComparer.Ordinal);
			LogDiagnostic ($"FastDeploy2 symlink update new files: {newFiles.Count}; removed files: {removedFiles.Count}.");

			if (!await RunCombinedShellSymlinkUpdate (remoteStagingPath, overridePath, currentManifest, previousSymlinkManifest, newFiles, removedFiles)) {
				return await FallbackToCopy (remoteStagingPath, overridePath);
			}

			return true;
		}

		async Task<bool> RunCombinedShellSymlinkUpdate (string remoteStagingPath, string overridePath, ManifestData currentManifest, ManifestData previousManifest, HashSet<string> newFiles, List<string> removedFiles)
		{
			var currentByDirectory = GroupFilesByDirectory (currentManifest.Files.Keys);
			var newByDirectory = GroupFilesByDirectory (newFiles);
			var removedByDirectory = GroupFilesByDirectory (removedFiles);
			var directories = new HashSet<string> (currentByDirectory.Keys, StringComparer.Ordinal);
			directories.UnionWith (removedByDirectory.Keys);

			foreach (string directory in directories) {
				var currentInDirectory = GetFilesInDirectory (currentByDirectory, directory);
				var newInDirectory = GetFilesInDirectory (newByDirectory, directory);
				var removedInDirectory = GetFilesInDirectory (removedByDirectory, directory);
				string targetDirectory = CombineRemotePath (overridePath, directory);
				string sourceDirectory = CombineRemotePath (remoteStagingPath, directory);

				if (currentInDirectory.Count > 0 && (previousManifest == null || newInDirectory.Count == currentInDirectory.Count)) {
					// Clear and symlink only the files that live directly in this directory, never
					// the subdirectories. A plain `rm -rf ./*` + `ln -sf "$s"/* .` would (a) delete
					// child directories that other iterations populate and (b) create symlinks to
					// staging subdirectories; processing those children would then follow the symlink
					// back into the shell-owned staging area and fail with "Permission denied" under
					// run-as. Each subdirectory is handled by its own iteration instead.
					string script = $"d={QuoteShellArgument (targetDirectory)};s={QuoteShellArgument (sourceDirectory)};mkdir -p \"$d\"&&cd \"$d\"&&for e in ./*;do [ -d \"$e\" ]||rm -f \"$e\";done&&for f in \"$s\"/*;do [ -d \"$f\" ]||ln -sf \"$f\" .;done";
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

		static List<string> GetFilesInDirectory (Dictionary<string, List<string>> filesByDirectory, string directory)
		{
			return filesByDirectory.TryGetValue (directory, out List<string> files) ? files : [];
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
			LogDiagnostic ("FastDeploy2 symlink update failed; falling back to copy mode.");
			return await UpdateOverrideCopies (remoteStagingPath, overridePath, clearOverrideDirectory: true);
		}

		async Task<bool> UpdateOverrideCopies (string remoteStagingPath, string overridePath, bool clearOverrideDirectory = false)
		{
			if (clearOverrideDirectory) {
				if (!await ClearOverrideDirectory (overridePath)) {
					return false;
				}
			} else if (!await ClearOverrideSymlinkState (overridePath)) {
				return false;
			}

			var stagedFileData = await GetRemoteFileData (remoteStagingPath, runAs: false);
			if (stagedFileData == null) {
				return false;
			}
			stagedFileData.Remove (ManifestHashMarker);

			var overrideFileData = await GetRemoteFileData (overridePath, runAs: true);
			if (overrideFileData == null) {
				return false;
			}

			if (!await RemoveStaleOverrideFiles (overridePath, stagedFileData, overrideFileData)) {
				return false;
			}

			return await CopyChangedFiles (remoteStagingPath, overridePath, stagedFileData, overrideFileData);
		}

		ManifestData CreateManifest (List<DirectPushFile> files)
		{
			var manifest = new ManifestData {
				DeviceId = GetDeviceId (),
				PackageName = PackageName,
				UserId = GetUserId (),
				PrimaryCpuAbi = PrimaryCpuAbi,
				Files = new Dictionary<string, ManifestEntry> (StringComparer.Ordinal),
			};
			foreach (var file in files) {
				var info = new FileInfo (file.LocalPath);
				manifest.Files [file.RelativePath] = new ManifestEntry {
					RelativePath = file.RelativePath,
					LocalPath = file.LocalPath,
					Size = info.Length,
					LastWriteTimeUtcTicks = info.LastWriteTimeUtc.Ticks,
				};
			}
			return manifest;
		}

		HashSet<string> GetChangedFiles (ManifestData currentManifest, ManifestData previousManifest)
		{
			if (previousManifest == null) {
				return new HashSet<string> (currentManifest.Files.Keys, StringComparer.Ordinal);
			}

			var changedFiles = new HashSet<string> (StringComparer.Ordinal);
			foreach (var entry in currentManifest.Files) {
				if (!previousManifest.Files.TryGetValue (entry.Key, out ManifestEntry previous) ||
						previous.Size != entry.Value.Size ||
						previous.LastWriteTimeUtcTicks != entry.Value.LastWriteTimeUtcTicks) {
					changedFiles.Add (entry.Key);
				}
			}
			return changedFiles;
		}

		List<string> GetRemovedFiles (ManifestData currentManifest, ManifestData previousManifest)
		{
			var removedFiles = new List<string> ();
			if (previousManifest == null) {
				return removedFiles;
			}

			foreach (var entry in previousManifest.Files.Keys) {
				if (!currentManifest.Files.ContainsKey (entry)) {
					removedFiles.Add (entry);
				}
			}
			return removedFiles;
		}

		async Task<bool> UploadChangedFiles (string remoteStagingPath, List<DirectPushFile> files, HashSet<string> changedFiles)
		{
			var changedFileList = files.Where (file => changedFiles.Contains (file.RelativePath)).ToList ();
			foreach (var group in changedFileList.GroupBy (file => GetDirectoryName (file.RelativePath), StringComparer.Ordinal)) {
				string remoteDirectory = CombineRemotePath (remoteStagingPath, group.Key);
				foreach (var batch in BatchPushFilesWithoutSync (group.ToList (), remoteDirectory)) {
					var result = await RunAdbCommand (batch.ToArray ());
					if (result.ExitCode != 0) {
						LogFastDeploy2Error ("XA0129", result.Output, remoteDirectory);
						return false;
					}
				}
			}
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
					yield return CreatePushArgs (file.LocalPath, CombineRemotePath (remoteDirectory, Path.GetFileName (file.RelativePath)));
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

		async Task<DeviceManifestState> GetDeviceManifestState (string remoteStagingPath, string overridePath)
		{
			string remoteMarkerPath = CombineRemotePath (remoteStagingPath, ManifestHashMarker);
			string overrideMarkerPath = CombineRemotePath (overridePath, ManifestHashMarker);
			string runAsCommand = string.Join (" ", BuildRunAsArgs ().Concat (new [] {
				"sh",
				"-c",
				$"cat {QuoteShellArgument (overrideMarkerPath)} 2>/dev/null || true"
			}).Select (QuoteShellArgument));
			string script = $"printf 'remote='; cat {QuoteShellArgument (remoteMarkerPath)} 2>/dev/null || true; printf '\\noverride='; {runAsCommand} 2>/dev/null || true; printf '\\n'";
			var result = await RunAdbShellCommand (script);
			return ParseDeviceManifestState (result.Output);
		}

		async Task<bool> ClearOverrideSymlinkState (string overridePath)
		{
			string markerPath = CombineRemotePath (overridePath, ManifestHashMarker);
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

		async Task<bool> MarkRemoteManifest (string remoteStagingPath, string manifestHash)
		{
			string markerPath = CombineRemotePath (remoteStagingPath, ManifestHashMarker);
			var result = await RunAdbShellCommand ($"printf %s {QuoteShellArgument (manifestHash)} > {QuoteShellArgument (markerPath)}");
			if (result.ExitCode != 0 || IsShellError (result.Output, "printf")) {
				LogFastDeploy2Error ("XA0129", result.Output, markerPath);
				return false;
			}
			return true;
		}

		async Task<bool> MarkOverrideManifest (string overridePath, string manifestHash)
		{
			string output = await RunAsShell ($"mkdir -p {QuoteShellArgument (overridePath)}; printf %s {QuoteShellArgument (manifestHash)} > {QuoteShellArgument (CombineRemotePath (overridePath, ManifestHashMarker))}");
			if (RaiseRunAsError (output) || IsShellError (output, "mkdir") || IsShellError (output, "printf")) {
				LogFastDeploy2Error ("XA0129", output, overridePath);
				return false;
			}
			return true;
		}

		static DeviceManifestState ParseDeviceManifestState (string output)
		{
			var state = new DeviceManifestState ();
			foreach (string line in output.Split (new char [] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
				if (line.StartsWith ("remote=", StringComparison.Ordinal)) {
					state.RemoteHash = line.Substring ("remote=".Length).Trim ();
				} else if (line.StartsWith ("override=", StringComparison.Ordinal)) {
					state.OverrideHash = line.Substring ("override=".Length).Trim ();
				}
			}
			return state;
		}

		ManifestData LoadPreviousManifest ()
		{
			string manifestFile = GetManifestFilePath ();
			if (!File.Exists (manifestFile)) {
				return null;
			}

			try {
				var manifest = JsonSerializer.Deserialize (File.ReadAllText (manifestFile), typeof (ManifestData), FastDeploy2JsonSerializerContext.Default) as ManifestData;
				return IsManifestForCurrentTarget (manifest) ? manifest : null;
			} catch (Exception ex) {
				LogDiagnostic ($"Ignoring FastDeploy2 manifest '{manifestFile}'. {ex}");
				return null;
			}
		}

		void WriteManifest (ManifestData manifest)
		{
			string manifestFile = GetManifestFilePath ();
			Directory.CreateDirectory (Path.GetDirectoryName (manifestFile));
			File.WriteAllText (manifestFile, JsonSerializer.Serialize (manifest, typeof (ManifestData), FastDeploy2JsonSerializerContext.Default));
		}

		bool IsManifestForCurrentTarget (ManifestData manifest)
		{
			return manifest != null &&
				string.Equals (manifest.DeviceId, GetDeviceId (), StringComparison.Ordinal) &&
				string.Equals (manifest.PackageName, PackageName, StringComparison.Ordinal) &&
				string.Equals (manifest.UserId, GetUserId (), StringComparison.Ordinal) &&
				string.Equals (manifest.PrimaryCpuAbi, PrimaryCpuAbi, StringComparison.Ordinal) &&
				manifest.Files != null;
		}

		static string ComputeManifestHash (ManifestData manifest)
		{
			using (var hash = SHA256.Create ()) {
				byte [] bytes = Encoding.UTF8.GetBytes (GetCanonicalManifestText (manifest));
				return BitConverter.ToString (hash.ComputeHash (bytes)).Replace ("-", "").ToLowerInvariant ();
			}
		}

		static string GetCanonicalManifestText (ManifestData manifest)
		{
			var builder = new StringBuilder ();
			builder.AppendLine (manifest.DeviceId ?? "");
			builder.AppendLine (manifest.PackageName ?? "");
			builder.AppendLine (manifest.UserId ?? "");
			builder.AppendLine (manifest.PrimaryCpuAbi ?? "");
			foreach (var entry in manifest.Files.OrderBy (entry => entry.Key, StringComparer.Ordinal)) {
				builder.Append (entry.Key).Append ('\t')
					.Append (entry.Value.LocalPath).Append ('\t')
					.Append (entry.Value.RelativePath).Append ('\t')
					.Append (entry.Value.Size).Append ('\t')
					.AppendLine (entry.Value.LastWriteTimeUtcTicks.ToString ());
			}
			return builder.ToString ();
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

		class DeviceManifestState {
			public string RemoteHash { get; set; } = "";
			public string OverrideHash { get; set; } = "";
		}

		internal class ManifestData {
			[JsonPropertyName ("deviceId")]
			public string DeviceId { get; set; }

			[JsonPropertyName ("packageName")]
			public string PackageName { get; set; }

			[JsonPropertyName ("userId")]
			public string UserId { get; set; }

			[JsonPropertyName ("primaryCpuAbi")]
			public string PrimaryCpuAbi { get; set; }

			[JsonPropertyName ("files")]
			public Dictionary<string, ManifestEntry> Files { get; set; }
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
