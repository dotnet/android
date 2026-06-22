using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Mono.AndroidTools;
using Mono.AndroidTools.Util;
using Xamarin.Android.Build.Debugging.Tasks.Properties;

namespace Xamarin.Android.Tasks
{
	public partial class FastDeploy2 : AsyncTask
	{
		const string OverridePath = "files/.__override__";
		const int StaleFileRemovalBatchSize = 100;
		const int CopyBatchSize = 25;
		const int MaxShellCommandLength = 900;
		const int MaxAdbCommandLength = 4096;

		public override string TaskPrefix => "FD2";

		public string AdbTarget { get; set; }
		public string UploadFlagFile { get; set; }
		public bool EmbedAssembliesIntoApk { get; set; }
		public bool ReInstall { get; set; } = false;

		[Required]
		public string PackageName { get; set; }

		public string PackageFile { get; set; }

		public string PrimaryCpuAbi { get; set; }

		public ITaskItem [] FastDevFiles { get; set; }

		public bool PreserveUserData { get; set; } = true;

		public bool DiagnosticLogging { get; set; } = false;

		public string UserID { get; set; }

		public bool IsTestOnly { get; set; }

		[Required]
		public string IntermediateOutputPath { get; set; }

		public ITaskItem [] EnvironmentFiles { get; set; }

		public string AdbToolPath { get; set; }

		public string AdbToolExe { get; set; }

		public string AdbPushCompressionAlgorithm { get; set; } = "any";

		public string AppFileTransferMode { get; set; } = "Copy";

		AndroidDevice Device;
		PackageInfo packageInfo = new PackageInfo ();
		DateTime lastUpload = DateTime.MinValue;
		Queue<string> diagnosticLogs = new Queue<string> ();
		readonly object diagnosticLogsLock = new object ();

		string OverrideFullPath {
			get { return packageInfo.IsSystemApplication ? $"{packageInfo.InternalPath}/{OverridePath}" : OverridePath; }
		}

		class PackageInfo {
			string internalPath = null;
			public string InternalPath {
				get { return internalPath; }
				set { internalPath = value?.Trim () ?? null; }
			}

			public bool IsSystemApplication { get; set; } = false;
			public bool AdbIsRoot { get; set; } = false;
			public string UserId { get; set; } = null;
			public string PackageName { get; set; } = null;
			public int ProcessId { get; set; } = 0;
		}

		class RemoteFileInfo {
			public long Size { get; set; }
			public long ModifiedTime { get; set; }
		}

		class DirectPushFile {
			public string LocalPath { get; set; }
			public string RelativePath { get; set; }
		}

		void LogDiagnostic (string message)
		{
			if (DiagnosticLogging) {
				LogDebugMessage (message);
				return;
			}
			lock (diagnosticLogsLock) {
				diagnosticLogs.Enqueue (message);
			}
		}

		void PrintDiagnostics ()
		{
			while (true) {
				string message;
				lock (diagnosticLogsLock) {
					if (diagnosticLogs.Count == 0) {
						break;
					}
					message = diagnosticLogs.Dequeue ();
				}
				LogMessage (message);
			}
		}

		void DebugHandler (string task, string message)
		{
			LogDiagnostic ($"DEBUG {task} {message}");
		}

		public override bool Execute ()
		{
			Device = AndroidHelper.ParseTarget (AdbTarget, LogMessage, LogCodedError, logErrors: true, engine4: BuildEngine4);
			if (Device == null) {
				PrintDiagnostics ();
				return false;
			}
			LogMessage ($"Found device: {Device.ID}");

			if (string.IsNullOrEmpty (PrimaryCpuAbi) && !EmbedAssembliesIntoApk) {
				PrintDiagnostics ();
				LogCodedError ("XA0010", Resources.XA0010_NoAbi, Device.ID);
				return false;
			}

			var flagFilePath = GetFullPath (UploadFlagFile);
			lastUpload = File.GetLastWriteTimeUtc (flagFilePath);
			LogDiagnostic ($"LastWriteTime of `{flagFilePath}`: {lastUpload}");

			var lifetime = RegisteredTaskObjectLifetime.AppDomain;
			var key = ProjectSpecificTaskObjectKey ($"{Device.ID}_{PackageName}_{GetType ().Name}");
			if (!File.Exists (UploadFlagFile)) {
				packageInfo = new PackageInfo ();
			} else {
				packageInfo = BuildEngine4.GetRegisteredTaskObjectAssemblyLocal<PackageInfo> (key, lifetime) ?? new PackageInfo ();
			}

			AndroidLogger.Debug += DebugHandler;
			try {
				return base.Execute ();
			} finally {
				BuildEngine4.RegisterTaskObjectAssemblyLocal (key, packageInfo, lifetime, allowEarlyCollection: false);
				AndroidLogger.Debug -= DebugHandler;
			}
		}

		public async override Task RunTaskAsync ()
		{
			try {
				await RunInstall ();
			} catch {
				PrintDiagnostics ();
				throw;
			}
		}

		async Task RunInstall ()
		{
			await RunLoggedDeviceOperation ("EnsureProperties", () => Device.EnsureProperties (CancellationToken));

			string redirectStdio = Device.Properties.Get ("log.redirect-stdio");
			if (redirectStdio != null && string.Equals ("true", redirectStdio.Trim (), StringComparison.OrdinalIgnoreCase)) {
				LogFastDeploy2Error ("XA0128", Resources.XA0128_RedirectStdioIsEnabled);
				return;
			}

			string runAsDisabled = Device.Properties.Get ("ro.boot.disable_runas");
			if (runAsDisabled != null && string.Equals ("true", runAsDisabled.Trim (), StringComparison.OrdinalIgnoreCase)) {
				LogFastDeploy2Error ("XA0131", Resources.XA0131_DeveloperModeNotEnabled);
				return;
			}

			await CheckAppInstalledAndDebuggable (PackageName);

			if (EmbedAssembliesIntoApk) {
				await RemoveOverrideDirectory ();
			}

			if (ReInstall && !string.IsNullOrEmpty (PackageFile)) {
				var uninstallCommand = new PmUninstallCommand {
					PackageName = PackageName,
					PreserveData = PreserveUserData,
				};
				await RunLoggedDeviceOperation ($"UninstallPackage {uninstallCommand}", () => Device.UninstallPackage (uninstallCommand, CancellationToken));
			}

			bool packageFileOutOfDate = !string.IsNullOrEmpty (PackageFile) &&
				(packageInfo.InternalPath.IndexOf ("unknown", StringComparison.OrdinalIgnoreCase) >= 0 || ReInstall || IsPackageFileOutOfDate ());

			if (packageFileOutOfDate) {
				try {
					await InstallPackage ();
				} catch (Exception ex) {
					LogFastDeploy2Error (GetErrorCode (ex), ex.ToString ());
					return;
				}
				if (!EmbedAssembliesIntoApk && packageInfo.InternalPath.IndexOf ("unknown", StringComparison.OrdinalIgnoreCase) >= 0) {
					packageInfo.InternalPath = null;
					await CheckAppInstalledAndDebuggable (PackageName);
					if (RaiseRunAsError (packageInfo.InternalPath)) {
						return;
					}
				}
			}

			if (EmbedAssembliesIntoApk)
				return;

			if ((FastDevFiles?.Length ?? 0) == 0 && (EnvironmentFiles?.Length ?? 0) == 0) {
				return;
			}

			await TerminateApp ();
			await DeployFastDevFilesWithAdbPush (OverrideFullPath);
		}

		bool IsPackageFileOutOfDate ()
		{
			var packageFile = GetFullPath (PackageFile);
			var lastPackage = File.GetLastWriteTimeUtc (packageFile);
			LogDiagnostic ($"LastWriteTime of `{packageFile}`: {lastPackage}");
			return lastUpload < lastPackage;
		}

		async Task CheckAppInstalledAndDebuggable (string packageName)
		{
			packageInfo.UserId = UserID;
			packageInfo.PackageName = packageName;
			packageInfo.ProcessId = 0;
			await EnsureUserIsRunning ();
			string packageInfoOutput = IsSafePackageNameForShell (packageName) ?
				await RunAs ("sh", "-c", $"pwd; pidof {packageName} 2>/dev/null || true") :
				await RunAs ("pwd");
			ParsePackageInfoOutput (packageInfoOutput);
			if (string.IsNullOrEmpty (packageInfo.InternalPath)) {
				packageInfo.InternalPath = packageInfoOutput?.Trim ();
			}
			if (packageInfo.InternalPath.IndexOf ("Permission denied", StringComparison.OrdinalIgnoreCase) >= 0) {
				packageInfo.InternalPath = await RunAs ("readlink", "-f", ".");
			}
			if (packageInfo.InternalPath.IndexOf ("not an application", StringComparison.OrdinalIgnoreCase) >= 0) {
				LogDiagnostic ($"Package {packageInfo.PackageName} is a system application.");
				packageInfo.IsSystemApplication = true;
				var whoami = await RunAdbShellCommand ("whoami");
				packageInfo.AdbIsRoot = whoami.Output.Trim () == "root";
				LogDiagnostic ($"using {(packageInfo.AdbIsRoot ? "root" : $"su {packageInfo.UserId}")} to install fast deployment files.");
				packageInfo.InternalPath = $"/data/user/{(packageInfo.UserId ?? "0")}/{packageInfo.PackageName}";
				return;
			}
			if (packageInfo.InternalPath.IndexOf ("not debuggable", StringComparison.OrdinalIgnoreCase) >= 0) {
				LogDiagnostic ($"Package {packageInfo.PackageName} was not debuggable. Forcing ReInstall");
				ReInstall = true;
				return;
			}
			if (packageInfo.InternalPath.IndexOf ("unknown", StringComparison.OrdinalIgnoreCase) >= 0) {
				LogDiagnostic ($"Package {packageInfo.PackageName} was not installed.");
				return;
			}
			if (packageInfo.InternalPath.IndexOf ("Permission denied", StringComparison.OrdinalIgnoreCase) >= 0) {
				LogDiagnostic ("run-as not supported on this device.");
			}
		}

		static bool IsSafePackageNameForShell (string packageName)
		{
			if (string.IsNullOrEmpty (packageName)) {
				return false;
			}
			foreach (char c in packageName) {
				if (!(char.IsLetterOrDigit (c) || c == '.' || c == '_')) {
					return false;
				}
			}
			return true;
		}

		void ParsePackageInfoOutput (string output)
		{
			if (string.IsNullOrEmpty (output)) {
				return;
			}

			string [] lines = output.Replace ("\r", "").Split (new char [] { '\n' }, StringSplitOptions.None);
			if (lines.Length > 0 && !string.IsNullOrEmpty (lines [0])) {
				packageInfo.InternalPath = lines [0].Trim ();
			}
			if (lines.Length <= 1) {
				return;
			}

			string pidLine = lines [1].Trim ();
			int space = pidLine.IndexOf (' ');
			if (space >= 0) {
				pidLine = pidLine.Substring (0, space);
			}
			if (int.TryParse (pidLine, out int pid)) {
				packageInfo.ProcessId = pid;
			}
		}

		async Task EnsureUserIsRunning ()
		{
			var userId = (UserID ?? "").Trim ();
			if (userId.Length == 0 || (int.TryParse (userId, out var id) && id == 0)) {
				return;
			}
			LogDiagnostic ($"Ensuring Android user {userId} is in the 'running' state before run-as queries.");
			var result = await RunAdbShellCommand ("am", "start-user", "-w", userId);
			string output = result.Output;
			LogDiagnostic ($"'am start-user -w {userId}' returned: {(string.IsNullOrWhiteSpace (output) ? "<no output>" : output.Trim ())}");
		}

		async Task InstallPackage ()
		{
			LogDebugMessage ($"Installing Package {PackageName}");
			var installCommand = new PushAndInstallCommand {
				ApkFile = PackageFile,
				PackageName = PackageName,
				ReInstall = ReInstall,
				User = UserID,
				TestOnly = IsTestOnly,
			};
			try {
				await RunLoggedDeviceOperation (FormatPushAndInstallOperation (installCommand), () => Device.PushAndInstallPackageAsync (installCommand, token: CancellationToken));
				LogDebugMessage ($"Installed Package {PackageName}.");
			} catch (Exception exception) {
				var ex = exception;
				if (exception is AggregateException aex) {
					ex = aex.Flatten ().InnerException;
				}
				if (!await ShouldThrowIfPackageInstallFailed (ex as PackageAlreadyExistsException)) {
					LogDebugMessage ($"Installed Package {PackageName}.");
					return;
				}
				throw;
			}
		}

		async Task<bool> ShouldThrowIfPackageInstallFailed (PackageAlreadyExistsException e)
		{
			if (e == null)
				return true;

			int s = (e.PackageFile ?? "").LastIndexOf ('/');
			string apkBasename = s >= 0 ? e.PackageFile.Substring (s + 1) : e.PackageFile;

			if (apkBasename != Path.GetFileName (PackageFile))
				return false;

			LogDebugMessage (string.Format ("Package '{0}' already exists. Retrying...", PackageName));
			try {
				await RunLoggedDeviceOperation ($"DeleteFile {e.PackageFile} ignoreError=True", () => Device.DeleteFile (e.PackageFile, true, CancellationToken));
			} catch {
			}
			bool preserveData = !(e is RequiresUninstallException);
			LogDebugMessage (string.Format ("Forcing complete uninstall of '{0}'... Preserving Data: {1}", PackageName, preserveData));
			var uninstallCommand = new PmUninstallCommand () { PackageName = PackageName, User = UserID, PreserveData = preserveData };
			await RunLoggedDeviceOperation ($"UninstallPackage {uninstallCommand}", () => Device.UninstallPackage (uninstallCommand, cancellationToken: CancellationToken));
			LogDebugMessage (string.Format ("Installing '{0}'...", PackageName));
			var installCommand = new PushAndInstallCommand {
				ApkFile = PackageFile,
				PackageName = PackageName,
				ReInstall = false,
				User = UserID
			};
			await RunLoggedDeviceOperation (FormatPushAndInstallOperation (installCommand), () => Device.PushAndInstallPackageAsync (installCommand, token: CancellationToken));
			return false;
		}

		static string FormatPushAndInstallOperation (PushAndInstallCommand command)
		{
			return $"PushAndInstallPackage ApkFile={command.ApkFile}, PackageName={command.PackageName}, ReInstall={command.ReInstall}, User={command.User ?? ""}, TestOnly={command.TestOnly}";
		}

		async Task RemoveOverrideDirectory ()
		{
			await RunAs ("rm", "-Rf", OverrideFullPath);
		}

		async Task TerminateApp ()
		{
			var pid = packageInfo.ProcessId;
			if (pid == 0 && packageInfo.IsSystemApplication) {
				pid = await RunLoggedDeviceOperation ($"GetProcessId {PackageName}", () => Device.GetProcessId (PackageName, CancellationToken));
			}
			if (pid == 0) {
				LogDebugMessage ($"{PackageName} was not running, skipping kill");
				return;
			}
			LogDebugMessage ($"Terminating {PackageName}...");
			await RunLoggedDeviceOperation ($"KillProcessAndWaitForExit {PackageName}", () => Device.KillProcessAndWaitForExit (PackageName, CancellationToken));
			LogDebugMessage ($"{PackageName} Terminated.");
		}

		async Task<string> CreateRemoteStagingDirectories (string remoteStagingPath, HashSet<string> stagedFiles)
		{
			var directories = new HashSet<string> (StringComparer.Ordinal) { remoteStagingPath };
			foreach (var file in stagedFiles) {
				string directory = GetDirectoryName (file);
				if (!string.IsNullOrEmpty (directory)) {
					directories.Add (CombineRemotePath (remoteStagingPath, directory));
				}
			}

			var output = new StringBuilder ();
			foreach (var batch in BatchArguments ("mkdir", "-p", directories)) {
				output.Append ((await RunAdbShellCommand (batch.ToArray ())).Output);
			}
			return output.ToString ();
		}

		List<DirectPushFile> PrepareDirectPushFiles ()
		{
			var files = new List<DirectPushFile> ();
			foreach (var file in FastDevFiles ?? []) {
				string localPath = GetFullPath (file.ItemSpec);
				if (!File.Exists (localPath)) {
					LogDiagnostic ($"File '{file.ItemSpec}' does not exist. Skipping.");
					continue;
				}
				if (Path.GetExtension (file.ItemSpec) == ".so") {
					string abi = AndroidRidAbiHelper.GetNativeLibraryAbi (file);
					if (abi != PrimaryCpuAbi) {
						LogDebugMessage ($"NotifySync SkipCopyFile {file.ItemSpec} abi not suitable for this device.");
						continue;
					}
				}

				files.Add (new DirectPushFile {
					LocalPath = localPath,
					RelativePath = GetAdbPushTargetPath (file),
				});
				LogDiagnostic ($"Prepared {file.ItemSpec} => {files [files.Count - 1].RelativePath}");
			}

			if (EnvironmentFiles?.Length > 0) {
				byte [] environmentData = CreateEnvironmentFileData (EnvironmentFiles, out DateTime newestFileDateTime);
				if (environmentData.Length > 0) {
					string environmentFile = Path.Combine (GetFullPath (IntermediateOutputPath), "fastdeploy2-environment", PrimaryCpuAbi, "environment");
					WriteFileIfChanged (environmentFile, environmentData, newestFileDateTime);
					files.Add (new DirectPushFile {
						LocalPath = environmentFile,
						RelativePath = $"{PrimaryCpuAbi}/environment",
					});
				}
			}

			return files;
		}

		bool WriteFileIfChanged (string path, byte [] contents, DateTime modifiedDateTime)
		{
			if (File.Exists (path) && File.ReadAllBytes (path).SequenceEqual (contents)) {
				return false;
			}

			Directory.CreateDirectory (Path.GetDirectoryName (path));
			File.WriteAllBytes (path, contents);
			File.SetLastWriteTimeUtc (path, modifiedDateTime);
			return true;
		}

		string GetAdbPushTargetPath (ITaskItem file)
		{
			string targetPath = file.GetMetadata ("TargetPath");
			if (string.IsNullOrEmpty (targetPath)) {
				LogDiagnostic ($"'TargetPath' metadata not found on '{file.ItemSpec}'. Falling back to 'DestinationSubPath'");
				targetPath = file.GetMetadata ("DestinationSubPath");
			}
			if (!string.IsNullOrEmpty (targetPath)) {
				return targetPath.Replace ("\\", "/");
			}
			return Path.GetFileName (file.ItemSpec);
		}

		byte [] CreateEnvironmentFileData (ITaskItem [] environments, out DateTime newestFileDateTime)
		{
			int maxKeyLength = 0;
			int maxValueLength = 0;
			newestFileDateTime = DateTime.MinValue;
			var data = new Dictionary<string, string> ();
			foreach (ITaskItem env in environments ?? []) {
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
				return [];
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

		async Task<Dictionary<string, RemoteFileInfo>> GetRemoteFileData (string rootPath, bool runAs)
		{
			string output;
			if (runAs) {
				output = await RunAs ("find", rootPath, "-type", "f", "-exec", "stat", "-c", "%n|%s|%Y", "{}", "+");
				if (RaiseRunAsError (output)) {
					return null;
				}
			} else {
				var result = await RunAdbShellCommand ("find", rootPath, "-type", "f", "-exec", "stat", "-c", "%n|%s|%Y", "{}", "+");
				output = result.Output;
			}

			if (IsMissingDirectoryError (output)) {
				return new Dictionary<string, RemoteFileInfo> (StringComparer.Ordinal);
			}
			if (IsShellError (output, "find") || IsShellError (output, "stat")) {
				LogFastDeploy2Error ("XA0129", output, rootPath);
				return null;
			}

			return ParseRemoteFileData (rootPath, output);
		}

		Dictionary<string, RemoteFileInfo> ParseRemoteFileData (string rootPath, string output)
		{
			var files = new Dictionary<string, RemoteFileInfo> (StringComparer.Ordinal);
			string prefix = rootPath.TrimEnd ('/') + "/";
			foreach (string line in output.Split (new char [] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
				var entries = line.Split (new char [] { '|' }, 3);
				if (entries.Length != 3) {
					LogDebugMessage ($"Ignoring remote file entry '{line}'. Line is incorrectly formatted.");
					continue;
				}
				string remoteFile = entries [0].Trim ();
				if (!remoteFile.StartsWith (prefix, StringComparison.Ordinal)) {
					LogDebugMessage ($"Ignoring remote file entry '{line}'. Path is outside '{rootPath}'.");
					continue;
				}
				if (!long.TryParse (entries [1].Trim (), out long size) || !long.TryParse (entries [2].Trim (), out long mtime)) {
					LogDebugMessage ($"Ignoring remote file entry '{line}'. Size or timestamp is invalid.");
					continue;
				}
				files [remoteFile.Substring (prefix.Length)] = new RemoteFileInfo {
					Size = size,
					ModifiedTime = mtime,
				};
			}
			return files;
		}

		async Task<bool> RemoveStaleOverrideFiles (string overridePath, Dictionary<string, RemoteFileInfo> stagedFiles, Dictionary<string, RemoteFileInfo> overrideFiles)
		{
			var staleFiles = new List<string> ();
			foreach (var file in overrideFiles.Keys) {
				if (!stagedFiles.ContainsKey (file)) {
					staleFiles.Add (CombineRemotePath (overridePath, file));
				}
			}

			LogDiagnostic ($"FastDeploy2 removing {staleFiles.Count} stale override files.");
			for (int i = 0; i < staleFiles.Count; i += StaleFileRemovalBatchSize) {
				var args = new List<string> { "rm", "-f" };
				args.AddRange (staleFiles.Skip (i).Take (StaleFileRemovalBatchSize));
				string output = await RunAs (args.ToArray ());
				if (RaiseRunAsError (output) || IsShellError (output, "rm")) {
					LogFastDeploy2Error ("XA0129", output, overridePath);
					return false;
				}
			}
			return true;
		}

		async Task<bool> CopyChangedFiles (string remoteStagingPath, string overridePath, Dictionary<string, RemoteFileInfo> stagedFiles, Dictionary<string, RemoteFileInfo> overrideFiles)
		{
			var changedFiles = new List<string> ();
			foreach (var file in stagedFiles) {
				if (!overrideFiles.TryGetValue (file.Key, out RemoteFileInfo existing) ||
						existing.Size != file.Value.Size ||
						existing.ModifiedTime != file.Value.ModifiedTime) {
					changedFiles.Add (file.Key);
				}
			}

			LogDiagnostic ($"FastDeploy2 copying {changedFiles.Count} changed override files.");
			var filesByDirectory = GroupFilesByDirectory (changedFiles);

			foreach (var group in filesByDirectory) {
				string targetDirectory = CombineRemotePath (overridePath, group.Key);
				string output = await RunAs ("mkdir", "-p", targetDirectory);
				if (RaiseRunAsError (output) || IsShellError (output, "mkdir")) {
					LogFastDeploy2Error ("XA0129", output, targetDirectory);
					return false;
				}

				for (int i = 0; i < group.Value.Count; i += CopyBatchSize) {
					var batchFiles = group.Value.Skip (i).Take (CopyBatchSize).ToList ();
					var removeArgs = new List<string> { "rm", "-f" };
					foreach (string file in batchFiles) {
						removeArgs.Add (CombineRemotePath (targetDirectory, Path.GetFileName (file)));
					}
					output = await RunAs (removeArgs.ToArray ());
					if (RaiseRunAsError (output) || IsShellError (output, "rm")) {
						LogFastDeploy2Error ("XA0129", output, targetDirectory);
						return false;
					}

					var args = new List<string> { "cp", "-p" };
					foreach (string file in batchFiles) {
						args.Add (CombineRemotePath (remoteStagingPath, file));
					}
					args.Add (targetDirectory);
					output = await RunAs (args.ToArray ());
					if (RaiseRunAsError (output) || IsShellError (output, "cp")) {
						LogFastDeploy2Error ("XA0129", output, targetDirectory);
						return false;
					}
				}
			}

			return true;
		}

		IEnumerable<List<string>> BatchArguments (string command, string option, IEnumerable<string> values)
		{
			var batch = new List<string> { command, option };
			int length = command.Length + option.Length + 2;
			foreach (var value in values) {
				int itemLength = value.Length + 3;
				if (batch.Count > 2 && length + itemLength >= MaxShellCommandLength) {
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

		(int pushed, int skipped) TryParsePushSummary (string output)
		{
			int pushed = 0;
			int skipped = 0;
			foreach (var line in output.Split (new char [] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
				var match = AdbPushSummaryRegex.Match (line);
				if (!match.Success) {
					continue;
				}
				pushed = int.Parse (match.Groups ["pushed"].Value);
				skipped = int.Parse (match.Groups ["skipped"].Value);
			}
			return (pushed, skipped);
		}

		async Task<AdbCommandResult> RunAdbCommand (params string [] arguments)
		{
			return await RunAdbCommand (arguments, environmentVariables: null);
		}

		async Task<AdbCommandResult> RunAdbShellCommand (params string [] arguments)
		{
			return await RunAdbCommand (new [] { "shell" }.Concat (arguments).ToArray ());
		}

		async Task<AdbCommandResult> RunAdbCommand (string [] arguments, Dictionary<string, string> environmentVariables)
		{
			string adb = ResolveAdbPath ();
			var processArguments = new ProcessArgumentBuilder ();
			if (Device != null && !string.IsNullOrEmpty (Device.ID) && !string.Equals (Device.ID, "any", StringComparison.OrdinalIgnoreCase)) {
				processArguments.AddQuoted ("-s");
				processArguments.AddQuoted (Device.ID);
			}
			processArguments.AddQuoted (arguments);

			var stdout = new StringBuilder ();
			var stderr = new StringBuilder ();
			var stdoutCompleted = new ManualResetEvent (false);
			var stderrCompleted = new ManualResetEvent (false);
			var psi = new ProcessStartInfo {
				FileName = adb,
				Arguments = processArguments.ToString (),
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden,
			};
			if (environmentVariables != null) {
				foreach (var kvp in environmentVariables) {
					psi.EnvironmentVariables [kvp.Key] = kvp.Value;
				}
			}

			LogDiagnostic ($"adb command: {psi.FileName} {psi.Arguments}");
			using (var process = new Process ()) {
				process.StartInfo = psi;
				process.OutputDataReceived += (sender, e) => {
					if (e.Data != null) {
						lock (stdout) {
							stdout.AppendLine (e.Data);
						}
					} else {
						stdoutCompleted.Set ();
					}
				};
				process.ErrorDataReceived += (sender, e) => {
					if (e.Data != null) {
						lock (stderr) {
							stderr.AppendLine (e.Data);
						}
					} else {
						stderrCompleted.Set ();
					}
				};

				process.Start ();
				process.BeginOutputReadLine ();
				process.BeginErrorReadLine ();
				using (CancellationToken.Register (() => {
					try {
						if (!process.HasExited) {
							process.Kill ();
						}
					} catch (InvalidOperationException) {
					}
				})) {
					await Task.Run (() => process.WaitForExit (), CancellationToken);
				}
				stdoutCompleted.WaitOne (TimeSpan.FromSeconds (30));
				stderrCompleted.WaitOne (TimeSpan.FromSeconds (30));
				var result = new AdbCommandResult {
					ExitCode = process.ExitCode,
					StandardOutput = stdout.ToString ().Trim (),
					StandardError = stderr.ToString ().Trim (),
				};
				LogAdbCommandResult (result);
				return result;
			}
		}

		void LogAdbCommandResult (AdbCommandResult result)
		{
			LogDiagnostic ($"adb exit code: {result.ExitCode}");
			LogAdbStream ("stdout", result.StandardOutput);
			LogAdbStream ("stderr", result.StandardError);
		}

		void LogAdbStream (string name, string value)
		{
			if (string.IsNullOrEmpty (value)) {
				return;
			}
			LogDiagnostic ($"adb {name}:{Environment.NewLine}{value}");
		}

		async Task RunLoggedDeviceOperation (string operation, Func<Task> action)
		{
			LogDiagnostic ($"AndroidDevice operation: {operation}");
			await action ();
			LogDiagnostic ($"AndroidDevice operation completed: {operation}");
		}

		async Task<T> RunLoggedDeviceOperation<T> (string operation, Func<Task<T>> action)
		{
			LogDiagnostic ($"AndroidDevice operation: {operation}");
			T result = await action ();
			LogDiagnostic ($"AndroidDevice operation completed: {operation} => {result}");
			return result;
		}

		List<string> BuildRunAsArgs ()
		{
			List<string> args = new List<string> ();
			if (packageInfo.IsSystemApplication) {
				if (!packageInfo.AdbIsRoot) {
					args.Add ("su");
					args.Add (packageInfo.UserId ?? "0");
				}
				return args;
			}
			args.Add ("run-as");
			args.Add (packageInfo.PackageName);
			if (!string.IsNullOrEmpty (packageInfo.UserId)) {
				args.Add ("--user");
				args.Add (packageInfo.UserId);
			}
			return args;
		}

		async Task<string> RunAs (params string [] arguments)
		{
			List<string> args = BuildRunAsArgs ();
			args.AddRange (arguments);
			var result = await RunAdbShellCommand (args.ToArray ());
			return result.Output;
		}

		async Task<string> RunAsShell (string script)
		{
			List<string> args = BuildRunAsArgs ();
			args.Add ("sh");
			args.Add ("-c");
			args.Add (script);
			string command = string.Join (" ", args.Select (QuoteShellArgument));
			var result = await RunAdbShellCommand (command);
			return result.Output;
		}

		static string QuoteShellArgument (string value)
		{
			return "'" + value.Replace ("'", "'\"'\"'") + "'";
		}

		string ResolveAdbPath ()
		{
			var exe = string.IsNullOrEmpty (AdbToolExe) ? "adb" : AdbToolExe;
			return string.IsNullOrEmpty (AdbToolPath) ? exe : Path.Combine (AdbToolPath, exe);
		}

		string GetRemoteAdbPushStagingPath ()
		{
			return $"{RemoteStagingRoot}/{PackageName}/{GetUserId ()}";
		}

		string GetUserId ()
		{
			return string.IsNullOrEmpty (UserID) ? "0" : UserID;
		}

		string GetDeviceId ()
		{
			if (Device != null && !string.IsNullOrEmpty (Device.ID)) {
				return Device.ID;
			}
			return string.IsNullOrEmpty (AdbTarget) ? "any" : AdbTarget;
		}

		void LogFastDeploy2Error (string errorCode, string error, string file = "")
		{
			if (!string.IsNullOrEmpty (file)) {
				LogDiagnostic ($"{errorCode} while deploying '{file}': {error}");
			} else {
				LogDiagnostic ($"{errorCode}: {error}");
			}
			PrintDiagnostics ();
			if (errorCode == "XA0129") {
				LogCodedError (errorCode, Resources.XA0129_ErrorDeployingFile, file);
			} else {
				LogCodedError (errorCode, error);
			}
		}

		string GetFullPath (string dir) => Path.IsPathRooted (dir) ? dir : Path.GetFullPath (Path.Combine (WorkingDirectory, dir));

		static string GetDirectoryName (string file)
		{
			return Path.GetDirectoryName (file)?.Replace ("\\", "/") ?? "";
		}

		static string CombineRemotePath (string rootPath, string relativePath)
		{
			return string.IsNullOrEmpty (relativePath) ? rootPath : $"{rootPath}/{relativePath}";
		}

		static Dictionary<string, List<string>> GroupFilesByDirectory (IEnumerable<string> files)
		{
			var filesByDirectory = new Dictionary<string, List<string>> (StringComparer.Ordinal);
			foreach (string file in files) {
				string directory = GetDirectoryName (file);
				if (!filesByDirectory.TryGetValue (directory, out List<string> filesInDirectory)) {
					filesInDirectory = new List<string> ();
					filesByDirectory.Add (directory, filesInDirectory);
				}
				filesInDirectory.Add (file);
			}
			return filesByDirectory;
		}

		bool RaiseRunAsError (string error)
		{
			if (TryGetRunAsErrorCode (error, out var err)) {
				LogDiagnostic ($"{err.code}: {err.message}");
				PrintDiagnostics ();
				LogCodedError (err.code, err.message, error);
				return true;
			}
			return false;
		}

		bool TryGetRunAsErrorCode (string error, out (string error, string code, string message) errTuple)
		{
			errTuple = (error: "unknown", code: "XA0132", message: error);
			foreach (var err in runas_codes) {
				if (error.IndexOf (err.error, StringComparison.OrdinalIgnoreCase) >= 0) {
					errTuple = err;
					return true;
				}
			}
			return false;
		}

		string GetErrorCode (Exception ex)
		{
			return ex switch {
				IncompatibleCpuAbiException => "ADB0020",
				RequiresUninstallException => "ADB0030",
				SdkNotSupportedException => "ADB0040",
				PackageAlreadyExistsException => "ADB0050",
				InsufficientSpaceException => "ADB0060",
				InstallFailedException => "ADB0010",
				_ => GetErrorCode (ex.Message),
			};
		}

		static string GetErrorCode (string message)
		{
			foreach (var errorCode in error_codes)
				if (message.IndexOf (errorCode.message, StringComparison.OrdinalIgnoreCase) >= 0)
					return errorCode.code;

			return "ADB1000";
		}

		static bool IsShellError (string output, string command)
		{
			if (string.IsNullOrEmpty (output)) {
				return false;
			}
			return output.IndexOf ($"{command}:", StringComparison.OrdinalIgnoreCase) >= 0 ||
				output.IndexOf ("No such file or directory", StringComparison.OrdinalIgnoreCase) >= 0 ||
				output.IndexOf ("Permission denied", StringComparison.OrdinalIgnoreCase) >= 0 ||
				output.IndexOf ("Read-only file system", StringComparison.OrdinalIgnoreCase) >= 0 ||
				output.IndexOf ("not found", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		static bool IsMissingDirectoryError (string output)
		{
			return !string.IsNullOrEmpty (output) &&
				output.IndexOf ("No such file or directory", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		struct AdbCommandResult
		{
			public int ExitCode;
			public string StandardOutput;
			public string StandardError;

			public string Output {
				get {
					if (string.IsNullOrEmpty (StandardOutput)) {
						return StandardError ?? "";
					}
					if (string.IsNullOrEmpty (StandardError)) {
						return StandardOutput;
					}
					return $"{StandardOutput}{Environment.NewLine}{StandardError}";
				}
			}
		}

		static readonly List<(string error, string code, string message)> runas_codes = new List<(string error, string code, string message)> () {
			{ (error: "run-as is disabled",             code: "XA0131", message: Resources.XA0131_DeveloperModeNotEnabled ) },
			{ (error: "Could not set capabilities",     code: "XA0131", message: Resources.XA0131_DeveloperModeNotEnabled ) },
			{ (error: "unknown",                        code: "XA0132", message: Resources.XA0132_PackageNotInstalled ) },
			{ (error: "Permission denied",              code: "XA0133", message: Resources.XA0133_RunAsPermissionDenied ) },
			{ (error: "package not debuggable",         code: "XA0134", message: Resources.XA0134_RunAsPackageNotDebuggable ) },
			{ (error: "package not an application",     code: "XA0135", message: Resources.XA0135_RunAsPackageNotAndApplication ) },
			{ (error: "has corrupt installation",       code: "XA0136", message: Resources.XA0136_RunAsCorruptInstallation ) },
			{ (error: "users can run this program",     code: "XA0137", message: Resources.XA0137_RunAsOSCorrupt ) },
			{ (error: "set SELinux security context",   code: "XA0137", message: Resources.XA0137_RunAsOSCorrupt ) },
			{ (error: "to package's data directory",    code: "XA0137", message: Resources.XA0137_RunAsOSCorrupt ) },
			{ (error: "couldn't stat",                  code: "XA0137", message: Resources.XA0137_RunAsOSCorrupt ) },
			{ (error: "has wrong owner",                code: "XA0137", message: Resources.XA0137_RunAsOSCorrupt ) },
			{ (error: "readable or writable by others", code: "XA0137", message: Resources.XA0137_RunAsOSCorrupt ) },
			{ (error: "not a directory",                code: "XA0137", message: Resources.XA0137_RunAsOSCorrupt ) },
			{ (error: "run-as:",                        code: "XA0137", message: Resources.XA0137_RunAsOSCorrupt ) },
		};

		static readonly List<(string code, string message)> error_codes = new List<(string code, string message)> () {
			{ (code: "ADB0010", message: nameof (InstallFailedException)) },
			{ (code: "ADB0020", message: nameof (IncompatibleCpuAbiException)) },
			{ (code: "ADB0030", message: nameof (RequiresUninstallException)) },
			{ (code: "ADB0040", message: nameof (SdkNotSupportedException)) },
			{ (code: "ADB0050", message: nameof (PackageAlreadyExistsException)) },
			{ (code: "ADB0060", message: nameof (InsufficientSpaceException)) },
			{ (code: "ADB1001", message: "failed to create session") },
			{ (code: "ADB1002", message: "failed to finalize session") },
			{ (code: "ADB1003", message: "product directory not specified; set $ANDROID_PRODUCT_OUT") },
			{ (code: "ADB1004", message: "server didn't ACK") },
			{ (code: "ADB1005", message: "server killed by remote request") },
			{ (code: "ADB1006", message: "timed out waiting for threads to finish reading from ADB server") },
			{ (code: "ADB1007", message: "usage:") },
			{ (code: "ADB1008", message: "bulkIn endpoint not assigned") },
			{ (code: "ADB1009", message: "bulkOut endpoint not assigned") },
			{ (code: "ADB1010", message: "cannot start server on remote host") },
			{ (code: "ADB1011", message: "cap_clear_flag(INHERITABLE) failed") },
			{ (code: "ADB1012", message: "cap_clear_flag(PEMITTED) failed") },
			{ (code: "ADB1013", message: "cap_set_proc() failed") },
			{ (code: "ADB1014", message: "Client not connected") },
			{ (code: "ADB1015", message: "Could not find device interface") },
			{ (code: "ADB1016", message: "Could not set SELinux context") },
			{ (code: "ADB1017", message: "Could not start mdnsd") },
			{ (code: "ADB1018", message: "could not start server") },
			{ (code: "ADB1019", message: "couldn't allocate StdinReadArgs object") },
			{ (code: "ADB1020", message: "couldn't create USB matching dictionary") },
			{ (code: "ADB1021", message: "daemon started successfully") },
			{ (code: "ADB1022", message: "daemon still not running") },
			{ (code: "ADB1023", message: "error: no emulator detected") },
			{ (code: "ADB1024", message: "error: shell command too long") },
			{ (code: "ADB1025", message: "Failed to allocate key") },
			{ (code: "ADB1026", message: "failed to allocate memory for ShellProtocol object") },
			{ (code: "ADB1027", message: "failed to allocate new subprocess") },
			{ (code: "ADB1028", message: "Failed to convert to public key") },
			{ (code: "ADB1029", message: "failed to create pipe to report error") },
			{ (code: "ADB1030", message: "failed to create run queue notify socketpair") },
			{ (code: "ADB1031", message: "failed to empty run queue notify fd") },
			{ (code: "ADB1032", message: "failed to encode RSA public key") },
			{ (code: "ADB1033", message: "Failed to generate new key") },
			{ (code: "ADB1034", message: "failed to get matching services") },
			{ (code: "ADB1035", message: "failed to get user home directory") },
			{ (code: "ADB1036", message: "Failed to get user key") },
			{ (code: "ADB1037", message: "failed to make run queue notify socket nonblocking") },
			{ (code: "ADB1038", message: "Failed to read key") },
			{ (code: "ADB1039", message: "failed to register libusb hotplug callback") },
			{ (code: "ADB1040", message: "failed to start daemon") },
			{ (code: "ADB1041", message: "failed to write to run queue notify fd") },
			{ (code: "ADB1042", message: "Key must be a null-terminated string") },
			{ (code: "ADB1043", message: "Pipe stalled, clearing stall") },
			{ (code: "ADB1044", message: "Public key too large to base64 encode") },
			{ (code: "ADB1045", message: "reply fd for adb server to client communication not specified") },
			{ (code: "ADB1046", message: "run queue notify fd was closed") },
			{ (code: "ADB1047", message: "Unable to get interface class, subclass and protocol") },
			{ (code: "ADB1048", message: "usb_read interface was null") },
			{ (code: "ADB1049", message: "usb_write interface was null") },
			{ (code: "ADB1050", message: "cannot fit pipe handle value into 32-bits") },
			{ (code: "ADB1051", message: "connect error for create") },
			{ (code: "ADB1052", message: "connect error for finalize") },
			{ (code: "ADB1053", message: "connect error for write") },
			{ (code: "ADB1054", message: "could not open adb service") },
			{ (code: "ADB1055", message: "couldn't parse 'wait-for' command") },
			{ (code: "ADB1056", message: "CreateFileW 'nul' failed") },
			{ (code: "ADB1057", message: "only wrote") },
			{ (code: "ADB1058", message: "error response") },
			{ (code: "ADB1059", message: "failed to install") },
			{ (code: "ADB1060", message: "failed to read block") },
			{ (code: "ADB1061", message: "failed to write data") },
			{ (code: "ADB1062", message: "invalid reply fd") },
			{ (code: "ADB1063", message: "pre-KitKat sideload connection failed") },
			{ (code: "ADB1064", message: "doesn't match this client") },
			{ (code: "ADB1065", message: "sideload connection failed") },
			{ (code: "ADB1066", message: "unable to connect for backup") },
			{ (code: "ADB1067", message: "unable to connect for restore") },
			{ (code: "ADB1068", message: "unable to connect for") },
			{ (code: "ADB1069", message: "unexpected output length for") },
			{ (code: "ADB1070", message: "expected 'any', 'local', or 'usb'") },
			{ (code: "ADB1071", message: "attempted to close unregistered usb_handle for") },
			{ (code: "ADB1072", message: "attempted to reinitialize adb_server_socket_spec") },
			{ (code: "ADB1073", message: "cannot connect to daemon at") },
			{ (code: "ADB1074", message: "Cannot mkdir") },
			{ (code: "ADB1075", message: "Connection banner is too long") },
			{ (code: "ADB1076", message: "Could not clear pipe stall both ends") },
			{ (code: "ADB1077", message: "Could not install smartsocket listener") },
			{ (code: "ADB1078", message: "Could not open interface") },
			{ (code: "ADB1079", message: "Could not register mDNS service") },
			{ (code: "ADB1080", message: "Couldn't create a device interface") },
			{ (code: "ADB1081", message: "Couldn't grab device from interface") },
			{ (code: "ADB1082", message: "Couldn't query the interface") },
			{ (code: "ADB1083", message: "daemon not running; starting now at") },
			{ (code: "ADB1084", message: "destroying fde not created by fdevent_create") },
			{ (code: "ADB1085", message: "Encountered mDNS registration error") },
			{ (code: "ADB1086", message: "not implemented on Win32") },
			{ (code: "ADB1087", message: "could not connect to TCP port") },
			{ (code: "ADB1088", message: "no emulator connected") },
			{ (code: "ADB1089", message: "only supports allocating a pty") },
			{ (code: "ADB1090", message: "failed to connect to socket") },
			{ (code: "ADB1091", message: "failed to convert errno") },
			{ (code: "ADB1092", message: "failed to initialize libusb") },
			{ (code: "ADB1093", message: "Failed to parse key") },
			{ (code: "ADB1094", message: "failed to set non-blocking mode for fd") },
			{ (code: "ADB1095", message: "failed to start subprocess management thread") },
			{ (code: "ADB1096", message: "failed to start subprocess") },
			{ (code: "ADB1097", message: "FindDeviceInterface - could not get pipe properties") },
			{ (code: "ADB1098", message: "Invalid base64 key") },
			{ (code: "ADB1099", message: "Key too long") },
			{ (code: "ADB1100", message: "No ':' found in shell service arguments") },
			{ (code: "ADB1101", message: "observed inotify event for unmonitored path") },
			{ (code: "ADB1102", message: "packet data length doesn't match payload") },
			{ (code: "ADB1103", message: "Unable to create a device plug-in") },
			{ (code: "ADB1104", message: "Unable to create an interface plug-in") },
			{ (code: "ADB1105", message: "Unable to get number of endpoints") },
			{ (code: "ADB1106", message: "unexpected type for") },
			{ (code: "ADB1107", message: "Unknown socket type") },
			{ (code: "ADB1108", message: "Unknown trace flag") },
			{ (code: "ADB1109", message: "usb_read failed with status") },
			{ (code: "ADB1110", message: "usb_write failed with status") },
			{ (code: "ADB1111", message: "adb_socket_accept: failed to allocate accepted socket") },
			{ (code: "ADB1112", message: "cannot create service socket pair") },
			{ (code: "ADB1113", message: "cannot create socket pair") },
			{ (code: "ADB1114", message: "Error generating token") },
			{ (code: "ADB1115", message: "Error getting user key filename") },
			{ (code: "ADB1116", message: "Failed to accept") },
			{ (code: "ADB1117", message: "failed to create inotify fd") },
			{ (code: "ADB1118", message: "Failed to get adbd socket") },
			{ (code: "ADB1119", message: "failed to shutdown writes to FD") },
			{ (code: "ADB1120", message: "Failed to write PK") },
			{ (code: "ADB1121", message: "failed to write the exit code packet") },
			{ (code: "ADB1122", message: "read of inotify event failed") },
			{ (code: "ADB1123", message: "remote usb: 1 - write terminated") },
			{ (code: "ADB1124", message: "remote usb: 2 - write terminated") },
			{ (code: "ADB1125", message: "remote usb: read terminated (message)") },
			{ (code: "ADB1126", message: "remote usb: terminated (data)") },
			{ (code: "ADB1127", message: "select failed, closing subprocess pipes") },
			{ (code: "ADB1128", message: "backup unable to create file") },
			{ (code: "ADB1129", message: "cannot create thread") },
			{ (code: "ADB1130", message: "cannot get executable path") },
			{ (code: "ADB1131", message: "cannot make handle") },
			{ (code: "ADB1132", message: "CreatePipe failed") },
			{ (code: "ADB1133", message: "CreateProcessW failed") },
			{ (code: "ADB1134", message: "error while reading for") },
			{ (code: "ADB1135", message: "execl returned") },
			{ (code: "ADB1136", message: "failed to duplicate file descriptor for") },
			{ (code: "ADB1137", message: "failed to get file descriptor for") },
			{ (code: "ADB1138", message: "failed to open duplicate stream for") },
			{ (code: "ADB1139", message: "failed to open file") },
			{ (code: "ADB1140", message: "failed to read command") },
			{ (code: "ADB1141", message: "failed to read data from") },
			{ (code: "ADB1142", message: "failed to read from") },
			{ (code: "ADB1143", message: "failed to read package block") },
			{ (code: "ADB1144", message: "failed to seek to package block") },
			{ (code: "ADB1145", message: "failed to set binary mode for duplicate of") },
			{ (code: "ADB1146", message: "failed to stat file") },
			{ (code: "ADB1147", message: "failed to stat") },
			{ (code: "ADB1148", message: "failed to unbuffer") },
			{ (code: "ADB1149", message: "adb_socket_accept: accept on fd") },
			{ (code: "ADB1150", message: "unable to open file") },
			{ (code: "ADB1151", message: "unexpected result waiting for threads") },
			{ (code: "ADB1152", message: "aio: got error event on") },
			{ (code: "ADB1153", message: "aio: got error submitting") },
			{ (code: "ADB1154", message: "aio: got error waiting") },
			{ (code: "ADB1155", message: "cannot open bulk-in endpoint") },
			{ (code: "ADB1156", message: "cannot open bulk-out endpoint") },
			{ (code: "ADB1157", message: "cannot open control endpoint") },
			{ (code: "ADB1158", message: "Can't load") },
			{ (code: "ADB1159", message: "could not read ok from ADB Server") },
			{ (code: "ADB1160", message: "couldn't allocate state_info") },
			{ (code: "ADB1161", message: "Couldn't read") },
			{ (code: "ADB1162", message: "cannot write to emulator") },
			{ (code: "ADB1163", message: "error reading output FD") },
			{ (code: "ADB1164", message: "error reading protocol FD") },
			{ (code: "ADB1165", message: "error reading stdin FD") },
			{ (code: "ADB1166", message: "write failure during connection") },
			{ (code: "ADB1167", message: "failed to fcntl(F_GETFL) for fd") },
			{ (code: "ADB1168", message: "failed to fcntl(F_SETFL) for fd") },
			{ (code: "ADB1169", message: "failed to inotify_add_watch on path") },
			{ (code: "ADB1170", message: "Failed to listen on") },
			{ (code: "ADB1171", message: "failed to open directory") },
			{ (code: "ADB1172", message: "Failed to write public key to") },
			{ (code: "ADB1173", message: "failure closing FD") },
			{ (code: "ADB1174", message: "pipe failed in launch_server") },
			{ (code: "ADB1175", message: "poll() }, ret =") },
			{ (code: "ADB1176", message: "remote usb: read overflow") },
			{ (code: "ADB1177", message: "received framework auth socket connection again") },
			{ (code: "ADB1178", message: "failed to claim adb interface for device") },
			{ (code: "ADB1179", message: "failed to clear halt on device") },
			{ (code: "ADB1180", message: "failed to get active config descriptor for device at") },
			{ (code: "ADB1181", message: "failed to get device descriptor for device at") },
			{ (code: "ADB1182", message: "failed to get serial from device at") },
			{ (code: "ADB1183", message: "failed to open usb device at") },
			{ (code: "ADB1184", message: "failed to set interface alt setting for device") },
			{ (code: "ADB1185", message: "failed to submit zero-length write") },
			{ (code: "ADB1186", message: "failed to submit") },
			{ (code: "ADB1187", message: "Ignoring unknown shell service argument") },
			{ (code: "ADB1188", message: "transfer failed:") },
			{ (code: "ADB1189", message: "received empty serial from device at") },
			{ (code: "ADB1190", message: "refusing to recurse into directory") },
			{ (code: "ADB1191", message: "unmonitored event for") },
			{ (code: "ADB1192", message: "Failed to open") },
			{ (code: "ADB1193", message: "failed to write") },
		};

		static readonly Regex AdbPushSummaryRegex = new Regex (@"(?<pushed>\d+) files? pushed, (?<skipped>\d+) skipped", RegexOptions.Compiled);
	}
}
