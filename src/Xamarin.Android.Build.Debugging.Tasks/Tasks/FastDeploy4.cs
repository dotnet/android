using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class FastDeploy4 : FastDeploy2
	{
		const string RemoteStagingRootPath = "/tmp/fastdeploy4";
		const int CopyBatchSize = 25;
		const int RemoveBatchSize = 100;
		const int MaxAdbCommandLength = 4096;

		public override string TaskPrefix => "FD4";

		public string PushMode { get; set; } = "SingleFile";

		protected override string RemoteStagingRoot => RemoteStagingRootPath;

		protected override async Task<bool> DeployFastDevFilesWithAdbPush (string overridePath)
		{
			string remoteStagingPath = GetRemoteAdbPushStagingPath ();
			var phase = Stopwatch.StartNew ();
			var files = PrepareDirectPushFiles ();
			var expectedFiles = new HashSet<string> (files.Select (file => file.RelativePath), StringComparer.Ordinal);
			SetDiagnosticElapsed ("deploy.fastdeploy2.local.stage.ms", phase);
			if (files.Count == 0) {
				LogDiagnostic ("No FastDev files were prepared for direct adb push deployment.");
				return true;
			}

			phase.Restart ();
			if (!await CreateRemoteStagingDirectories (remoteStagingPath, files)) {
				return false;
			}
			SetDiagnosticElapsed ("deploy.fastdeploy2.remote.mkdir.ms", phase);

			if (!await RemoveStaleRemoteStagingFiles (remoteStagingPath, expectedFiles)) {
				return false;
			}

			var pushMode = PushMode ?? "";
			HashSet<string> changedFiles;
			if (string.Equals (pushMode, "SingleFile", StringComparison.OrdinalIgnoreCase)) {
				phase.Restart ();
				changedFiles = await PushFilesOneByOne (remoteStagingPath, files);
				SetDiagnosticElapsed ("deploy.fastdeploy2.upload.ms", phase);
			} else if (string.Equals (pushMode, "Bulk", StringComparison.OrdinalIgnoreCase)) {
				phase.Restart ();
				if (!await PushFilesInBulk (remoteStagingPath, files)) {
					return false;
				}
				SetDiagnosticElapsed ("deploy.fastdeploy2.upload.ms", phase);

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

				if (!await RemoveStaleOverrideFiles (overridePath, stagedFileData.Keys, overrideFileData.Keys)) {
					return false;
				}

				phase.Restart ();
				changedFiles = GetChangedFiles (stagedFileData, overrideFileData);
				SetDiagnosticElapsed ("deploy.fastdeploy2.compare.ms", phase);
			} else {
				LogFastDeploy2Error ("XA0129", $"Invalid FastDeploy4 PushMode '{PushMode}'. Supported values are 'SingleFile' and 'Bulk'.", PushMode);
				return false;
			}

			SetDiagnosticProperty ("deploy.fastdeploy4.push.mode", pushMode);

			phase.Restart ();
			var overrideFiles = await GetOverrideFileList (overridePath);
			SetDiagnosticElapsed ("deploy.fastdeploy3.override.list.ms", phase);
			if (overrideFiles == null) {
				return false;
			}

			phase.Restart ();
			int missingFiles = 0;
			foreach (string file in expectedFiles) {
				if (!overrideFiles.Contains (file) && changedFiles.Add (file)) {
					missingFiles++;
				}
			}
			SetDiagnosticElapsed ("deploy.fastdeploy2.compare.ms", phase);
			SetDiagnosticProperty ("deploy.fastdeploy3.missing.files", missingFiles);

			if (!await RemoveStaleOverrideFiles (overridePath, expectedFiles, overrideFiles)) {
				return false;
			}

			return await CopyChangedFiles (remoteStagingPath, overridePath, changedFiles);
		}

		new List<DirectPushFile> PrepareDirectPushFiles ()
		{
			var files = new List<DirectPushFile> ();
			foreach (var file in FastDevFiles ?? Array.Empty<Microsoft.Build.Framework.ITaskItem> ()) {
				if (Path.GetExtension (file.ItemSpec) == ".so") {
					string abi = AndroidRidAbiHelper.GetNativeLibraryAbi (file);
					if (abi != PrimaryCpuAbi) {
						LogDebugMessage ($"NotifySync SkipCopyFile {file.ItemSpec} abi not suitable for this device.");
						continue;
					}
				}

				files.Add (new DirectPushFile {
					LocalPath = file.ItemSpec,
					RelativePath = GetAdbPushTargetPath (file),
				});
				LogDiagnostic ($"Prepared {file.ItemSpec} => {files [files.Count - 1].RelativePath}");
			}

			if (EnvironmentFiles?.Length > 0) {
				byte [] environmentData = CreateEnvironmentFileData (EnvironmentFiles, out DateTime newestFileDateTime);
				if (environmentData.Length > 0) {
					string environmentFile = Path.Combine (GetFullPath (IntermediateOutputPath), "fastdeploy4", PrimaryCpuAbi, "environment");
					WriteFileIfChanged (environmentFile, environmentData, newestFileDateTime);
					files.Add (new DirectPushFile {
						LocalPath = environmentFile,
						RelativePath = $"{PrimaryCpuAbi}/environment",
					});
				}
			}

			return files;
		}

		async Task<bool> CreateRemoteStagingDirectories (string remoteStagingPath, List<DirectPushFile> files)
		{
			var directories = new HashSet<string> (StringComparer.Ordinal) { remoteStagingPath };
			foreach (var file in files) {
				string directory = Path.GetDirectoryName (file.RelativePath)?.Replace ("\\", "/") ?? "";
				if (!string.IsNullOrEmpty (directory)) {
					directories.Add ($"{remoteStagingPath}/{directory}");
				}
			}

			foreach (var batch in BatchArguments ("mkdir", "-p", directories)) {
				var result = await RunAdbCommand (new [] { "shell" }.Concat (batch).ToArray ());
				if (result.ExitCode != 0 || IsShellError (result.Output, "mkdir")) {
					LogFastDeploy2Error ("XA0129", result.Output, remoteStagingPath);
					return false;
				}
			}
			return true;
		}

		async Task<HashSet<string>> PushFilesOneByOne (string remoteStagingPath, List<DirectPushFile> files)
		{
			var changedFiles = new HashSet<string> (StringComparer.Ordinal);
			int pushed = 0;
			int skipped = 0;
			foreach (var file in files) {
				var args = CreatePushArgs (file.LocalPath, $"{remoteStagingPath}/{file.RelativePath}");
				var result = await RunAdbCommand (args.ToArray ());
				if (result.ExitCode != 0) {
					LogFastDeploy2Error ("XA0129", result.Output, file.LocalPath);
					return null;
				}
				var counts = TryParsePushSummary (result.Output);
				pushed += counts.pushed;
				skipped += counts.skipped;
				if (counts.pushed > 0) {
					changedFiles.Add (file.RelativePath);
				}
				LogDiagnostic (result.Output);
			}
			SetDiagnosticProperty ("deploy.fastdeploy2.adb.pushed.files", pushed);
			SetDiagnosticProperty ("deploy.fastdeploy2.adb.skipped.files", skipped);
			SetDiagnosticProperty ("deploy.fastdeploy4.direct.push.files", files.Count);
			return changedFiles;
		}

		async Task<bool> PushFilesInBulk (string remoteStagingPath, List<DirectPushFile> files)
		{
			int pushed = 0;
			int skipped = 0;
			int batches = 0;
			foreach (var group in files.GroupBy (file => Path.GetDirectoryName (file.RelativePath)?.Replace ("\\", "/") ?? "", StringComparer.Ordinal)) {
				string remoteDirectory = string.IsNullOrEmpty (group.Key) ? remoteStagingPath : $"{remoteStagingPath}/{group.Key}";
				foreach (var batch in BatchPushFiles (group.ToList (), remoteDirectory)) {
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
			SetDiagnosticProperty ("deploy.fastdeploy4.bulk.batches", batches);
			return true;
		}

		HashSet<string> GetChangedFiles (Dictionary<string, RemoteFileInfo> stagedFiles, Dictionary<string, RemoteFileInfo> overrideFiles)
		{
			var changedFiles = new HashSet<string> (StringComparer.Ordinal);
			foreach (var file in stagedFiles) {
				if (!overrideFiles.TryGetValue (file.Key, out RemoteFileInfo existing) ||
						existing.Size != file.Value.Size ||
						existing.ModifiedTime != file.Value.ModifiedTime) {
					changedFiles.Add (file.Key);
				}
			}
			return changedFiles;
		}

		async Task<HashSet<string>> GetOverrideFileList (string overridePath)
		{
			string output = await RunAs ("find", overridePath, "-type", "f");
			if (RaiseRunAsError (output)) {
				return null;
			}
			if (IsMissingDirectoryError (output)) {
				return new HashSet<string> (StringComparer.Ordinal);
			}
			if (IsShellError (output, "find")) {
				LogFastDeploy2Error ("XA0129", output, overridePath);
				return null;
			}

			var files = new HashSet<string> (StringComparer.Ordinal);
			string prefix = overridePath.TrimEnd ('/') + "/";
			foreach (string line in output.Split (new char [] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
				string remoteFile = line.Trim ();
				if (remoteFile.StartsWith (prefix, StringComparison.Ordinal)) {
					files.Add (remoteFile.Substring (prefix.Length));
				}
			}
			return files;
		}

		async Task<bool> RemoveStaleOverrideFiles (string overridePath, IEnumerable<string> stagedFiles, IEnumerable<string> overrideFiles)
		{
			var phase = Stopwatch.StartNew ();
			var staged = new HashSet<string> (stagedFiles, StringComparer.Ordinal);
			var staleFiles = new List<string> ();
			foreach (var file in overrideFiles) {
				if (!staged.Contains (file)) {
					staleFiles.Add ($"{overridePath}/{file}");
				}
			}

			SetDiagnosticProperty ("deploy.fastdeploy2.stale.files", staleFiles.Count);
			foreach (var batch in BatchArguments ("rm", "-f", staleFiles)) {
				string output = await RunAs (batch.ToArray ());
				if (RaiseRunAsError (output) || IsShellError (output, "rm")) {
					LogFastDeploy2Error ("XA0129", output, overridePath);
					return false;
				}
			}
			SetDiagnosticElapsed ("deploy.fastdeploy2.stale.remove.ms", phase);
			return true;
		}

		async Task<bool> CopyChangedFiles (string remoteStagingPath, string overridePath, HashSet<string> changedFiles)
		{
			SetDiagnosticProperty ("deploy.fastdeploy2.changed.files", changedFiles.Count);
			var filesByDirectory = new Dictionary<string, List<string>> (StringComparer.Ordinal);
			foreach (string file in changedFiles) {
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
					LogFastDeploy2Error ("XA0129", output, targetDirectory);
					return false;
				}

				for (int i = 0; i < group.Value.Count; i += CopyBatchSize) {
					var args = new List<string> { "cp", "-p" };
					foreach (string file in group.Value.Skip (i).Take (CopyBatchSize)) {
						args.Add ($"{remoteStagingPath}/{file}");
					}
					args.Add (targetDirectory);
					phase.Restart ();
					output = await RunAs (args.ToArray ());
					AddDiagnosticElapsed ("deploy.fastdeploy2.override.copy.ms", phase);
					if (RaiseRunAsError (output) || IsShellError (output, "cp")) {
						LogFastDeploy2Error ("XA0129", output, targetDirectory);
						return false;
					}
				}
			}
			return true;
		}

		List<string> CreatePushArgs (string localPath, string remotePath)
		{
			var args = new List<string> { "push" };
			if (!string.IsNullOrEmpty (AdbPushCompressionAlgorithm)) {
				args.Add ("-z");
				args.Add (AdbPushCompressionAlgorithm);
			}
			args.Add ("--sync");
			args.Add (localPath);
			args.Add (remotePath);
			return args;
		}

		IEnumerable<List<string>> BatchPushFiles (List<DirectPushFile> files, string remoteDirectory)
		{
			var batch = CreatePushArgsPrefix ();
			int length = EstimateCommandLength (batch) + remoteDirectory.Length + 4;
			foreach (var file in files) {
				if (Path.GetFileName (file.LocalPath) != Path.GetFileName (file.RelativePath)) {
					var single = CreatePushArgs (file.LocalPath, $"{remoteDirectory}/{Path.GetFileName (file.RelativePath)}");
					yield return single;
					continue;
				}

				int itemLength = file.LocalPath.Length + 3;
				if (batch.Count > 3 && length + itemLength >= MaxAdbCommandLength) {
					batch.Add (remoteDirectory);
					yield return batch;
					batch = CreatePushArgsPrefix ();
					length = EstimateCommandLength (batch) + remoteDirectory.Length + 4;
				}
				batch.Add (file.LocalPath);
				length += itemLength;
			}
			if (batch.Count > 3) {
				batch.Add (remoteDirectory);
				yield return batch;
			}
		}

		List<string> CreatePushArgsPrefix ()
		{
			var args = new List<string> { "push" };
			if (!string.IsNullOrEmpty (AdbPushCompressionAlgorithm)) {
				args.Add ("-z");
				args.Add (AdbPushCompressionAlgorithm);
			}
			args.Add ("--sync");
			return args;
		}

		IEnumerable<List<string>> BatchArguments (string command, string option, IEnumerable<string> values)
		{
			var batch = new List<string> { command, option };
			int length = command.Length + option.Length + 2;
			foreach (var value in values) {
				int itemLength = value.Length + 3;
				if (batch.Count > 2 && length + itemLength >= MaxAdbCommandLength) {
					yield return batch;
					batch = new List<string> { command, option };
					length = command.Length + option.Length + 2;
				}
				batch.Add (value);
				length += itemLength;
			}
			if (batch.Count > 2) {
				yield return batch;
			}
		}

		int EstimateCommandLength (List<string> args)
		{
			int length = 0;
			foreach (var arg in args) {
				length += arg.Length + 3;
			}
			return length;
		}

		(int pushed, int skipped) TryParsePushSummary (string output)
		{
			int pushed = 0;
			int skipped = 0;
			foreach (var line in output.Split (new char [] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
				var match = System.Text.RegularExpressions.Regex.Match (line, @"(?<pushed>\d+) files? pushed, (?<skipped>\d+) skipped");
				if (!match.Success) {
					continue;
				}
				pushed = int.Parse (match.Groups ["pushed"].Value);
				skipped = int.Parse (match.Groups ["skipped"].Value);
			}
			return (pushed, skipped);
		}

		string GetAdbPushTargetPath (Microsoft.Build.Framework.ITaskItem file)
		{
			string targetPath = file.GetMetadata ("TargetPath");
			if (string.IsNullOrEmpty (targetPath)) {
				LogDiagnostic ($"'TargetPath' meta data not found on '{file.ItemSpec}'. Falling back to'DestinationSubPath'");
				targetPath = file.GetMetadata ("DestinationSubPath");
			}
			if (!string.IsNullOrEmpty (targetPath)) {
				return targetPath.Replace ("\\", "/");
			}
			return Path.GetFileName (file.ItemSpec);
		}

		byte [] CreateEnvironmentFileData (Microsoft.Build.Framework.ITaskItem [] environments, out DateTime newestFileDateTime)
		{
			int maxKeyLength = 0;
			int maxValueLength = 0;
			newestFileDateTime = DateTime.MinValue;
			var data = new Dictionary<string, string> ();
			foreach (var env in environments ?? Array.Empty<Microsoft.Build.Framework.ITaskItem> ()) {
				if (!File.Exists (env.ItemSpec))
					continue;
				DateTime modifiedDateTime = File.GetLastWriteTimeUtc (env.ItemSpec);
				if (modifiedDateTime > newestFileDateTime)
					newestFileDateTime = modifiedDateTime;
				foreach (string line in File.ReadLines (env.ItemSpec)) {
					if (string.IsNullOrEmpty (line))
						continue;
					int index = line.IndexOf ('=');
					if (index == -1) {
						LogDebugMessage ($"Skipping invalid environment line: {line}");
						continue;
					}
					var key = line.Substring (0, index);
					var value = line.Substring (index + 1);
					maxKeyLength = Math.Max (maxKeyLength, key.Length);
					maxValueLength = Math.Max (maxValueLength, value.Length);
					data [key] = value;
				}
			}

			if (newestFileDateTime == DateTime.MinValue) {
				return Array.Empty<byte> ();
			}

			maxKeyLength++;
			maxValueLength++;

			using (var stream = new MemoryStream ())
			using (var binaryWriter = new BinaryWriter (stream, Encoding.ASCII)) {
				binaryWriter.Write (Encoding.ASCII.GetBytes ("0x" + maxKeyLength.ToString ("X8") + '\0'));
				binaryWriter.Write (Encoding.ASCII.GetBytes ("0x" + maxValueLength.ToString ("X8") + '\0'));
				foreach (var kvp in data) {
					binaryWriter.Write (Encoding.ASCII.GetBytes (kvp.Key.PadRight (maxKeyLength, '\0')));
					binaryWriter.Write (Encoding.ASCII.GetBytes (kvp.Value.PadRight (maxValueLength, '\0')));
				}
				binaryWriter.Flush ();
				return stream.ToArray ();
			}
		}

		new class DirectPushFile
		{
			public string LocalPath { get; set; }
			public string RelativePath { get; set; }
		}
	}
}
