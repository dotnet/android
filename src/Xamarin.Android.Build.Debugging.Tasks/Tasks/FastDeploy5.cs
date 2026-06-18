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
	public class FastDeploy5 : FastDeploy2
	{
		const string RemoteStagingRootPath = "/tmp/fastdeploy5";
		const string RemoteReadyMarker = ".fastdeploy5-ready";
		const int MaxAdbCommandLength = 4096;

		public override string TaskPrefix => "FD5";

		protected override string RemoteStagingRoot => RemoteStagingRootPath;

		protected override async Task<bool> DeployFastDevFilesWithAdbPush (string overridePath)
		{
			var phase = Stopwatch.StartNew ();
			var files = PrepareDirectPushFiles ();
			var expectedFiles = new HashSet<string> (files.Select (file => file.RelativePath), StringComparer.Ordinal);
			var currentManifest = CreateManifest (files);
			SetDiagnosticElapsed ("deploy.fastdeploy2.local.stage.ms", phase);
			if (files.Count == 0) {
				LogDiagnostic ("No FastDev files were prepared for manifest adb push deployment.");
				return true;
			}

			string remoteStagingPath = GetRemoteAdbPushStagingPath ();
			bool remoteReady = await IsRemoteReady (remoteStagingPath);
			var previousManifest = remoteReady ? LoadPreviousManifest () : null;
			if (previousManifest == null) {
				SetDiagnosticProperty ("deploy.fastdeploy5.full.push", 1);
			}

			var changedFiles = GetChangedFiles (currentManifest, previousManifest);
			var removedFiles = GetRemovedFiles (currentManifest, previousManifest);
			SetDiagnosticProperty ("deploy.fastdeploy5.manifest.changed.files", changedFiles.Count);
			SetDiagnosticProperty ("deploy.fastdeploy5.manifest.removed.files", removedFiles.Count);

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
			if (UseSymlinkAppFileTransfer ()) {
				result = await UpdateOverrideSymlinks (remoteStagingPath, overridePath, expectedFiles);
			} else {
				phase.Restart ();
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

				result = await CopyChangedFiles (remoteStagingPath, overridePath, stagedFileData, overrideFileData);
			}

			if (result) {
				WriteManifest (currentManifest);
				await MarkRemoteReady (remoteStagingPath);
			}
			return result;
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
				LogDiagnostic ($"Ignoring FastDeploy5 manifest '{manifestFile}'. {ex}");
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
			return Path.Combine (GetFullPath (IntermediateOutputPath), "fastdeploy5", GetSafeFileName (PackageName), GetSafeFileName (GetUserId ()), GetSafeFileName (PrimaryCpuAbi), "manifest.json");
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
