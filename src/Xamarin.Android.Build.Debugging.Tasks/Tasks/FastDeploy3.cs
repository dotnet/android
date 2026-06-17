using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Xamarin.Android.Tasks
{
	public class FastDeploy3 : FastDeploy2
	{
		const string RemoteDataLocalTmpRoot = "/data/local/tmp/fastdeploy3";
		const int CopyBatchSize = 25;
		const int StaleFileRemovalBatchSize = 100;

		public override string TaskPrefix => "FD3";

		protected override string RemoteStagingRoot => RemoteDataLocalTmpRoot;

		protected override string GetLocalStagingDirectory ()
		{
			return Path.Combine (GetAndroidProductOutDirectory (), "data", "local", "tmp", "fastdeploy3", PackageName, GetUserId ());
		}

		protected override string GetRemoteAdbPushStagingPath ()
		{
			return $"{RemoteDataLocalTmpRoot}/{PackageName}/{GetUserId ()}";
		}

		protected override async Task<bool> DeployFastDevFilesWithAdbPush (string overridePath)
		{
			string stagingDirectory = GetLocalStagingDirectory ();
			var phase = Stopwatch.StartNew ();
			var stagedFiles = PrepareAdbPushStagingDirectory (stagingDirectory);
			SetDiagnosticElapsed ("deploy.fastdeploy2.local.stage.ms", phase);
			if (stagedFiles.Count == 0) {
				LogDiagnostic ("No FastDev files were staged for adb sync deployment.");
				return true;
			}

			string remoteStagingPath = GetRemoteAdbPushStagingPath ();
			phase.Restart ();
			var mkdirResult = await RunAdbCommand ("shell", "mkdir", "-p", remoteStagingPath);
			string output = mkdirResult.Output;
			SetDiagnosticElapsed ("deploy.fastdeploy2.remote.mkdir.ms", phase);
			if (mkdirResult.ExitCode != 0 || IsShellError (output, "mkdir")) {
				LogFastDeploy2Error ("XA0129", output, remoteStagingPath);
				return false;
			}

			if (!await RemoveStaleRemoteStagingFiles (remoteStagingPath, stagedFiles)) {
				return false;
			}

			phase.Restart ();
			var syncChangedFiles = await GetChangedFilesFromSyncList (remoteStagingPath);
			SetDiagnosticElapsed ("deploy.fastdeploy3.sync.list.ms", phase);
			if (syncChangedFiles == null) {
				return false;
			}
			SetDiagnosticProperty ("deploy.fastdeploy3.sync.list.files", syncChangedFiles.Count);

			phase.Restart ();
			if (!await UploadStagingDirectory (stagingDirectory, remoteStagingPath)) {
				return false;
			}
			SetDiagnosticElapsed ("deploy.fastdeploy2.upload.ms", phase);

			phase.Restart ();
			var overrideFiles = await GetOverrideFileList (overridePath);
			SetDiagnosticElapsed ("deploy.fastdeploy2.override.stat.ms", phase);
			SetDiagnosticElapsed ("deploy.fastdeploy3.override.list.ms", phase);
			if (overrideFiles == null) {
				return false;
			}

			phase.Restart ();
			var changedFiles = new HashSet<string> (syncChangedFiles, StringComparer.Ordinal);
			int missingFiles = 0;
			foreach (var file in stagedFiles) {
				if (!overrideFiles.Contains (file) && changedFiles.Add (file)) {
					missingFiles++;
				}
			}
			SetDiagnosticElapsed ("deploy.fastdeploy2.compare.ms", phase);
			SetDiagnosticProperty ("deploy.fastdeploy3.missing.files", missingFiles);

			if (!await RemoveStaleOverrideFiles (overridePath, stagedFiles, overrideFiles)) {
				return false;
			}

			return await CopyChangedFiles (remoteStagingPath, overridePath, changedFiles);
		}

		protected override async Task<bool> UploadStagingDirectory (string stagingDirectory, string remoteStagingPath)
		{
			var args = new List<string> { "sync" };
			if (!string.IsNullOrEmpty (AdbPushCompressionAlgorithm)) {
				args.Add ("-z");
				args.Add (AdbPushCompressionAlgorithm);
			}
			args.Add ("data");

			var environmentVariables = new Dictionary<string, string> {
				{ "ANDROID_PRODUCT_OUT", GetAndroidProductOutDirectory () },
			};
			var result = await RunAdbCommand (args.ToArray (), environmentVariables);
			if (result.ExitCode != 0) {
				LogFastDeploy2Error ("XA0129", result.Output, stagingDirectory);
				return false;
			}
			SetAdbPushFileCounts (result.Output);
			LogDiagnostic (result.Output);
			return true;
		}

		async Task<HashSet<string>> GetChangedFilesFromSyncList (string remoteStagingPath)
		{
			var args = new [] { "sync", "-l", "data" };
			var environmentVariables = new Dictionary<string, string> {
				{ "ANDROID_PRODUCT_OUT", GetAndroidProductOutDirectory () },
			};
			var result = await RunAdbCommand (args, environmentVariables);
			if (result.ExitCode != 0) {
				LogFastDeploy2Error ("XA0129", result.Output, GetAndroidProductOutDirectory ());
				return null;
			}

			var changedFiles = new HashSet<string> (StringComparer.Ordinal);
			string prefix = remoteStagingPath.TrimEnd ('/') + "/";
			foreach (string line in result.Output.Split (new char [] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
				if (!line.StartsWith ("would push:", StringComparison.Ordinal)) {
					continue;
				}

				int index = line.LastIndexOf (" -> ", StringComparison.Ordinal);
				if (index < 0) {
					LogDebugMessage ($"Ignoring adb sync -l line '{line}'. Line is incorrectly formatted.");
					continue;
				}

				string remoteFile = line.Substring (index + 4).Trim ();
				if (!remoteFile.StartsWith (prefix, StringComparison.Ordinal)) {
					LogDebugMessage ($"Ignoring adb sync -l line '{line}'. Path is outside '{remoteStagingPath}'.");
					continue;
				}
				changedFiles.Add (remoteFile.Substring (prefix.Length));
			}
			LogDiagnostic ($"FastDeploy3 adb sync -l listed {changedFiles.Count} changed files.");
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
				if (!remoteFile.StartsWith (prefix, StringComparison.Ordinal)) {
					LogDebugMessage ($"Ignoring override file entry '{line}'. Path is outside '{overridePath}'.");
					continue;
				}
				files.Add (remoteFile.Substring (prefix.Length));
			}
			return files;
		}

		async Task<bool> RemoveStaleOverrideFiles (string overridePath, HashSet<string> stagedFiles, HashSet<string> overrideFiles)
		{
			var phase = Stopwatch.StartNew ();
			var staleFiles = new List<string> ();
			foreach (var file in overrideFiles) {
				if (!stagedFiles.Contains (file)) {
					staleFiles.Add ($"{overridePath}/{file}");
				}
			}

			LogDiagnostic ($"FastDeploy3 removing {staleFiles.Count} stale override files.");
			SetDiagnosticProperty ("deploy.fastdeploy2.stale.files", staleFiles.Count);
			for (int i = 0; i < staleFiles.Count; i += StaleFileRemovalBatchSize) {
				var args = new List<string> { "rm", "-f" };
				args.AddRange (staleFiles.Skip (i).Take (StaleFileRemovalBatchSize));
				string output = await RunAs (args.ToArray ());
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
			LogDiagnostic ($"FastDeploy3 copying {changedFiles.Count} changed override files.");
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

		string GetAndroidProductOutDirectory ()
		{
			return Path.Combine (GetFullPath (IntermediateOutputPath), "fastdeploy3-product-out");
		}

		string GetUserId ()
		{
			return string.IsNullOrEmpty (UserID) ? "0" : UserID;
		}
	}
}
