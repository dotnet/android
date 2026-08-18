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
			var currentManifest = CreateManifest (files);
			if (files.Count == 0) {
				LogDiagnostic ("No FastDev files were prepared for adb push deployment.");
				return true;
			}

			string remoteStagingPath = GetRemoteAdbPushStagingPath ();
			var previousManifest = LoadPreviousManifest ();
			string previousManifestHash = previousManifest == null ? "" : ComputeManifestHash (previousManifest);
			DeviceManifestState deviceManifestState;
			if (previousManifest == null) {
				deviceManifestState = new DeviceManifestState ();
			} else if (warmDeviceManifestState != null && string.Equals (warmDeviceManifestHash, previousManifestHash, StringComparison.Ordinal)) {
				deviceManifestState = warmDeviceManifestState;
			} else {
				deviceManifestState = await GetDeviceManifestState (overridePath);
			}
			bool overrideReady =
				!ResetOverrideDirectory &&
				previousManifest != null &&
				string.Equals (deviceManifestState.OverrideHash, previousManifestHash, StringComparison.Ordinal);
			if (!overrideReady) {
				previousManifest = null;
				if (!await ClearOverrideDirectory (overridePath)) {
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

			if (!await ResetRemoteStagingDirectory (remoteStagingPath)) {
				return false;
			}

			bool deployed;
			bool stagingRemoved;
			try {
				deployed = await DeployStagedFiles (
					remoteStagingPath,
					overridePath,
					files,
					changedFiles,
					removedFiles,
					currentManifest);
			} finally {
				stagingRemoved = await ResetRemoteStagingDirectory (remoteStagingPath);
			}

			return deployed && stagingRemoved;
		}

		async Task<bool> DeployStagedFiles (
			string remoteStagingPath,
			string overridePath,
			List<DirectPushFile> files,
			HashSet<string> changedFiles,
			List<string> removedFiles,
			ManifestData currentManifest)
		{
			if (changedFiles.Count > 0) {
				string output = await CreateRemoteStagingDirectories (remoteStagingPath, changedFiles);
				if (!string.IsNullOrEmpty (output) && IsShellError (output, "mkdir")) {
					LogFastDeploy2Error ("XA0129", output, remoteStagingPath);
					return false;
				}

				UploadFilesResult uploadResult = await UploadChangedFiles (remoteStagingPath, files, changedFiles);
				if (!uploadResult.Success) {
					LogFastDeploy2Error ("XA0129", uploadResult.Output, uploadResult.RemoteDirectory);
					return false;
				}
			}

			if (!await RemoveStaleOverrideFiles (overridePath, removedFiles)) {
				return false;
			}
			if (!await CopyChangedFiles (remoteStagingPath, overridePath, changedFiles)) {
				return false;
			}

			string currentManifestHash = ComputeManifestHash (currentManifest);
			if (!await MarkOverrideManifest (overridePath, currentManifestHash)) {
				return false;
			}

			WriteManifest (currentManifest);
			return true;
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

		async Task<UploadFilesResult> UploadChangedFiles (string remoteStagingPath, List<DirectPushFile> files, HashSet<string> changedFiles)
		{
			var changedFileList = files.Where (file => changedFiles.Contains (file.RelativePath)).ToList ();
			foreach (var group in changedFileList.GroupBy (file => GetDirectoryName (file.RelativePath), StringComparer.Ordinal)) {
				string remoteDirectory = CombineRemotePath (remoteStagingPath, group.Key);
				foreach (var batch in BatchPushFilesWithoutSync (group.ToList (), remoteDirectory)) {
					var result = await RunAdbCommand (batch.ToArray ());
					if (result.ExitCode != 0) {
						return new UploadFilesResult (
							success: false,
							output: result.Output,
							remoteDirectory: remoteDirectory);
					}
				}
			}
			return new UploadFilesResult (success: true, output: "", remoteDirectory: "");
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

		async Task<DeviceManifestState> GetDeviceManifestState (string overridePath)
		{
			string overrideMarkerPath = CombineRemotePath (overridePath, ManifestHashMarker);
			string output = await RunAsShell ($"cat {QuoteShellArgument (overrideMarkerPath)} 2>/dev/null || true");
			return new DeviceManifestState { OverrideHash = output.Trim () };
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

		async Task<bool> MarkOverrideManifest (string overridePath, string manifestHash)
		{
			string markerPath = CombineRemotePath (overridePath, ManifestHashMarker);
			string output = await RunAsShell (
				$"mkdir -p {QuoteShellArgument (overridePath)} && " +
				$"printf %s {QuoteShellArgument (manifestHash)} > {QuoteShellArgument (markerPath)}");
			if (RaiseRunAsError (output) ||
					IsShellError (output, "mkdir") ||
					IsShellError (output, "printf")) {
				LogFastDeploy2Error ("XA0129", output, markerPath);
				return false;
			}
			return true;
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
			public string OverrideHash { get; set; } = "";
		}

		readonly struct UploadFilesResult {
			public bool Success { get; }
			public string Output { get; }
			public string RemoteDirectory { get; }

			public UploadFilesResult (bool success, string output, string remoteDirectory)
			{
				Success = success;
				Output = output;
				RemoteDirectory = remoteDirectory;
			}
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

			[JsonPropertyName ("size")]
			public long Size { get; set; }

			[JsonPropertyName ("lastWriteTimeUtcTicks")]
			public long LastWriteTimeUtcTicks { get; set; }
		}
	}
}
