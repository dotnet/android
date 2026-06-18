using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
	public class FastDeploy2 : AsyncTask
	{
		const string OverridePath = "files/.__override__";
		const int StaleFileRemovalBatchSize = 100;
		const int CopyBatchSize = 25;

		public override string TaskPrefix => "FD2";

		public string AdbTarget { get; set; }
		public string UploadFlagFile { get; set; }
		public bool EmbedAssembliesIntoApk { get; set; }
		public bool ReInstall { get; set; } = false;

		[Required]
		public string PackageName { get; set; }

		public string PackageFile { get; set; }

		public string PrimaryCpuAbi { get; set; }
		public string ToolsAbi { get; set; }

		public ITaskItem [] FastDevFiles { get; set; }

		public bool PreserveUserData { get; set; } = true;

		[Required]
		public string FastDevToolPath { get; set; }

		[Required]
		public string ToolVersion { get; set; }

		public bool DiagnosticLogging { get; set; } = false;

		public bool UsingAndroidNETSdk { get; set; }

		public string UserID { get; set; }

		public bool IsTestOnly { get; set; }

		[Required]
		public string IntermediateOutputPath { get; set; }

		public ITaskItem [] EnvironmentFiles { get; set; }

		public string AdbToolPath { get; set; }

		public string AdbToolExe { get; set; }

		public string AdbPushCompressionAlgorithm { get; set; } = "any";

		AndroidDevice Device;
		PackageInfo packageInfo = new PackageInfo ();
		DateTime lastUpload = DateTime.MinValue;
		Queue<string> diagnosticLogs = new Queue<string> ();
		DiagnosticData diagnosticData = new DiagnosticData ();

		protected virtual string RemoteStagingRoot => "/tmp/fastdev2";

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

		class DiagnosticData {
			[JsonPropertyName ("Task")]
			public string Task { get; set; } = nameof (FastDeploy2);

			[JsonPropertyName ("Properties")]
			public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string> () {
				{ "target.prop.ro.product.build.version.sdk", "" },
				{ "target.prop.ro.product.cpu.abilist", "" },
				{ "target.prop.ro.product.manufacturer", "" },
				{ "target.prop.ro.product.model", "" },
				{ "target.prop.ro.product.cpu.abi", "" },
				{ "deploy.error.code", "" },
				{ "deploy.tool", "adb push" },
				{ "deploy.result", "Success" },
				{ "deploy.supports.fastdev", "True" },
				{ "deploy.systemapp", "False" },
				{ "deploy.duration.ms", "0" },
				{ "deploy.fastdeploy2.adb.pushed.files", "" },
				{ "deploy.fastdeploy2.adb.skipped.files", "" },
				{ "deploy.fastdeploy2.changed.files", "" },
				{ "deploy.fastdeploy2.stale.files", "" },
				{ "deploy.fastdeploy2.local.stage.ms", "" },
				{ "deploy.fastdeploy2.remote.mkdir.ms", "" },
				{ "deploy.fastdeploy2.remote.staging.cleanup.ms", "" },
				{ "deploy.fastdeploy2.upload.ms", "" },
				{ "deploy.fastdeploy2.staging.stat.ms", "" },
				{ "deploy.fastdeploy2.override.stat.ms", "" },
				{ "deploy.fastdeploy2.compare.ms", "" },
				{ "deploy.fastdeploy2.stale.remove.ms", "" },
				{ "deploy.fastdeploy2.override.mkdir.ms", "" },
				{ "deploy.fastdeploy2.override.copy.ms", "" },
				{ "deploy.fastdeploy3.sync.list.ms", "" },
				{ "deploy.fastdeploy3.sync.list.files", "" },
				{ "deploy.fastdeploy3.override.list.ms", "" },
				{ "deploy.fastdeploy3.missing.files", "" },
				{ "deploy.orchestration.ensure-properties.ms", "" },
				{ "deploy.orchestration.property-checks.ms", "" },
				{ "deploy.orchestration.package-check.ms", "" },
				{ "deploy.orchestration.package-timestamp.ms", "" },
				{ "deploy.orchestration.install.ms", "" },
				{ "deploy.orchestration.terminate.ms", "" },
				{ "deploy.orchestration.empty-check.ms", "" },
				{ "deploy.execute.parse-target.ms", "" },
				{ "deploy.execute.no-abi-check.ms", "" },
				{ "deploy.execute.upload-flag-stat.ms", "" },
				{ "deploy.execute.task-cache.ms", "" },
				{ "deploy.orchestration.property-capture.ms", "" },
				{ "deploy.orchestration.redirect-stdio-check.ms", "" },
				{ "deploy.orchestration.run-as-disabled-check.ms", "" },
				{ "deploy.orchestration.package-check.ensure-user.ms", "" },
				{ "deploy.orchestration.package-check.run-as-pwd.ms", "" },
				{ "deploy.orchestration.package-check.run-as-pwd-pidof.ms", "" },
				{ "deploy.orchestration.package-check.readlink.ms", "" },
				{ "deploy.orchestration.package-check.system-app.ms", "" },
				{ "deploy.orchestration.package-check.evaluate.ms", "" },
				{ "deploy.orchestration.package-timestamp.path-stat.ms", "" },
				{ "deploy.orchestration.install.push-install.ms", "" },
				{ "deploy.orchestration.install.retry-delete.ms", "" },
				{ "deploy.orchestration.install.retry-uninstall.ms", "" },
				{ "deploy.orchestration.install.retry-reinstall.ms", "" },
				{ "deploy.orchestration.terminate.get-pid.ms", "" },
				{ "deploy.orchestration.terminate.kill.ms", "" },
				{ "pii.deploy.error", "" },
				{ "pii.deploy.file", "" },
			};

			internal void SetProperty (string key, bool? value)
			{
				Properties [key] = value?.ToString () ?? "False";
			}

			internal void SetProperty (string key, int? value)
			{
				Properties [key] = value?.ToString () ?? "-1";
			}

			internal void SetProperty (string key, long? value)
			{
				Properties [key] = value?.ToString () ?? "-1";
			}

			internal void SetProperty (string key, string value)
			{
				Properties [key] = value ?? "unknown";
			}
		}

		protected class RemoteFileInfo {
			public long Size { get; set; }
			public long ModifiedTime { get; set; }
		}

		void DebugHandler (string task, string message)
		{
			LogDiagnostic ($"DEBUG {task} {message}");
		}

		public override bool Execute ()
		{
			var phase = Stopwatch.StartNew ();
			Device = AndroidHelper.ParseTarget (AdbTarget, LogMessage, LogCodedError, logErrors: true, engine4: BuildEngine4);
			SetDiagnosticElapsed ("deploy.execute.parse-target.ms", phase);
			if (Device == null) {
				PrintDiagnostics ();
				return false;
			}
			LogMessage ($"Found device: {Device.ID}");

			phase.Restart ();
			if (string.IsNullOrEmpty (PrimaryCpuAbi) && !EmbedAssembliesIntoApk) {
				SetDiagnosticElapsed ("deploy.execute.no-abi-check.ms", phase);
				PrintDiagnostics ();
				LogCodedError ("XA0010", Resources.XA0010_NoAbi, Device.ID);
				return false;
			}
			SetDiagnosticElapsed ("deploy.execute.no-abi-check.ms", phase);

			phase.Restart ();
			var flagFilePath = GetFullPath (UploadFlagFile);
			lastUpload = File.GetLastWriteTimeUtc (flagFilePath);
			LogDiagnostic ($"LastWriteTime of `{flagFilePath}`: {lastUpload}");
			diagnosticData.Task = GetType ().Name;
			SetDiagnosticElapsed ("deploy.execute.upload-flag-stat.ms", phase);

			phase.Restart ();
			var lifetime = RegisteredTaskObjectLifetime.AppDomain;
			var key = ProjectSpecificTaskObjectKey ($"{Device.ID}_{PackageName}_{GetType ().Name}");
			if (!File.Exists (UploadFlagFile)) {
				packageInfo = new PackageInfo ();
			} else {
				packageInfo = BuildEngine4.GetRegisteredTaskObjectAssemblyLocal<PackageInfo> (key, lifetime) ?? new PackageInfo ();
			}
			SetDiagnosticElapsed ("deploy.execute.task-cache.ms", phase);

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
			var sw = Stopwatch.StartNew ();
			try {
				await RunInstall ();
			} catch {
				PrintDiagnostics ();
				throw;
			} finally {
				sw.Stop ();
				SaveDiagnosticData (sw.ElapsedMilliseconds);
			}
		}

		async Task RunInstall ()
		{
			var phase = Stopwatch.StartNew ();
			await Device.EnsureProperties (CancellationToken).ConfigureAwait (false);
			SetDiagnosticElapsed ("deploy.orchestration.ensure-properties.ms", phase);

			phase.Restart ();
			diagnosticData.SetProperty ("target.prop.ro.product.build.version.sdk", Device.Properties?.BuildVersionSdk);
			diagnosticData.SetProperty ("target.prop.ro.product.cpu.abilist", string.Join (";", Device.Properties?.ProductCpuAbiList ?? Array.Empty<string> ()));
			diagnosticData.SetProperty ("target.prop.ro.product.cpu.abi", PrimaryCpuAbi);
			diagnosticData.SetProperty ("target.prop.ro.product.manufacturer", Device.Properties?.ProductManufacturer);
			diagnosticData.SetProperty ("target.prop.ro.product.model", Device.Properties?.ProductModel);
			SetDiagnosticElapsed ("deploy.orchestration.property-capture.ms", phase);

			phase.Restart ();
			string redirectStdio = Device.Properties.Get ("log.redirect-stdio");
			SetDiagnosticElapsed ("deploy.orchestration.redirect-stdio-check.ms", phase);
			if (redirectStdio != null && string.Equals ("true", redirectStdio.Trim (), StringComparison.OrdinalIgnoreCase)) {
				LogFastDeploy2Error ("XA0128", Resources.XA0128_RedirectStdioIsEnabled);
				return;
			}

			phase.Restart ();
			string runAsDisabled = Device.Properties.Get ("ro.boot.disable_runas");
			SetDiagnosticElapsed ("deploy.orchestration.run-as-disabled-check.ms", phase);
			if (runAsDisabled != null && string.Equals ("true", runAsDisabled.Trim (), StringComparison.OrdinalIgnoreCase)) {
				LogFastDeploy2Error ("XA0131", Resources.XA0131_DeveloperModeNotEnabled);
				return;
			}
			SetDiagnosticElapsed ("deploy.orchestration.property-checks.ms", phase);

			phase.Restart ();
			await CheckAppInstalledAndDebuggable (PackageName);
			SetDiagnosticElapsed ("deploy.orchestration.package-check.ms", phase);

			if (EmbedAssembliesIntoApk) {
				await RemoveOverrideDirectory ();
			}

			if (ReInstall && !string.IsNullOrEmpty (PackageFile)) {
				await Device.UninstallPackage (PackageName, PreserveUserData, CancellationToken);
			}

			phase.Restart ();
			bool packageFileOutOfDate = !string.IsNullOrEmpty (PackageFile) &&
				(packageInfo.InternalPath.IndexOf ("unknown", StringComparison.OrdinalIgnoreCase) >= 0 || ReInstall || IsPackageFileOutOfDate ());
			SetDiagnosticElapsed ("deploy.orchestration.package-timestamp.ms", phase);

			if (packageFileOutOfDate) {
				try {
					phase.Restart ();
					await InstallPackage ();
					AddDiagnosticElapsed ("deploy.orchestration.install.ms", phase);
				} catch (Exception ex) {
					AddDiagnosticElapsed ("deploy.orchestration.install.ms", phase);
					LogFastDeploy2Error (GetErrorCode (ex), ex.ToString ());
					return;
				}
				if (!EmbedAssembliesIntoApk && packageInfo.InternalPath.IndexOf ("unknown", StringComparison.OrdinalIgnoreCase) >= 0) {
					packageInfo.InternalPath = null;
					phase.Restart ();
					await CheckAppInstalledAndDebuggable (PackageName);
					AddDiagnosticElapsed ("deploy.orchestration.package-check.ms", phase);
					if (RaiseRunAsError (packageInfo.InternalPath)) {
						return;
					}
				}
			}

			if (EmbedAssembliesIntoApk)
				return;

			phase.Restart ();
			if ((FastDevFiles?.Length ?? 0) == 0 && (EnvironmentFiles?.Length ?? 0) == 0) {
				SetDiagnosticElapsed ("deploy.orchestration.empty-check.ms", phase);
				return;
			}
			SetDiagnosticElapsed ("deploy.orchestration.empty-check.ms", phase);

			phase.Restart ();
			await TerminateApp ();
			SetDiagnosticElapsed ("deploy.orchestration.terminate.ms", phase);
			await DeployFastDevFilesWithAdbPush (OverrideFullPath);
		}

		bool IsPackageFileOutOfDate ()
		{
			var phase = Stopwatch.StartNew ();
			var packageFile = GetFullPath (PackageFile);
			var lastPackage = File.GetLastWriteTimeUtc (packageFile);
			LogDiagnostic ($"LastWriteTime of `{packageFile}`: {lastPackage}");
			SetDiagnosticElapsed ("deploy.orchestration.package-timestamp.path-stat.ms", phase);
			return lastUpload < lastPackage;
		}

		async Task CheckAppInstalledAndDebuggable (string packageName)
		{
			var phase = Stopwatch.StartNew ();
			packageInfo.UserId = UserID;
			packageInfo.PackageName = packageName;
			packageInfo.ProcessId = 0;
			await EnsureUserIsRunning ();
			SetDiagnosticElapsed ("deploy.orchestration.package-check.ensure-user.ms", phase);
			phase.Restart ();
			string packageInfoOutput = IsSafePackageNameForShell (packageName) ?
				await RunAs ("sh", "-c", $"pwd; pidof {packageName} 2>/dev/null || true") :
				await RunAs ("pwd");
			SetDiagnosticElapsed ("deploy.orchestration.package-check.run-as-pwd-pidof.ms", phase);
			ParsePackageInfoOutput (packageInfoOutput);
			if (string.IsNullOrEmpty (packageInfo.InternalPath)) {
				packageInfo.InternalPath = packageInfoOutput?.Trim ();
			}
			phase.Restart ();
			SetDiagnosticElapsed ("deploy.orchestration.package-check.run-as-pwd.ms", phase);
			if (packageInfo.InternalPath.IndexOf ("Permission denied", StringComparison.OrdinalIgnoreCase) >= 0) {
				phase.Restart ();
				packageInfo.InternalPath = await RunAs ("readlink", "-f", ".");
				SetDiagnosticElapsed ("deploy.orchestration.package-check.readlink.ms", phase);
			}
			phase.Restart ();
			if (packageInfo.InternalPath.IndexOf ("not an application", StringComparison.OrdinalIgnoreCase) >= 0) {
				LogDiagnostic ($"Package {packageInfo.PackageName} is a system application.");
				packageInfo.IsSystemApplication = true;
				diagnosticData.SetProperty ("deploy.systemapp", value: true);
				string whoami = await Device.RunShellCommand ("whoami");
				packageInfo.AdbIsRoot = whoami.Trim () == "root";
				LogDiagnostic ($"using {(packageInfo.AdbIsRoot ? "root" : $"su {packageInfo.UserId}")} to install fast deployment files.");
				packageInfo.InternalPath = $"/data/user/{(packageInfo.UserId ?? "0")}/{packageInfo.PackageName}";
				SetDiagnosticElapsed ("deploy.orchestration.package-check.system-app.ms", phase);
				return;
			}
			if (packageInfo.InternalPath.IndexOf ("not debuggable", StringComparison.OrdinalIgnoreCase) >= 0) {
				LogDiagnostic ($"Package {packageInfo.PackageName} was not debuggable. Forcing ReInstall");
				ReInstall = true;
				SetDiagnosticElapsed ("deploy.orchestration.package-check.evaluate.ms", phase);
				return;
			}
			if (packageInfo.InternalPath.IndexOf ("unknown", StringComparison.OrdinalIgnoreCase) >= 0) {
				LogDiagnostic ($"Package {packageInfo.PackageName} was not installed.");
				SetDiagnosticElapsed ("deploy.orchestration.package-check.evaluate.ms", phase);
				return;
			}
			if (packageInfo.InternalPath.IndexOf ("Permission denied", StringComparison.OrdinalIgnoreCase) >= 0) {
				LogDiagnostic ("run-as not supported on this device.");
				diagnosticData.SetProperty ("deploy.supports.fastdev", value: false);
			}
			SetDiagnosticElapsed ("deploy.orchestration.package-check.evaluate.ms", phase);
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
			string output = await Device.RunShellCommand (CancellationToken, "am", "start-user", "-w", userId);
			LogDiagnostic ($"'am start-user -w {userId}' returned: {(string.IsNullOrWhiteSpace (output) ? "<no output>" : output.Trim ())}");
		}

		async Task InstallPackage ()
		{
			LogDebugMessage ($"Installing Package {PackageName}");
			try {
				var phase = Stopwatch.StartNew ();
				await Device.PushAndInstallPackageAsync (new PushAndInstallCommand {
					ApkFile = PackageFile,
					PackageName = PackageName,
					ReInstall = ReInstall,
					User = UserID,
					TestOnly = IsTestOnly,
				}, token: CancellationToken);
				SetDiagnosticElapsed ("deploy.orchestration.install.push-install.ms", phase);
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
			var phase = Stopwatch.StartNew ();
			try {
				await Device.DeleteFile (e.PackageFile, true, CancellationToken);
			} catch {
			}
			SetDiagnosticElapsed ("deploy.orchestration.install.retry-delete.ms", phase);
			bool preserveData = !(e is RequiresUninstallException);
			LogDebugMessage (string.Format ("Forcing complete uninstall of '{0}'... Preserving Data: {1}", PackageName, preserveData));
			var uninstallCommand = new PmUninstallCommand () { PackageName = PackageName, User = UserID, PreserveData = preserveData };
			phase.Restart ();
			await Device.UninstallPackage (uninstallCommand, cancellationToken: CancellationToken);
			SetDiagnosticElapsed ("deploy.orchestration.install.retry-uninstall.ms", phase);
			LogDebugMessage (string.Format ("Installing '{0}'...", PackageName));
			phase.Restart ();
			await Device.PushAndInstallPackageAsync (new PushAndInstallCommand {
				ApkFile = PackageFile,
				PackageName = PackageName,
				ReInstall = false,
				User = UserID
			}, token: CancellationToken);
			SetDiagnosticElapsed ("deploy.orchestration.install.retry-reinstall.ms", phase);
			return false;
		}

		async Task RemoveOverrideDirectory ()
		{
			await RunAs ("rm", "-Rf", OverrideFullPath);
		}

		async Task TerminateApp ()
		{
			var phase = Stopwatch.StartNew ();
			var pid = packageInfo.ProcessId;
			if (pid == 0 && packageInfo.IsSystemApplication) {
				pid = await Device.GetProcessId (PackageName, CancellationToken);
			}
			SetDiagnosticElapsed ("deploy.orchestration.terminate.get-pid.ms", phase);
			if (pid == 0) {
				LogDebugMessage ($"{PackageName} was not running, skipping kill");
				return;
			}
			LogDebugMessage ($"Terminating {PackageName}...");
			phase.Restart ();
			await Device.KillProcessAndWaitForExit (PackageName, CancellationToken);
			SetDiagnosticElapsed ("deploy.orchestration.terminate.kill.ms", phase);
			LogDebugMessage ($"{PackageName} Terminated.");
		}

		protected virtual async Task<bool> DeployFastDevFilesWithAdbPush (string overridePath)
		{
			string stagingDirectory = GetLocalStagingDirectory ();
			var phase = Stopwatch.StartNew ();
			var stagedFiles = PrepareAdbPushStagingDirectory (stagingDirectory);
			SetDiagnosticElapsed ("deploy.fastdeploy2.local.stage.ms", phase);
			if (stagedFiles.Count == 0) {
				LogDiagnostic ("No FastDev files were staged for adb push deployment.");
				return true;
			}

			string remoteStagingPath = GetRemoteAdbPushStagingPath ();
			phase.Restart ();
			string output = await Device.RunShellCommand (CancellationToken, "mkdir", "-p", remoteStagingPath);
			SetDiagnosticElapsed ("deploy.fastdeploy2.remote.mkdir.ms", phase);
			if (IsShellError (output, "mkdir")) {
				LogFastDeploy2Error ("XA0129", output, remoteStagingPath);
				return false;
			}

			if (!await RemoveStaleRemoteStagingFiles (remoteStagingPath, stagedFiles)) {
				return false;
			}

			phase.Restart ();
			if (!await UploadStagingDirectory (stagingDirectory, remoteStagingPath)) {
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

			if (!await RemoveStaleOverrideFiles (overridePath, stagedFileData, overrideFileData)) {
				return false;
			}

			return await CopyChangedFiles (remoteStagingPath, overridePath, stagedFileData, overrideFileData);
		}

		protected HashSet<string> PrepareAdbPushStagingDirectory (string stagingDirectory)
		{
			if (Directory.Exists (stagingDirectory)) {
				Directory.Delete (stagingDirectory, recursive: true);
			}
			Directory.CreateDirectory (stagingDirectory);

			var stagedFiles = new HashSet<string> (StringComparer.Ordinal);
			foreach (var file in FastDevFiles ?? Array.Empty<ITaskItem> ()) {
				if (!File.Exists (file.ItemSpec)) {
					LogDebugMessage ($"File '{file.ItemSpec}' does not exists. Skipping.");
					continue;
				}
				if (Path.GetExtension (file.ItemSpec) == ".so") {
					string abi = AndroidRidAbiHelper.GetNativeLibraryAbi (file);
					if (abi != PrimaryCpuAbi) {
						LogDebugMessage ($"NotifySync SkipCopyFile {file.ItemSpec} abi not suitable for this device.");
						continue;
					}
				}

				string targetPath = GetAdbPushTargetPath (file);
				string destination = GetStagingFilePath (stagingDirectory, targetPath);
				Directory.CreateDirectory (Path.GetDirectoryName (destination));
				File.Copy (file.ItemSpec, destination, overwrite: true);
				File.SetLastWriteTimeUtc (destination, File.GetLastWriteTimeUtc (file.ItemSpec));
				stagedFiles.Add (targetPath.Replace ("\\", "/"));
				LogDiagnostic ($"Staged {file.ItemSpec} => {targetPath}");
			}

			if (EnvironmentFiles?.Length > 0) {
				string targetPath = $"{PrimaryCpuAbi}/environment";
				string destination = GetStagingFilePath (stagingDirectory, targetPath);
				Directory.CreateDirectory (Path.GetDirectoryName (destination));
				byte [] environmentData = CreateEnvironmentFileData (EnvironmentFiles, out DateTime newestFileDateTime);
				if (environmentData.Length > 0) {
					File.WriteAllBytes (destination, environmentData);
					File.SetLastWriteTimeUtc (destination, newestFileDateTime);
					stagedFiles.Add (targetPath);
					LogDiagnostic ($"Staged @(AndroidEnvironment) files => {targetPath}");
				}
			}

			return stagedFiles;
		}

		string GetAdbPushTargetPath (ITaskItem file)
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

		static string GetStagingFilePath (string stagingDirectory, string targetPath)
		{
			string fullStagingDirectory = Path.GetFullPath (stagingDirectory);
			string destination = Path.GetFullPath (Path.Combine (fullStagingDirectory, targetPath.Replace ('/', Path.DirectorySeparatorChar)));
			string stagingPrefix = fullStagingDirectory.EndsWith (Path.DirectorySeparatorChar.ToString (), StringComparison.Ordinal) ?
				fullStagingDirectory :
				fullStagingDirectory + Path.DirectorySeparatorChar;
			if (!destination.StartsWith (stagingPrefix, StringComparison.Ordinal)) {
				throw new InvalidOperationException ($"FastDev target path '{targetPath}' escapes staging directory '{stagingDirectory}'.");
			}
			return destination;
		}

		byte [] CreateEnvironmentFileData (ITaskItem [] environments, out DateTime newestFileDateTime)
		{
			int maxKeyLength = 0;
			int maxValueLength = 0;
			newestFileDateTime = DateTime.MinValue;
			var data = new Dictionary<string, string> ();
			foreach (ITaskItem env in environments ?? Array.Empty<ITaskItem> ()) {
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

		protected async Task<bool> RemoveStaleRemoteStagingFiles (string remoteStagingPath, HashSet<string> stagedFiles)
		{
			var phase = Stopwatch.StartNew ();
			string filelist = await Device.RunShellCommand (CancellationToken, "find", remoteStagingPath, "-type", "f");
			if (IsShellError (filelist, "find")) {
				LogFastDeploy2Error ("XA0129", filelist, remoteStagingPath);
				return false;
			}

			string prefix = remoteStagingPath.TrimEnd ('/') + "/";
			var staleFiles = new List<string> ();
			foreach (string line in filelist.Split (new char [] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
				string remoteFile = line.Trim ();
				if (!remoteFile.StartsWith (prefix, StringComparison.Ordinal)) {
					continue;
				}
				string relativePath = remoteFile.Substring (prefix.Length);
				if (!stagedFiles.Contains (relativePath)) {
					staleFiles.Add (remoteFile);
				}
			}

			for (int i = 0; i < staleFiles.Count; i += StaleFileRemovalBatchSize) {
				var args = new List<string> { "rm", "-f" };
				args.AddRange (staleFiles.Skip (i).Take (StaleFileRemovalBatchSize));
				string output = await Device.RunShellCommand (CancellationToken, args.ToArray ());
				if (IsShellError (output, "rm")) {
					LogFastDeploy2Error ("XA0129", output, remoteStagingPath);
					return false;
				}
			}

			SetDiagnosticElapsed ("deploy.fastdeploy2.remote.staging.cleanup.ms", phase);
			return true;
		}

		protected async Task<Dictionary<string, RemoteFileInfo>> GetRemoteFileData (string rootPath, bool runAs)
		{
			string output;
			if (runAs) {
				output = await RunAs ("find", rootPath, "-type", "f", "-exec", "stat", "-c", "%n|%s|%Y", "{}", "+");
				if (RaiseRunAsError (output)) {
					return null;
				}
			} else {
				output = await Device.RunShellCommand (CancellationToken, "find", rootPath, "-type", "f", "-exec", "stat", "-c", "%n|%s|%Y", "{}", "+");
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
			var phase = Stopwatch.StartNew ();
			var staleFiles = new List<string> ();
			foreach (var file in overrideFiles.Keys) {
				if (!stagedFiles.ContainsKey (file)) {
					staleFiles.Add ($"{overridePath}/{file}");
				}
			}

			LogDiagnostic ($"FastDeploy2 removing {staleFiles.Count} stale override files.");
			diagnosticData.SetProperty ("deploy.fastdeploy2.stale.files", staleFiles.Count);
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

		async Task<bool> CopyChangedFiles (string remoteStagingPath, string overridePath, Dictionary<string, RemoteFileInfo> stagedFiles, Dictionary<string, RemoteFileInfo> overrideFiles)
		{
			var phase = Stopwatch.StartNew ();
			var changedFiles = new List<string> ();
			foreach (var file in stagedFiles) {
				if (!overrideFiles.TryGetValue (file.Key, out RemoteFileInfo existing) ||
						existing.Size != file.Value.Size ||
						existing.ModifiedTime != file.Value.ModifiedTime) {
					changedFiles.Add (file.Key);
				}
			}
			SetDiagnosticElapsed ("deploy.fastdeploy2.compare.ms", phase);

			LogDiagnostic ($"FastDeploy2 copying {changedFiles.Count} changed override files.");
			diagnosticData.SetProperty ("deploy.fastdeploy2.changed.files", changedFiles.Count);
			var filesByDirectory = new Dictionary<string, List<string>> (StringComparer.Ordinal);
			foreach (string file in changedFiles) {
				string directory = Path.GetDirectoryName (file)?.Replace ("\\", "/") ?? "";
				if (!filesByDirectory.TryGetValue (directory, out List<string> files)) {
					files = new List<string> ();
					filesByDirectory.Add (directory, files);
				}
				files.Add (file);
			}

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

		protected void SetDiagnosticElapsed (string key, Stopwatch stopwatch)
		{
			diagnosticData.SetProperty (key, stopwatch.ElapsedMilliseconds);
		}

		protected void AddDiagnosticElapsed (string key, Stopwatch stopwatch)
		{
			if (!long.TryParse (diagnosticData.Properties [key], out long current)) {
				current = 0;
			}
			diagnosticData.SetProperty (key, current + stopwatch.ElapsedMilliseconds);
		}

		protected void SetDiagnosticProperty (string key, int value)
		{
			diagnosticData.SetProperty (key, value);
		}

		protected void SetDiagnosticProperty (string key, string value)
		{
			diagnosticData.SetProperty (key, value);
		}

		protected virtual string GetLocalStagingDirectory ()
		{
			return Path.Combine (GetFullPath (IntermediateOutputPath), "fastdeploy2");
		}

		protected virtual async Task<bool> UploadStagingDirectory (string stagingDirectory, string remoteStagingPath)
		{
			var args = new List<string> { "push" };
			if (!string.IsNullOrEmpty (AdbPushCompressionAlgorithm)) {
				args.Add ("-z");
				args.Add (AdbPushCompressionAlgorithm);
			}
			args.Add ("--sync");
			args.Add (Path.Combine (stagingDirectory, "."));
			args.Add (remoteStagingPath);

			var result = await RunAdbCommand (args.ToArray ());
			if (result.ExitCode != 0) {
				LogFastDeploy2Error ("XA0129", result.Output, stagingDirectory);
				return false;
			}
			SetAdbPushFileCounts (result.Output);
			LogDiagnostic (result.Output);
			return true;
		}

		protected void SetAdbPushFileCounts (string output)
		{
			var match = AdbPushSummaryRegex.Match (output ?? "");
			if (!match.Success) {
				return;
			}
			diagnosticData.SetProperty ("deploy.fastdeploy2.adb.pushed.files", match.Groups ["pushed"].Value);
			diagnosticData.SetProperty ("deploy.fastdeploy2.adb.skipped.files", match.Groups ["skipped"].Value);
		}

		protected async Task<AdbCommandResult> RunAdbCommand (params string [] arguments)
		{
			return await RunAdbCommand (arguments, environmentVariables: null);
		}

		protected async Task<AdbCommandResult> RunAdbCommand (string [] arguments, Dictionary<string, string> environmentVariables)
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

			LogDiagnostic ($"Running adb: {psi.FileName} {psi.Arguments}");
			using (var process = new Process ()) {
				process.StartInfo = psi;
				process.OutputDataReceived += (sender, e) => {
					if (e.Data != null) {
						lock (stdout) {
							stdout.AppendLine (e.Data);
						}
						LogDiagnostic (e.Data);
					} else {
						stdoutCompleted.Set ();
					}
				};
				process.ErrorDataReceived += (sender, e) => {
					if (e.Data != null) {
						lock (stderr) {
							stderr.AppendLine (e.Data);
						}
						LogDiagnostic (e.Data);
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
				return new AdbCommandResult {
					ExitCode = process.ExitCode,
					Output = $"{stdout}{stderr}".Trim (),
				};
			}
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

		protected async Task<string> RunAs (params string [] arguments)
		{
			List<string> args = BuildRunAsArgs ();
			args.AddRange (arguments);
			string result = await Device.RunShellCommand (CancellationToken, args.ToArray ());
			LogDebugMessage ($"{arguments [0]} returned: {result}");
			return result;
		}

		protected string ResolveAdbPath ()
		{
			var exe = string.IsNullOrEmpty (AdbToolExe) ? "adb" : AdbToolExe;
			return string.IsNullOrEmpty (AdbToolPath) ? exe : Path.Combine (AdbToolPath, exe);
		}

		protected virtual string GetRemoteAdbPushStagingPath ()
		{
			var user = string.IsNullOrEmpty (UserID) ? "0" : UserID;
			return $"{RemoteStagingRoot}/{PackageName}/{user}";
		}

		protected void LogFastDeploy2Error (string errorCode, string error, string file = "")
		{
			LogDiagnosticDataError (errorCode, error, file);
			PrintDiagnostics ();
			if (errorCode == "XA0129") {
				LogCodedError (errorCode, Resources.XA0129_ErrorDeployingFile, file);
			} else {
				LogCodedError (errorCode, error);
			}
		}

		protected void LogDiagnostic (string message)
		{
			if (DiagnosticLogging) {
				LogDebugMessage (message);
				return;
			}
			diagnosticLogs.Enqueue (message);
		}

		void PrintDiagnostics ()
		{
			while (diagnosticLogs.Count > 0) {
				LogMessage (diagnosticLogs.Dequeue ());
			}
			LogMessage ($"{diagnosticData.Task}");
			foreach (var t in diagnosticData.Properties) {
				LogMessage ($"\t{t.Key}: {t.Value}");
			}
		}

		void LogDiagnosticDataError (string errorCode, string error, string file = "")
		{
			diagnosticData.SetProperty ("deploy.result", "Failed");
			if (!string.IsNullOrEmpty (file))
				diagnosticData.SetProperty ("pii.deploy.file", file);
			diagnosticData.SetProperty ("pii.deploy.error", error);
			diagnosticData.SetProperty ("deploy.error.code", errorCode);
		}

		void SaveDiagnosticData (long ms)
		{
			JsonSerializerOptions options = new JsonSerializerOptions {
				WriteIndented = true
			};
			diagnosticData.SetProperty ("deploy.duration.ms", ms);
			string newPath = Path.Combine (IntermediateOutputPath, "diagnostics", $"{GetType ().Name.ToLowerInvariant ()}.json");
			File.WriteAllText (newPath, JsonSerializer.Serialize (diagnosticData, options));
		}

		protected string GetFullPath (string dir) => Path.IsPathRooted (dir) ? dir : Path.GetFullPath (Path.Combine (WorkingDirectory, dir));

		protected bool RaiseRunAsError (string error)
		{
			if (TryGetRunAsErrorCode (error, out var err)) {
				LogDiagnosticDataError (err.code, err.message);
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
			switch (ex) {
				case IncompatibleCpuAbiException e:
					return "ADB0020";
				case RequiresUninstallException e:
					return "ADB0030";
				case SdkNotSupportedException e:
					return "ADB0040";
				case PackageAlreadyExistsException e:
					return "ADB0050";
				case InsufficientSpaceException e:
					return "ADB0060";
				case InstallFailedException e:
					return "ADB0010";
				default:
					return GetErrorCode (ex.Message);
			}
		}

		static string GetErrorCode (string message)
		{
			foreach (var errorCode in error_codes)
				if (message.IndexOf (errorCode.message, StringComparison.OrdinalIgnoreCase) >= 0)
					return errorCode.code;

			return "ADB1000";
		}

		protected static bool IsShellError (string output, string command)
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

		protected static bool IsMissingDirectoryError (string output)
		{
			return !string.IsNullOrEmpty (output) &&
				output.IndexOf ("No such file or directory", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		protected struct AdbCommandResult
		{
			public int ExitCode;
			public string Output;
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
