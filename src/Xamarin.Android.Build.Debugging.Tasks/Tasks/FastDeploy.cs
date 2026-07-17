using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.IO;
using System.Linq;
using System.Net;
using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Android.Build.Tasks;
using Mono.AndroidTools;
using Xamarin.Android.Build.Debugging.Tasks.Properties;

using K4os.Compression.LZ4;

using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks
{
	public class FastDeploy : AsyncTask
	{
		const string XAToolsTempPath = "/data/local/tmp/.xatools";
		const string OverridePath = "files/.__override__";
		const string ToolsPath = "files/.__tools__";
		const int MAX_COMMAND = 4096;
		const int ADB_COMMAND_PADDING = 100;
		
		public override string TaskPrefix => "FD";

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
		public bool ResetOverrideDirectory { get; set; }

		[Required]
		public string FastDevToolPath { get; set; }

		public string FastDevTool { get; set; } = "xamarin.sync";
		public string FastDevFindTool { get; set; } = "xamarin.find";
		public string FastDevStatTool { get; set; } = "xamarin.stat";
		public string FastDevCpTool { get; set; } = "xamarin.cp";

		[Required]
		public string ToolVersion { get; set; }

		public bool DiagnosticLogging  { get; set; } = false;

		public bool UsingAndroidNETSdk { get; set; }

		public string UserID { get; set; }

		public bool IsTestOnly { get; set; }

		[Required]
		public string IntermediateOutputPath { get; set; }
		public ITaskItem[] EnvironmentFiles { get; set; }

		AndroidDevice Device;
		DateTime lastUpload = DateTime.MinValue;

		internal class PackageInfo {
			string internalPath = null;
			public string InternalPath {
				get { return internalPath; }
				set {
					internalPath = value?.Trim () ?? null;
				}
			}

			public string ToolVersion { get; set; }
			public int? BlockSize { get; set; }
			public bool SupportsFastDev { get; set; } = true;
			public bool IsSystemApplication { get; set; } = false;
			public bool AdbIsRoot { get; set; } = false;
			public string UserId { get; set; } = null;
			public string PackageName { get; set; } = null;
			public bool DiagnosticLogging { get; set; } = false;
			public Action<string> LogDebugMessage;

		}

		private class DiagnosticData {
			[JsonPropertyName ("Task")]
			public string Task { get; set; } = nameof (FastDeploy);
			[JsonPropertyName ("Properties")]
			public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>() {
				{ "target.prop.ro.product.build.version.sdk", "" },
				{ "target.prop.ro.product.cpu.abilist", "" },
				{ "target.prop.ro.product.manufacturer", "" },
				{ "target.prop.ro.product.model", "" },
				{ "target.prop.ro.product.cpu.abi", ""},
				{ "deploy.error.code", ""},
				{ "deploy.tool", "xamarin.sync" },
				{ "deploy.result", "Success" },
				{ "deploy.supports.fastdev", "True" },
				{ "deploy.systemapp", "False" },
				{ "deploy.duration.ms", "0" },
				{ "pii.deploy.error", "" },
				{ "pii.deploy.file", "" },
			};

			internal void SetProperty (string key, bool? value)
			{
				Properties[key] = value?.ToString () ?? "False";
			}

			internal void SetProperty (string key, int? value)
			{
				Properties[key] = value?.ToString () ?? "-1";
			}

			internal void SetProperty (string key, long? value)
			{
				Properties[key] = value?.ToString () ?? "-1";
			}

			internal void SetProperty (string key, string value)
			{
				Properties[key] = value ?? "unknown";
			}
		}

		PackageInfo packageInfo;
		Stopwatch stopWatch = new Stopwatch ();

		Queue<string> diagnosticLogs = new Queue<string> ();

		DiagnosticData diagnosticData = new DiagnosticData ();

		protected string ToolsFullPath {
			get { return packageInfo.IsSystemApplication ? $"{packageInfo.InternalPath}/{ToolsPath}" : ToolsPath; }
		}

		protected string OverrideFullPath {
			get { return packageInfo.IsSystemApplication ? $"{packageInfo.InternalPath}/{OverridePath}" : OverridePath; }
		}

		void StartTiming ()
		{
			stopWatch.Restart ();
		}

		long GetElapsedTimeAndRestart ()
		{
			stopWatch.Stop ();
			long elapsedTime = stopWatch.ElapsedMilliseconds;
			stopWatch.Restart ();
			return elapsedTime;
		}

		void DebugHandler (string task, string message)
		{
			LogDiagnostic ($"DEBUG {task} {message} [{GetElapsedTimeAndRestart ()}ms]");
		}

		void LogDebugMessageWithTiming (string message)
		{
			LogDiagnostic ($"{message} [{GetElapsedTimeAndRestart ()}ms]");
		}

		void LogDiagnostic (string message)
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
			string newPath = Path.Combine(IntermediateOutputPath, "diagnostics",  "fastdeploy.json");
			File.WriteAllText (newPath, JsonSerializer.Serialize (diagnosticData, options));
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

			var lifetime = RegisteredTaskObjectLifetime.AppDomain;
			var key = ProjectSpecificTaskObjectKey ($"{Device.ID}_{PackageName}");
			if (!File.Exists (UploadFlagFile)) {
				packageInfo = new PackageInfo ();
			} else {
				packageInfo = BuildEngine4.GetRegisteredTaskObjectAssemblyLocal<PackageInfo>(key, lifetime) ?? new PackageInfo ();
			}
			packageInfo.DiagnosticLogging = DiagnosticLogging;
			packageInfo.LogDebugMessage = LogDiagnostic;
			AndroidLogger.Debug += DebugHandler;
			try {
				var flagFilePath = GetFullPath (UploadFlagFile);
				lastUpload = File.GetLastWriteTimeUtc (flagFilePath);
				LogDiagnostic ($"LastWriteTime of `{flagFilePath}`: {lastUpload}");
				StartTiming ();
				return base.Execute ();
			} finally {
				BuildEngine4.RegisterTaskObjectAssemblyLocal (key, packageInfo, lifetime, allowEarlyCollection: false);
				stopWatch.Stop ();
				AndroidLogger.Debug -= DebugHandler;
			}
		}

		public async override Task RunTaskAsync ()
		{
			var sw = new Stopwatch ();
			sw.Restart ();
			try {
				await RunInstall ();
			} catch {
				PrintDiagnostics ();
				throw;
			} finally {
				sw.Stop();
				SaveDiagnosticData (sw.ElapsedMilliseconds);
			}
		}

		public async Task RunInstall ()
		{
			await Device.EnsureProperties (CancellationToken).ConfigureAwait (false);

			diagnosticData.SetProperty ("target.prop.ro.product.build.version.sdk", Device.Properties?.BuildVersionSdk);
			diagnosticData.SetProperty ("target.prop.ro.product.cpu.abilist", string.Join (";", Device.Properties?.ProductCpuAbiList ?? Array.Empty<string> ()));
			diagnosticData.SetProperty ("target.prop.ro.product.cpu.abi", PrimaryCpuAbi);
			diagnosticData.SetProperty ("target.prop.ro.product.manufacturer", Device.Properties?.ProductManufacturer);
			diagnosticData.SetProperty ("target.prop.ro.product.model", Device.Properties?.ProductModel);

			string redirectStdio = Device.Properties.Get ("log.redirect-stdio");
			if (redirectStdio != null && string.Equals ("true", redirectStdio.Trim (), StringComparison.OrdinalIgnoreCase)) {
				LogDiagnosticDataError ("XA0128", Resources.XA0128_RedirectStdioIsEnabled);
				PrintDiagnostics ();
				LogCodedError ($"XA0128", Resources.XA0128_RedirectStdioIsEnabled);
				return;
			}

			string runAsDisabled = Device.Properties.Get ("ro.boot.disable_runas");
			if (runAsDisabled != null && string.Equals ("true", runAsDisabled.Trim (), StringComparison.OrdinalIgnoreCase)) {
					LogDiagnosticDataError ("XA0131", Resources.XA0131_DeveloperModeNotEnabled);
					PrintDiagnostics ();
					LogCodedError ($"XA0131", Resources.XA0131_DeveloperModeNotEnabled);
					return;
			}

			await CheckAppInstalledAndDebuggable (PackageName);

			if (EmbedAssembliesIntoApk) {
				// we need to remove the .__override__ directory BEFORE we uninstall the debug apk.
				// this is because run-as does NOT work on release apps.
				await RemoveOverrideDirectory();
			}

			if (ReInstall && !string.IsNullOrEmpty (PackageFile)) {
				await Device.UninstallPackage (PackageName, PreserveUserData, CancellationToken);
			}
			if (!string.IsNullOrEmpty (PackageFile) &&
					(packageInfo.InternalPath.IndexOf ("unknown", StringComparison.OrdinalIgnoreCase) >= 0 || ReInstall || IsPackageFileOutOfDate ())) {
				try {
					await InstallPackage (!(packageInfo.InternalPath.IndexOf ("unknown", StringComparison.OrdinalIgnoreCase) >= 0));
				} catch (Exception ex) {
					LogDiagnosticDataError (GetErrorCode (ex), ex.ToString ());
					PrintDiagnostics ();
					LogCodedError (GetErrorCode (ex), ex.ToString ());
					return;
				}

				// `pm install` can report success (or empty output) yet leave the package
				// absent on the device; that only surfaces later as an opaque XA0137 run-as
				// "couldn't stat /data/user/N/<pkg>" failure during the post-install probe.
				// Positively confirm the package landed via `pm path` and, if it did not,
				// force a single reinstall before continuing.
				if (!await IsPackageInstalled (PackageName)) {
					LogDiagnostic ($"`pm path {PackageName}` reported no package after a successful-looking install; forcing a reinstall.");
					diagnosticData.SetProperty ("deploy.reinstall.after.missing.package", value: true);
					await LogAvailableDiskSpace ();
					ReInstall = true;
					try {
						await InstallPackage (installed: false);
					} catch (Exception ex) {
						LogDiagnosticDataError (GetErrorCode (ex), ex.ToString ());
						PrintDiagnostics ();
						LogCodedError (GetErrorCode (ex), ex.ToString ());
						return;
					}
					if (!await IsPackageInstalled (PackageName)) {
						LogDiagnostic ($"`pm path {PackageName}` still reports no package after reinstall.");
						LogDiagnosticDataError ("XA0132", Resources.XA0132_PackageNotInstalled);
						PrintDiagnostics ();
						LogCodedError ("XA0132", Resources.XA0132_PackageNotInstalled);
						return;
					}
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

			if (ResetOverrideDirectory && !await ResetOverrideDirectoryForConfigurationChange ()) {
				return;
			}

			if (!await InstallFastDevTools (ToolsFullPath)) {
				return;
			}
			
			if (FastDevFiles?.Any () ?? false) {
				await TerminateApp ();
				await DeployFastDevFiles (ToolsFullPath, OverrideFullPath);
			}

			return;
		}

		async Task<bool> ResetOverrideDirectoryForConfigurationChange ()
		{
			LogDebugMessage ($"Removing {OverrideFullPath} after the fast deployment configuration changed.");
			string output = await Device.RunAs (packageInfo, "rm", "-Rf", OverrideFullPath);
			if (RaiseRunAsError (output)) {
				return false;
			}
			if (output.IndexOf ("rm:", StringComparison.OrdinalIgnoreCase) >= 0) {
				LogDiagnosticDataError ("XA0129", output, OverrideFullPath);
				PrintDiagnostics ();
				LogCodedError ("XA0129", Resources.XA0129_ErrorDeployingFile, OverrideFullPath);
				return false;
			}
			return true;
		}

		bool IsPackageFileOutOfDate ()
		{
			var packageFile = GetFullPath (PackageFile);
			var lastPackage = File.GetLastWriteTimeUtc (packageFile);
			LogDiagnostic ($"LastWriteTime of `{packageFile}`: {lastPackage}");
			return lastUpload < lastPackage;
		}

		int CompressLZ4 (ref byte [] data, int len, ref byte [] outBuffer, LZ4Level lZ4Level = LZ4Level.L00_FAST)
		{
			int compressedLength = LZ4Codec.Encode (data, 0, len, outBuffer, 0, outBuffer.Length, lZ4Level);
			if (compressedLength < 0 || compressedLength >= outBuffer.Length) {
				if (DiagnosticLogging)
					LogDebugMessage ($"Sending Data Uncompressed.");
				compressedLength = outBuffer.Length;
				data.CopyTo (outBuffer, 0);
			}
			return compressedLength;
		}

		async Task CheckAppInstalledAndDebuggable (string packageName)
		{
			packageInfo.UserId = UserID;
			packageInfo.PackageName = packageName;
			await EnsureUserIsRunning ();
			packageInfo.InternalPath = packageInfo.InternalPath ?? await QueryInternalPathWithRetry ();
			if (packageInfo.InternalPath.IndexOf ("Permission denied", StringComparison.OrdinalIgnoreCase) >= 0) {
				packageInfo.InternalPath = await Device.RunAs (packageInfo, "readlink", "-f", ".");
			}
			if (packageInfo.InternalPath.IndexOf ("not an application", StringComparison.OrdinalIgnoreCase) >= 0) {
				LogDiagnostic ($"Package {packageInfo.PackageName} is a system application.");
				packageInfo.IsSystemApplication = true;
				diagnosticData.SetProperty ("deploy.systemapp", value:true);
				string whoami = await Device.RunShellCommand ("whoami");
				packageInfo.AdbIsRoot = whoami.Trim () == "root";
				LogDiagnostic ($"using {(packageInfo.AdbIsRoot ? "root" : $"su {packageInfo.UserId}")} to install fast deployment files. ");
				packageInfo.InternalPath = $"/data/user/{(packageInfo.UserId ?? "0")}/{packageInfo.PackageName}";
				return;
			}
			if (packageInfo.InternalPath.IndexOf ("not debuggable", StringComparison.OrdinalIgnoreCase) >= 0) {
				// current install is not debuggable, so lets uninstall it
				LogDiagnostic ($"Package {packageInfo.PackageName} was not debuggable. Forcing ReInstall");
				ReInstall = true;
				return;
			}
			if (packageInfo.InternalPath.IndexOf ("unknown", StringComparison.OrdinalIgnoreCase) >= 0) {
				LogDiagnostic ($"Package {packageInfo.PackageName} was not installed.");
				return;
			}
			if (packageInfo.InternalPath.IndexOf ("Permission denied", StringComparison.OrdinalIgnoreCase) >= 0) {
				// run-as is probably not supported.
				LogDiagnostic ("run-as not supported on this device.");
				packageInfo.SupportsFastDev = false;
				diagnosticData.SetProperty ("deploy.supports.fastdev", value: false);
			}
			return;
		}

		/// <summary>
		/// Issues the first <c>run-as &lt;pkg&gt; pwd</c> query, retrying briefly while the
		/// per-user data directory is not yet stat-able through <c>run-as</c>.
		/// </summary>
		/// <remarks>
		/// <para>Immediately after <c>pm install</c>, the per-user data directory
		/// <c>/data/user/N/&lt;pkg&gt;</c> may not yet be stat-able through <c>run-as</c>,
		/// even for the primary user (id 0). During that window <c>run-as</c> returns
		/// <c>run-as: couldn't stat /data/user/N/&lt;pkg&gt;: No such file or directory</c>,
		/// which otherwise raises <c>XA0137</c> and disables Fast Deployment. This races
		/// install on the primary user ~daily in CI. Poll for a bounded period to let the
		/// directory materialize before giving up. See
		/// https://github.com/dotnet/android/issues/7821 and
		/// https://github.com/dotnet/android/issues/11808.</para>
		/// <para>Retry policy: up to 10 attempts with a 500 ms delay between each, giving
		/// a maximum wait of 4.5 seconds before the error is surfaced as <c>XA0137</c>.
		/// Only the transient <c>couldn't stat … No such file or directory</c> signature
		/// (detected by <see cref="IsTransientRunAsStatRace"/>) triggers a retry; all other
		/// <c>run-as</c> failures are surfaced immediately.</para>
		/// </remarks>
		async Task<string> QueryInternalPathWithRetry ()
		{
			const int maxAttempts = 10;
			var delay = TimeSpan.FromMilliseconds (500);
			string result = await Device.RunAs (packageInfo, "pwd");
			for (int attempt = 1; attempt < maxAttempts && IsTransientRunAsStatRace (result); attempt++) {
				LogDiagnostic ($"run-as could not stat the data directory for {packageInfo.PackageName} yet (attempt {attempt}/{maxAttempts}); retrying in {delay.TotalMilliseconds:0} ms. Output: {result?.Trim ()}");
				await Task.Delay (delay, CancellationToken);
				result = await Device.RunAs (packageInfo, "pwd");
			}
			return result;
		}

		/// <summary>
		/// Returns <see langword="true"/> when a <c>run-as</c> result matches the transient
		/// install-vs-run-as race signature (<c>couldn't stat … No such file or directory</c>),
		/// i.e. the per-user data directory has not yet materialized after <c>pm install</c>.
		/// </summary>
		internal static bool IsTransientRunAsStatRace (string result)
		{
			if (string.IsNullOrEmpty (result)) {
				return false;
			}
			return result.IndexOf ("couldn't stat", StringComparison.OrdinalIgnoreCase) >= 0 &&
				result.IndexOf ("No such file or directory", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		/// <summary>
		/// Ensures the secondary Android user targeted by this deployment is in the
		/// 'running' state before any <c>run-as</c> query is issued against it.
		/// </summary>
		/// <remarks>
		/// <para><c>pm install --user N &lt;apk&gt;</c> registers the package but does
		/// not materialize the per-user data directory <c>/data/user/N/&lt;pkg&gt;</c>;
		/// that directory is only created once user <c>N</c> is brought to the running
		/// state. Until then, every <c>run-as &lt;pkg&gt; --user N</c> invocation fails
		/// with <c>run-as: couldn't stat /data/user/N/&lt;pkg&gt;: No such file or
		/// directory</c> and raises <c>XA0137</c>. See
		/// https://github.com/dotnet/android/issues/7821.</para>
		/// <para>Empirical measurement on an arm64 API-34 emulator (N=50) showed that
		/// without this step, 52% of installs fail <c>run-as</c> at <c>t=0</c> and 30%
		/// never recover even after 30 seconds of polling — i.e. polling alone is not
		/// sufficient. <c>am start-user -w N</c> succeeds in 100/100 attempts within
		/// ~134 ms median (max 363 ms), is idempotent, and is a cheap no-op when the
		/// user is already running. The primary user (id 0) never requires this step,
		/// so skip it to avoid any cost in the common case.</para>
		/// </remarks>
		async Task EnsureUserIsRunning ()
		{
			var userId = (UserID ?? string.Empty).Trim ();
			if (userId.Length == 0 || (int.TryParse (userId, out var id) && id == 0)) {
				return;
			}
			LogDiagnostic ($"Ensuring Android user {userId} is in the 'running' state before run-as queries.");
			string output = await Device.RunShellCommand (CancellationToken, "am", "start-user", "-w", userId);
			// `am start-user -w` normally prints `Success: user started`. Surface any
			// output (success or failure, e.g. `Error: could not start user`) at the
			// diagnostic level so build logs make the cause obvious if the subsequent
			// `run-as` query fails. Do not attempt to interpret the output here: the
			// existing run-as error path raises XA0137 deterministically on failure,
			// and parsing `am`'s output for error markers risks false positives.
			LogDiagnostic ($"'am start-user -w {userId}' returned: {(string.IsNullOrWhiteSpace (output) ? "<no output>" : output.Trim ())}");
		}

		protected async Task RemoveOverrideDirectory () {
			// remote //.__override__ directory has files in it.
			// We can do that by using out tool stat.
			string overrideExists = await Device.RunAs (packageInfo, $"{ToolsFullPath}/{FastDevStatTool}", OverrideFullPath);
			if (!(overrideExists.IndexOf ("error:", StringComparison.OrdinalIgnoreCase) >= 0) &&
					!(overrideExists.IndexOf ("package not debuggable", StringComparison.OrdinalIgnoreCase) >= 0)) {
				await Device.RunAs (packageInfo, "rm", "-Rf", OverrideFullPath);
			}
		}

		protected async Task TerminateApp ()
		{
			var pid = await Device.GetProcessId (PackageName, CancellationToken);
			if (pid == 0) {
				LogDebugMessage ($"{PackageName} was not running, skipping kill");
				return;
			}
			LogDebugMessage ($"Terminating {PackageName}...");
			await Device.KillProcessAndWaitForExit (PackageName, CancellationToken);
			LogDebugMessageWithTiming ($"{PackageName} Terminated.");
		}

		protected async Task InstallPackage (bool installed = true)
		{
			LogDebugMessage ($"Installing Package {PackageName}");
			try {
				await Device.PushAndInstallPackageAsync (new PushAndInstallCommand {
					 ApkFile = PackageFile,
					 PackageName = PackageName,
					 ReInstall = ReInstall,
					 User = UserID,
					 TestOnly = IsTestOnly,
				}, token: CancellationToken);
				LogDebugMessageWithTiming ($"Installed Package {PackageName}.");
			} catch (Exception exception) {
				var ex = exception;
				if (exception is AggregateException aex) {
					ex = aex.Flatten ().InnerException;
				}
				if (!await ShouldThrowIfPackageInstallFailed (ex as PackageAlreadyExistsException)) {
					LogDebugMessageWithTiming ($"Installed Package {PackageName}.");
					return;
				}
				throw;
			}
			return;
		}

		/// <summary>
		/// Confirms the package is actually present on the device via <c>pm path &lt;pkg&gt;</c>.
		/// <c>pm install</c> can report success (or empty output) yet leave the package absent,
		/// which otherwise only surfaces as an opaque <c>XA0137</c> run-as "couldn't stat" failure
		/// during the post-install probe. Returns <see langword="true"/> when a <c>package:/…</c>
		/// path is reported (or when there is no package name to query).
		/// </summary>
		async Task<bool> IsPackageInstalled (string packageName)
		{
			if (string.IsNullOrEmpty (packageName)) {
				return true;
			}
			var args = new List<string> { "pm", "path" };
			var userId = (UserID ?? string.Empty).Trim ();
			if (userId.Length > 0) {
				args.Add ("--user");
				args.Add (userId);
			}
			args.Add (packageName);
			string output = await Device.RunShellCommand (CancellationToken, args.ToArray ());
			LogDiagnostic ($"`pm path {packageName}` returned: {(string.IsNullOrWhiteSpace (output) ? "<no output>" : output.Trim ())}");
			return IsPackageInstalledOutput (output);
		}

		internal static bool IsPackageInstalledOutput (string output)
		{
			return !string.IsNullOrWhiteSpace (output) &&
				output.IndexOf ("package:", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		/// <summary>
		/// Logs the device's free space on the internal (<c>/data</c>) partition. A package that
		/// vanishes right after a "successful" install is often a symptom of a full data partition
		/// (test APKs accumulate on CI emulators), which <c>pm install</c> does not always surface
		/// as <c>INSTALL_FAILED_INSUFFICIENT_STORAGE</c>. Best-effort: never throws.
		/// </summary>
		async Task LogAvailableDiskSpace ()
		{
			try {
				var disk = await Device.GetAvailableSpace (CancellationToken);
				LogDiagnostic ($"Free space on /data: {disk.InternalSpace / (1024 * 1024)} MiB ({disk.InternalSpace} bytes).");
				diagnosticData.SetProperty ("deploy.data.free.bytes", disk.InternalSpace);
			} catch (Exception ex) {
				LogDiagnostic ($"Could not query device disk space: {ex.Message}");
			}
		}

		async Task<bool> ShouldThrowIfPackageInstallFailed (PackageAlreadyExistsException e)
		{
			if (e == null)
				return true;

			int s = (e.PackageFile ?? "").LastIndexOf ('/');
			string apkBasename  = s >= 0 ? e.PackageFile.Substring (s+1) : e.PackageFile;

			// If the runtime already exists, ignore the error
			// Sometimes android doesn't report it's installed when it is  :/
			if (apkBasename != Path.GetFileName (PackageFile))
				return false;

			// Oops; things have gotten wedged (stale/interrupted install?)
			// The file we tried to upload already exists on the device!
			// Delete and try again.
			LogDebugMessage (string.Format ("Package '{0}' already exists. Retrying...", PackageName));
			try {
				// NOTE We NEED to delete the cache data too other wise the install will fail.
				await Device.DeleteFile (e.PackageFile, true, CancellationToken);
			} catch {
				// Ebil, yes, but...
			}
			bool preserveData = !(e is RequiresUninstallException);
			LogDebugMessage (string.Format ("Forcing complete uninstall of '{0}'... Preserving Data: {1}", PackageName, preserveData));
			var uninstallCommand = new PmUninstallCommand() { PackageName = PackageName, User = UserID, PreserveData = preserveData };
			await Device.UninstallPackage (uninstallCommand, cancellationToken: CancellationToken);
			LogDebugMessage (string.Format ("Installing '{0}'...", PackageName));
			await Device.PushAndInstallPackageAsync (new PushAndInstallCommand {
					 ApkFile = PackageFile,
					 PackageName = PackageName,
					 ReInstall = false,
					 User = UserID
			},token: CancellationToken);
			return false;
		}

		protected async Task<bool> InstallFastDevTools (string toolPath)
		{
			if (string.Compare (packageInfo.ToolVersion ?? string.Empty, ToolVersion, StringComparison.OrdinalIgnoreCase) == 0) {
				LogDebugMessage ($"FastDev Tools already installed for the app. {packageInfo.ToolVersion}");
				return true;
			}

			string output = await Device.RunAs (packageInfo, "cat", $"{toolPath}/version");
			if (string.Compare (output.Trim (), ToolVersion, StringComparison.OrdinalIgnoreCase) == 0) {
				LogDebugMessage ($"FastDev Tools already installed for the app. {output}");
				packageInfo.ToolVersion = ToolVersion;
				return true;
			}

			output = await Device.RunAs (packageInfo, "mkdir", "-p", toolPath);
			if (output.IndexOf ("run-as:", StringComparison.OrdinalIgnoreCase) >= 0 ||
					output.IndexOf ("mkdir:", StringComparison.OrdinalIgnoreCase) >= 0) {
				if (!RaiseRunAsError (output)) {
					LogDiagnosticDataError ("XA0130", output);
					PrintDiagnostics ();
					LogCodedError ($"XA0130", Resources.XA0130_FastDevNotSupported);
				}
				return false;
			}
			// we have to do this as a normal shell command since running
			// mkdir under `run-as` will result in a `permission-denied` error.
			output = await Device.RunShellCommand ("mkdir", "-p", XAToolsTempPath);
			if (output.IndexOf ("mkdir:", StringComparison.OrdinalIgnoreCase) >= 0) {
				if (!RaiseRunAsError (output)) {
					LogDiagnosticDataError ("XA0130", output);
					PrintDiagnostics ();
					LogCodedError ($"XA0130", Resources.XA0130_FastDevNotSupported);
				}
				return false;
			}

			string toolAbi = string.IsNullOrEmpty (ToolsAbi) ? PrimaryCpuAbi : ToolsAbi;
			var tools = new [] { FastDevFindTool, FastDevTool, FastDevStatTool, FastDevCpTool };
			foreach (var tool in tools) {
				LogDebugMessage ($"Installing FastDev Tool {toolPath}/{tool} for {toolAbi}");
				if (!await PushFileToDevice (Device, PackageName, toolPath, Path.Combine (FastDevToolPath, toolAbi, tool), $"{toolPath}/{tool}", CancellationToken)) {
					LogDiagnosticDataError ("XA0126", Resources.XA0126_UnableToCopyFastDevTools);
					PrintDiagnostics ();
					LogCodedError ($"XA0126", Resources.XA0126_UnableToCopyFastDevTools, toolPath, tool);
					return false;
				}
			}
			LogDebugMessage ($"Setting FastDev Tools Permissions");
			await Device.RunAs (packageInfo, "chmod", "700", $"{toolPath}/{FastDevTool}", $"{toolPath}/{FastDevFindTool}", $"{toolPath}/{FastDevStatTool}", $"{toolPath}/{FastDevCpTool}");
			LogDebugMessage ($"Installing FastDev Tools to {toolPath}/version");
			await PushFileTextToDevice (Device, PackageName, ToolVersion, Encoding.ASCII, $"{toolPath}/version", token: CancellationToken);
			LogDebugMessage ($"Removing FastDev Tools temp directory.");
			await Device.RunShellCommand ("rm", "-Rf", XAToolsTempPath);
			packageInfo.ToolVersion = ToolVersion;
			return true;
		}

		async Task<bool> PushFileToDevice (AndroidDevice device, string packageName, string toolPath, string file, string target, CancellationToken token)
		{
			if (!File.Exists (file)) {
				LogDebugMessage ($"File '{file}' does not exists. Skipping.");
				return false;
			}
			using (var fs = File.OpenRead (file)) {
				if (!await PushStreamToDevice (device, packageName, toolPath, fs, target, DateTime.UtcNow, token: token)) {
					return false;
				}
			}
			return true;
		}

		async Task<bool> PushFileTextToDevice (AndroidDevice device, string packageName, string fileContents, Encoding encoding, string target, CancellationToken token)
		{
			using (var ms = new MemoryStream ()) {
				using (var sw1 = new StreamWriter (ms, encoding, 1024, leaveOpen: true)) {
					sw1.WriteLine (fileContents);
					sw1.Flush ();
				}
				ms.Position = 0;
				if (!await PushStreamToDevice (device, packageName, null, ms, target, DateTime.UtcNow, token: token)) {
					return false;
				}
			}
			return true;
		}

		async Task<bool> PushStreamToDeviceWithTool (AndroidDevice device, string packageName, string toolPath, Stream stream, string target, DateTimeOffset modifiedDateTime, CancellationToken token = default (CancellationToken))
		{
			string targetFile = Path.GetFileName (target);
			try {
				long wrote = await device.Push (stream, $"{XAToolsTempPath}/{targetFile}", cancellationToken: token);
				LogDiagnostic ($"Pushed {wrote} to {XAToolsTempPath}/{targetFile}");
				string r = await device.RunAs (packageInfo, $"{toolPath}/{FastDevCpTool}", $"{XAToolsTempPath}/{targetFile}", target, $"{modifiedDateTime.ToUnixTimeMilliseconds ()}");
				if (r.IndexOf ("run-as:", StringComparison.OrdinalIgnoreCase) >= 0) {
					TryGetRunAsErrorCode (r, out var err);
					LogDiagnosticDataError (err.code, r, targetFile);
					return false;
				}
				LogDiagnostic ($"moved {XAToolsTempPath}/{targetFile} to {target}");
				LogDebugMessageWithTiming ($"Installed {target}.");
			} catch (Exception ex) {
				LogDebugMessageWithTiming ($"Failed to push {targetFile} to {target}. {ex}.");
				LogDiagnosticDataError(GetErrorCode (ex),ex.ToString (), targetFile);
				return false;
			}
			return true;
		}

		async Task<bool> PushStreamToDevice (AndroidDevice device, string packageName, string toolPath, Stream stream, string target, DateTimeOffset modifiedDateTime, CancellationToken token = default (CancellationToken))
		{
			string targetFile = Path.GetFileName (target);
			try {
				long wrote = await device.Push (stream, $"{XAToolsTempPath}/{targetFile}", cancellationToken: token);
				LogDiagnostic ($"Pushed {wrote} to {XAToolsTempPath}/{targetFile}");
				string r = await device.RunAs (packageInfo, "cp", $"{XAToolsTempPath}/{targetFile}", target);
				if (r.IndexOf ("run-as:", StringComparison.OrdinalIgnoreCase) >= 0) {
					TryGetRunAsErrorCode (r, out var err);
					LogDiagnosticDataError (err.code, r, targetFile);
					return false;
				}
				LogDiagnostic ($"moved {XAToolsTempPath}/{targetFile} to {target}");
				await device.RunAs (packageInfo, "touch", "-t", $"{modifiedDateTime.ToString ("yyyyMMdd.HHmmss")}", target);
				LogDebugMessageWithTiming ($"Installed {target}.");
			} catch (Exception ex) {
				LogDiagnosticDataError (GetErrorCode (ex),ex.ToString ());
				LogDebugMessageWithTiming ($"Failed to push {targetFile} to {target}. {ex}.");
				return false;
			}
			return true;
		}

		string GetTargetPath (ITaskItem file)
		{
			string targetPath = file.GetMetadata ("TargetPath");
			if (string.IsNullOrEmpty (targetPath)) {
				// fallback to DestinationSubPath
				LogDiagnostic ($"'TargetPath' meta data not found on '{file.ItemSpec}'. Falling back to'DestinationSubPath'");
				targetPath = file.GetMetadata ("DestinationSubPath");
			}
			return targetPath;
		}

		protected async Task DeployFastDevFiles (string toolPath, string overridePath)
		{
			// get the optimal blocksize from the device. This will help speed up transfer and disk writes.
			LZ4Level lz4level = LZ4Level.L03_HC;

			LogDiagnostic ("Calculating subdirectories");
			HashSet<string> directories = new HashSet<string> ();
			directories.Add (overridePath);
			foreach (var file in FastDevFiles) {
				string targetPath = GetTargetPath (file);
				if (!string.IsNullOrEmpty (targetPath)) {
					string dirName = Path.GetDirectoryName (targetPath).Replace ("\\", "/");
					if (!string.IsNullOrEmpty (dirName)) {
						directories.Add ($"{overridePath}/{dirName}");
						LogDiagnostic ($"{targetPath} => {overridePath}/{dirName}");
					}
				}
			}
			int length = ADB_COMMAND_PADDING + PackageName.Length;
			List<string> args = new List<string>(directories.Count + 2);
			args.Add ("mkdir");
			args.Add ("-p");
			foreach (var dir in directories) {
				int newLength = dir.Length + 3;
				if ((length + newLength) >= MAX_COMMAND) {
					await Device.RunAs (packageInfo, args);
					length = ADB_COMMAND_PADDING + PackageName.Length;
					args.Clear ();
					args.Add ("mkdir");
					args.Add ("-p");
				}
				length += newLength;
				args.Add (dir);
			}
			await Device.RunAs (packageInfo, args);

			string filelist = await Device.RunAs (packageInfo, $"{toolPath}/{FastDevFindTool}", DiagnosticLogging ? "-vd" : "-v", overridePath);
			LogDiagnostic ($"{FastDevFindTool}: {filelist}");
			string [] files = Array.Empty<string> ();
			if (!(filelist.IndexOf ("error:", StringComparison.OrdinalIgnoreCase) >= 0)) {
				files = filelist.Split (new char [] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
			}
			Dictionary<string, (long size, DateTimeOffset mtime)> fileData = new Dictionary<string, (long, DateTimeOffset)> ();
			foreach (var file in files) {   // file size mtime
				if (file.IndexOf ("\t") == -1) {
					LogDebugMessage ($"{FastDevFindTool}: Ignoring line '{file}'. Line is incorrectly formatted.");
					continue;
				}
				var entires = file.Split (new char [] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
				if (entires.Length != 3) {
					LogDebugMessage ($"{FastDevFindTool}: Ignoring line {file}. Input does not have 3 items.");
					continue;
				}
				if (long.TryParse (entires [1].Trim (), out long fsize) && long.TryParse (entires [2].Trim (), out long mtime)) {
					DateTimeOffset offset;
					try {
						offset = DateTimeOffset.FromUnixTimeMilliseconds (mtime);
					} catch (ArgumentOutOfRangeException)  {
						offset = DateTimeOffset.MinValue;
					}
					fileData.Add (entires [0].Replace ("./", "").Trim (), (size: fsize, mtime: offset));
				} else {
					LogDebugMessage ($"Failed to parse values for line {file}. Ignoring.");
				}
			}
			// remove known directories s they don't get deleted.
			fileData.Remove ("links");

			foreach (var file in FastDevFiles) {
				if (!File.Exists (file.ItemSpec)) {
					LogDebugMessage ($"File '{file.ItemSpec}' does not exists. Skipping.");
					continue;
				}
				StartTiming ();
				if (Path.GetExtension (file.ItemSpec) == ".so") {
					string abi = AndroidRidAbiHelper.GetNativeLibraryAbi (file);
					if (abi != PrimaryCpuAbi) {
						LogDebugMessageWithTiming ($"NotifySync SkipCopyFile {file.ItemSpec} abi not suitable for this device.");
						continue;
					}
				}
				string targetPath = GetTargetPath (file);
				if (!string.IsNullOrEmpty (targetPath)) {
					targetPath = $"{targetPath}".Replace ("\\", "/");
				} else {
					targetPath = $"{Path.GetFileName (file.ItemSpec)}";
				}
				string filename = Path.GetFileName (file.ItemSpec);
				var fi = new FileInfo (file.ItemSpec);
				bool modified = true;
				DateTimeOffset modifiedDateTime = File.GetLastWriteTimeUtc (file.ItemSpec);
				DateTimeOffset remoteDateTime = DateTimeOffset.MinValue;
				if (fileData.ContainsKey (targetPath)) {
					remoteDateTime = fileData [targetPath].mtime;
					modified = remoteDateTime.ToUnixTimeMilliseconds () < modifiedDateTime.ToUnixTimeMilliseconds () || fi.Length != fileData [targetPath].size;
				}
				if (!modified) {
					LogDebugMessageWithTiming ($"NotifySync SkipCopyFile {file.ItemSpec}=>{targetPath} file is up to date.");
					fileData.Remove (targetPath);
					continue;
				}
				if (!await DeployFileWithFastDevTool (file, toolPath, overridePath, lz4level, modifiedDateTime)) {
					diagnosticData.SetProperty ("deploy.result", "Failed");
					return;
				}
				LogDebugMessageWithTiming ($"NotifySync CopyFile {file.ItemSpec}.");
				LogDiagnostic ($"Local Modified Time '{modifiedDateTime.ToUnixTimeMilliseconds ()}' is newer than '{remoteDateTime.ToUnixTimeMilliseconds ()}'.");
				fileData.Remove (targetPath);
			}
			if (EnvironmentFiles?.Length > 0) {
				string targetPath = $"{PrimaryCpuAbi}/environment";
				DateTimeOffset remoteDateTime = DateTimeOffset.MinValue;
				if (fileData.ContainsKey (targetPath)) {
					remoteDateTime = fileData [targetPath].mtime;
				}
				await DeployEnvironmentFiles (EnvironmentFiles, toolPath, overridePath, targetPath, remoteDateTime);
				fileData.Remove (targetPath);
			}
			foreach (var file in fileData.Keys) {
				// we need to remove unknown files from the .__override__ path
				string targetFile = $"{file.Replace ("./", "")}";
				LogDebugMessage ($"Remove redundant file {OverrideFullPath}/{targetFile}");
				await Device.RunAs (packageInfo, "rm", "-Rf", $"{OverrideFullPath}/{targetFile}");
			}
			// clean up the temp folder if we are not using the xamarin.sync tool
			if (!packageInfo.SupportsFastDev)
				await Device.RunShellCommand ("rm", "-Rf", XAToolsTempPath);
			return;
		}

		async Task<bool> DeployFileWithFastDevTool (ITaskItem file, string toolPath, string overridePath, LZ4Level lz4level, DateTimeOffset modifiedDateTime)
		{

			using (var fs = File.OpenRead (file.ItemSpec)) {
				string destination = overridePath;
				// This bit handles subdirectories.
				int bufferSize = LZ4Codec.MaximumOutputSize (fs.Length > int.MaxValue ? int.MaxValue : (int)fs.Length);
				string targetPath = GetTargetPath (file);
				if (!string.IsNullOrEmpty (targetPath)) {
					destination += $"/{targetPath}".Replace ("\\", "/");
				} else {
					destination += $"/{Path.GetFileName (file.ItemSpec)}";
				}
				if (packageInfo.SupportsFastDev) {
					byte [] buffer = ArrayPool<byte>.Shared.Rent (bufferSize);
					byte [] compressed = ArrayPool<byte>.Shared.Rent (bufferSize);
					try {
						List<string> args = DeviceExt.BuildArgs (DeviceExt.RunAsCommand, packageInfo);
						args.AddRange (new string[] { $"{toolPath}/{FastDevTool}", $"{compressed.Length}", $"{fs.Length}", $"{destination}", $"{modifiedDateTime.ToUnixTimeMilliseconds ()}" });
						LogDiagnostic ($"executing: {string.Join (" ", args.ToArray ())}");
						var output = await Device.RunShellCommandStream (args.ToArray (), async (s) => {
							int read = await fs.ReadAsync (buffer, 0, buffer.Length);
							if (read == 0)
								return false;
							int compressedLength = CompressLZ4 (ref buffer, read, ref compressed, lz4level);
							int l = IPAddress.HostToNetworkOrder (compressedLength);
							var v = BitConverter.GetBytes (l);
							try {
								s.Write (v, 0, 4);
								s.Write (compressed, 0, compressedLength);
							} catch {
								return false;
							}
							return true;
						}, CancellationToken);
						LogDiagnostic ($"FastDev of {file.ItemSpec} returned: {output}");

						if (output.IndexOf ("error:", StringComparison.OrdinalIgnoreCase) >= 0) {
							if (output.IndexOf ("from stdin.", StringComparison.OrdinalIgnoreCase) >= 0) {
								LogDiagnostic ($"'{FastDevTool}' returned '{output}' when deploying '{destination}'. Falling back to backup deployment.");
								diagnosticData.SetProperty ("pii.deploy.error", output);
								diagnosticData.SetProperty ("pii.deploy.file", file.ItemSpec);
								diagnosticData.SetProperty ("deploy.tool", value:"xamarin.cp");
								// Log warning and fallback to adb push style deployment. It will be slower... but it works.
							} else {
								LogDiagnosticDataError ("XA0127", output, file.ItemSpec);
								PrintDiagnostics ();
								LogCodedError ($"XA0127", Resources.XA0127_ErrorDeployingFile, destination, FastDevTool, output);
								return false;
							}
						}

						if (output.IndexOf ($"wrote [{fs.Length}]", StringComparison.OrdinalIgnoreCase) >= 0) {
							return true;
						}
						// we didn't write the file as we expected so use the backup path.
						// this can happen is the devices supports run-as but does not support
						// reading data in from stdin. Normally on older devices.
						// if we get here, we will just reset the stream and drop through to the
						// backup path.
						packageInfo.SupportsFastDev = false;
						fs.Position = 0;
					} catch (Exception ex) {
						LogDiagnostic ($"Hit exception. Falling back to slow deployment for {file.ItemSpec}. {ex}");
						diagnosticData.SetProperty ("pii.deploy.error", ex.ToString ());
						diagnosticData.SetProperty ("deploy.tool", value:"xamarin.cp");
						packageInfo.SupportsFastDev = false;
						fs.Position = 0;
					} finally {
						ArrayPool<byte>.Shared.Return (buffer);
						ArrayPool<byte>.Shared.Return (compressed);
					}
				}
				if (!packageInfo.SupportsFastDev) {
					if (!await PushStreamToDeviceWithTool (Device, PackageName, toolPath, fs, destination, modifiedDateTime, token: CancellationToken)) {
						LogDiagnosticDataError ("XA0129", Resources.XA0129_ErrorDeployingFile, destination);
						PrintDiagnostics ();
						LogCodedError ($"XA0129", Resources.XA0129_ErrorDeployingFile, destination);
						return false;
					}
				}
			}
			return true;
		}

		async Task<bool> DeployEnvironmentFiles (ITaskItem[] environments, string toolPath, string overridePath, string targetPath, DateTimeOffset remoteFileModified)
		{
			int maxKeyLength = 0;
			int maxValueLength = 0;
			DateTimeOffset newestFileDateTime = DateTimeOffset.MinValue;
			var data = new Dictionary<string, string> ();
			foreach (ITaskItem env in environments ?? Array.Empty<ITaskItem> ()) {
				if (!File.Exists (env.ItemSpec))
					continue;
				DateTimeOffset modifiedDateTime = File.GetLastWriteTimeUtc (env.ItemSpec);
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

			// Length+1 so at least one trailing \0 for the longest value
			maxKeyLength++;
			maxValueLength++;

			if (newestFileDateTime <= remoteFileModified) {
				LogDebugMessage ($"NotifySync SkipCopyFile @(AndroidEnvironment) files => {targetPath} file is up to date.");
				return true;
			}
			var stream = new MemoryStream (); // dont use Pool as Device.Push dispose's the stream.
			var binaryWriter = new BinaryWriter (stream, Encoding.ASCII);
			binaryWriter.Write (Encoding.ASCII.GetBytes ("0x" + maxKeyLength.ToString ("X8") + '\0'));
			binaryWriter.Write (Encoding.ASCII.GetBytes ("0x" + maxValueLength.ToString ("X8") + '\0'));
			foreach (var kvp in data) {
				binaryWriter.Write (Encoding.ASCII.GetBytes (kvp.Key.PadRight (maxKeyLength, '\0')));
				binaryWriter.Write (Encoding.ASCII.GetBytes (kvp.Value.PadRight (maxValueLength, '\0')));
			}
			binaryWriter.Flush ();
			binaryWriter.BaseStream.Position = 0;
			await PushStreamToDeviceWithTool (Device, PackageName, toolPath, binaryWriter.BaseStream, $"{overridePath}/{targetPath}", DateTimeOffset.UtcNow, token: CancellationToken);
			LogDebugMessageWithTiming ($"NotifySync CopyFile @(AndroidEnvironment) files.");
			LogDiagnostic ($"Local Modified Time '{newestFileDateTime.ToUnixTimeMilliseconds ()}' is newer than '{remoteFileModified.ToUnixTimeMilliseconds ()}'.");
			return true;
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
				//NOTE: this one is a base class
				case InstallFailedException e:
					return "ADB0010";
				default:
					return GetErrorCode (ex.Message);
			}
		}

		static readonly List<(string error, string code, string message)> runas_codes = new List<(string error, string code, string message)> () {
			{ (error: "run-as is disabled",             code: "XA0131", message: Resources.XA0131_DeveloperModeNotEnabled ) },
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

		bool RaiseRunAsError (string error)
		{
			if (TryGetRunAsErrorCode (error, out var err)) {
				LogDiagnosticDataError (err.code, err.message);
				PrintDiagnostics ();
				LogCodedError (err.code, err.message, error);
				return true;
			}
			return false;
		}

		string GetFullPath (string dir) => Path.IsPathRooted (dir) ? dir : Path.GetFullPath (Path.Combine (WorkingDirectory, dir));

		static string GetErrorCode (string message)
		{
			foreach (var errorCode in error_codes)
				if (message.IndexOf (errorCode.message, StringComparison.OrdinalIgnoreCase) >= 0)
					return errorCode.code;

			return "ADB1000";
		}

		static readonly List<(string code, string message)> error_codes = new List<(string code , string message)> () {
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
	}

	internal static class DeviceExt
	{
		internal static string RunAsCommand = "run-as";

		internal static List<string> BuildArgs(string command, FastDeploy.PackageInfo packageInfo)
		{
			List<string> args = new List<string> ();
			if (packageInfo.IsSystemApplication) {
				if (!packageInfo.AdbIsRoot) {
					args.Add ("su");
					args.Add (packageInfo.UserId ?? "0");
				}
				return args;
			}
			args.Add (RunAsCommand);
			args.Add (packageInfo.PackageName);
			if (!string.IsNullOrEmpty (packageInfo.UserId)) {
				args.Add("--user");
				args.Add(packageInfo.UserId);
			}
			return args;
		}

		internal static async Task<string> RunAs (this AndroidDevice Device, FastDeploy.PackageInfo packageInfo, IEnumerable<string> arguments)
		{
			string [] args = arguments.ToArray ();
			string result = await Device.RunAs (packageInfo, args);
			packageInfo.LogDebugMessage ($"{args[0]} returned: {result}");
			return result;
		}

		internal static async Task<string> RunAs (this AndroidDevice Device, FastDeploy.PackageInfo packageInfo, params string [] arguments)
		{
			List<string> args = BuildArgs(RunAsCommand, packageInfo);
			args.AddRange (arguments);
			string result = await Device.RunShellCommand (args.ToArray ());
			packageInfo.LogDebugMessage ($"{arguments[0]} returned: {result}");
			return result;
		}
	}
}
