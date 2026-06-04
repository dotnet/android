//
// AndroidDeploySession.cs
//
// Authors:
//       Jonathan Pobst <jpobst@xamarin.com>
//       Michael Hutchinson <mhutch@xamarin.com>
//
// Copyright 2012-2013 Xamarin Inc. All rights reserved.
//

using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using Mono.AndroidTools;
using Mono.AndroidTools.Adb;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Xamarin.AndroidTools
{
	public class AndroidDeploySession
	{
		[Obsolete ("Use PackageApiLevel")]
		public string PackageTargetApi {
			get { return PackageApiLevel.ToString (); }
			set { PackageApiLevel = int.Parse (value); }
		}

		public int PackageApiLevel { get; set; }
		public AndroidDevice Device { get; private set; }
		public string PackageSupportedArchs { get; set; }
		public IProgressNotifier ProgressReporter { get; set; }

		/// <summary>Whether the user wants unstripped binaries and BCL debug symbols</summary>
		public bool ProvideFullDebugRuntime { get; set; }

		public bool UsesSharedRuntime { get; set; }
		public bool PreserveUserData { get; set; }

		public bool ForcePackageInstall { get; set; }
		public bool IsFastDev { get; set; }
		public string PackageName { get; set; }
		public string PackageFile { get; set; }

		public string AaptToolPath { get; set; }

		[Obsolete ("DeltaInstall support has been removed.", error: true)]
		public bool AllowDeltaInstall {
			get { throw new NotSupportedException ("DeltaInstall support has been removed."); }
			set { throw new NotSupportedException ("DeltaInstall support has been removed."); }
		}

		[Obsolete ("Activity execution at installation is no longer needed")]
		public string Activity { get; set; }

		[Obsolete ("Use FastDevAssembliesProvider")]
		public List<string> Assemblies { get; set; }

		/// <summary>
		/// Gets the name of the activity launched to create the FastDev assembly directory.
		/// </summary>
		[Obsolete ("Activity execution at installation is no longer needed")]
		public Func<string> FastDevActivityProvider { get; set; }

		/// <summary>
		/// Gets the collection of assemblies to be deployed by FastDev.
		/// </summary>
		public Func<IEnumerable<string>> FastDevAssembliesProvider { get; set; }

		/// <summary>
		/// Gets the collection of dexes to be deployed by FastDev.
		/// </summary>
		public Func<IEnumerable<string>> FastDevNativeLibrariesProvider { get; set; }

		/// <summary>
		/// Gets the collection of dexes to be deployed by FastDev.
		/// </summary>
		public Func<IEnumerable<string>> FastDevDexesProvider { get; set; }

		/// <summary>
		/// Gets the collection of packaged_resources to be deployed by FastDev.
		/// </summary>
		public Func<IEnumerable<string>> FastDevPackagedResourcesProvider { get; set; }

		/// <summary>
		/// Gets the collection of resources to be deployed by FastDev.
		/// </summary>
		public Func<IEnumerable<string>> FastDevResourcesProvider { get; set; }

		/// <summary>
		/// Gets the collection of typemap files to be deployed by FastDev.
		/// </summary>
		public Func<IEnumerable<string>> FastDevTypemapsProvider { get; set; }

		/// <summary>
		/// If non-null, will block on this before deploying actual assemblies. Returns true if the package changed.
		/// </summary>
		public Task<bool> PackagingTask { get; set; }

		public bool External { get; private set; }
		public string PackageRemotePath { get; private set; }

		private CancellationToken token;
		private IList<AndroidInstalledPackage> packages;
		private string fastDevRemotePath;

		public AndroidDeploySession (AndroidDevice device, IList<AndroidInstalledPackage> packages = null)
		{
			Device = device;
			this.packages = packages;

			PreserveUserData = true;
		}

		public async Task StartAsync (CancellationToken token)
		{
			await RunLoggedAsync (token).ConfigureAwait(false);
		}

		async Task RunLoggedAsync (CancellationToken token)
		{
			try {
				await RunAsync (token).ConfigureAwait(false);
			} catch (Exception ex) {
				// make sure we observe these exceptions otherwise they get logged as unhandled
				// so do this before the cancellation check below
				var aex = ex as AggregateException;
				if (aex != null) {
					ex = aex.Flatten ().InnerException;
				}

				if (token.IsCancellationRequested) {
					AndroidLogger.LogInfo ("Deployment cancelled");
					token.ThrowIfCancellationRequested ();
				}

				if (ex is OperationCanceledException)
					throw ex;

				AndroidLogger.LogError ("Deployment failed", ex);
				if (ex is AndroidDeploymentException) {
					throw;
				}

				if (ex is DeviceNotFoundException || ex is DeviceDisconnectedException) {
					throw new AndroidDeploymentException (AndroidDeploymentFailureReason.DeviceDisconnected, ex);
				}

				throw new AndroidDeploymentException (AndroidDeploymentFailureReason.InternalError, ex);
			}
		}

		[Obsolete ("Use StartAsync")]
		public bool Start (CancellationToken token)
		{
			try {
				RunLoggedAsync (token).Wait ();
				return true;
			} catch (OperationCanceledException) {
				// ignore
			} catch (AndroidDeploymentException ex) {
				string title, detail;
				ex.GetNiceExplanation (out title, out detail);
				ProgressReporter.ShowErrorDialog (title, detail, ex);
			}
			return false;
		}

		async Task RunAsync (CancellationToken token)
		{
			this.token = token;

			await Device.EnsureProperties (token).ConfigureAwait(false);

			// Make sure the device arch is supported
			if (!string.IsNullOrWhiteSpace (PackageSupportedArchs) && !Device.CanRunPackageArchitecture (PackageSupportedArchs)) {
				throw new AndroidDeploymentException (AndroidDeploymentFailureReason.ArchitectureNotSupported, this);
			}

			if (UsesSharedRuntime && !Device.CanRunPackageArchitecture (MonoDroidSdk.SharedRuntimeAbis)) {
				throw new AndroidDeploymentException (AndroidDeploymentFailureReason.ArchitectureNotSupportedBySharedRuntime, this);
			}

			string redirectStdio = Device.Properties.Get ("log.redirect-stdio");
			if (redirectStdio != null && string.Equals ("true", redirectStdio.Trim (), StringComparison.OrdinalIgnoreCase)) {
				throw new AndroidDeploymentException (AndroidDeploymentFailureReason.StdioRedirectionEnabled);
			}

			if (packages == null) {
				packages = await Device.GetPackagesAsync (PackageApiLevel, PackageName, ProvideFullDebugRuntime, token, ProgressReporter);
			}

			if (packages == null) {
				throw new AndroidDeploymentException (AndroidDeploymentFailureReason.FailedToGetPackageList);
			}

			if (UsesSharedRuntime) {
				await EnsureCorrectSharedRuntimes ();
			}

			if (!UsesSharedRuntime && IsFastDev) {
				if (string.IsNullOrWhiteSpace (Device.Properties.MonoLog)) {
					await Device.SetProperty ("debug.mono.log", "gc");
				}
			}

			//packaging can be done in parallel with installing the shared runtime, but need to block on it
			//before installing the app package
			if (PackagingTask != null && PackagingTask.Status != TaskStatus.RanToCompletion) {
				ProgressReporter.BeginStep ("Waiting for packaging to complete");
				try {
					await PackagingTask;
				} catch (Exception ex) {
					if (ex is AndroidDeploymentException || ex is OperationCanceledException)
						throw;
					throw new AndroidDeploymentException (AndroidDeploymentFailureReason.PackagingFailed, ex);
				}
				ProgressReporter.EndStep (null);
			}

			await InstallPackage ();

			if (IsFastDev) {
				try {
					await FastDevAsync (true);
				} catch {
					ShowProgressText ("Fast dev didn't succeed, trying another location...", token);

					await FastDevAsync (false);
				}
			} else {
				var fastDevPath = await GetFastDevRemotePathAsync (true);
				if (!string.IsNullOrEmpty (fastDevPath)) {
					ShowProgressText ("Fast dev is disabled, removing fast dev directory: " + fastDevPath, token);
					await Device.DeleteDirectory (fastDevPath, true, token);
				}
				fastDevPath = await GetFastDevRemotePathAsync (false);
				if (!string.IsNullOrEmpty (fastDevPath)) {
					ShowProgressText ("Fast dev is disabled, removing fast dev directory: " + fastDevPath, token);
					await Device.DeleteDirectory (fastDevPath, true, token);
				}
			}
		}

		async Task EnsureCorrectSharedRuntimes ()
		{
			// See if there are any old runtimes we need to remove
			await RemoveOldRuntimes ();

			// Install the shared runtime and platform

			try {
				await CheckAndInstallSharedRuntimeAsync ();
			} catch (Exception ex) {
				if (ex is InsufficientSpaceException) {
					throw new AndroidDeploymentException (AndroidDeploymentFailureReason.InsufficientSpaceForRuntime, ex);
				}
				throw;
			}

			try {
				await InstallSharedPlatformAsync ();
			} catch (Exception ex) {
				if (ex is InsufficientSpaceException) {
					throw new AndroidDeploymentException (AndroidDeploymentFailureReason.InsufficientSpaceForPlatform, ex);
				}
				if (ex is SdkNotSupportedException) {
					throw new AndroidDeploymentException (AndroidDeploymentFailureReason.SdkNotSupportedByDevice, ex);
				}
				throw;
			}
		}

		async Task RemoveOldRuntimes ()
		{
			// See if we were asked to cancel
			if (token.IsCancellationRequested)
				return;

			// See if there are any old runtimes we need to remove
			var old_runtimes = packages.GetOldRuntimesAndPlatforms (PackageApiLevel).ToList ();
			if (old_runtimes.Count == 0) {
				return;
			}

			ProgressReporter.BeginStep ("Removing old runtimes");
			foreach (var runtime in old_runtimes) {
				ShowProgressText (string.Format ("Removing old runtime: {0} [{1}]..", runtime.Name, runtime.Version), token);
				await Device.UninstallPackage (runtime.Name, false, token);
				packages.Remove (runtime);
			}
			ProgressReporter.EndStep (null);
		}

		// Returns whether a new shared runtime was installed
		private async Task<bool> CheckAndInstallSharedRuntimeAsync ()
		{
			// See if we were asked to cancel
			if (token.IsCancellationRequested)
				return false;

			// See if the device already has the shared runtime
			var needs_runtime = !packages.IsCurrentRuntimeInstalled ();

			// See if there is a runtime on the device that
			// we cannot determine the version of
			if (needs_runtime && packages.IsUnknownRuntimeInstalled ()) {
				ProgressReporter.ShowErrorDialog (
					"Unknown Runtime",
					"There is a shared runtime on the device whose version cannot be determined. " +
					"A new runtime will not be deployed. If the runtime needs to be replaced, please manually " +
					"remove it from the device.");
				needs_runtime = false;
			}

			// If needed, install the shared runtime
			if (!needs_runtime)
				return false;

			ProgressReporter.BeginStep ("Installing shared runtime");
			await Device.InstallSharedRuntimeAsync (ProvideFullDebugRuntime, token, ProgressReporter);
			ProgressReporter.EndStep (null);
			packages.Add (new AndroidInstalledPackage (AndroidPackageListExtensions.runtimeName, null, MonoDroidSdk.SharedRuntimeVersion));

			return true;
		}

		// Returns whether a new shared platform was installed
		private async Task<bool> InstallSharedPlatformAsync ()
		{
			// See if we were asked to cancel
			if (token.IsCancellationRequested)
				return false;

			// See if the device already has the shared platform
			var needs_platform = !packages.IsCurrentPlatformInstalled (PackageApiLevel);

			// See if there is a runtime on the device that
			// we cannot determine the version of
			if (needs_platform && packages.IsUnknownPlatformInstalled (PackageApiLevel)) {
				ProgressReporter.ShowErrorDialog (
					"Unknown Platform Runtime",
					"There is a platform support runtime on the device whose version cannot be determined. " +
					"A new platform support runtime will not be deployed. If the platform support runtime needs " +
					"to be replaced, please manually remove it from the device.");
				needs_platform = false;
			}

			var platform_file = await PlatformPackage.GetPlatformPackagePathAsync (PackageApiLevel, AaptToolPath, ProgressReporter, token);

			var sharedRuntimePackage = string.Format (AndroidPackageListExtensions.platformName, PackageApiLevel);
			var useXamarinPackage = sharedRuntimePackage;
			var version = PlatformPackage.GetPlatformPackageVersion (PackageApiLevel, ref useXamarinPackage);

			// If needed, install the shared platform
			if (needs_platform) {
				ProgressReporter.BeginStep ("Installing platform framework");
				ShowProgressText (string.Format ("Installing the API {0} platform framework..", PackageApiLevel), token);
				await Device.InstallSharedPlatformAsync (platform_file, PackageApiLevel, ProgressReporter.ReportProgress, token);
				ProgressReporter.EndStep (null);
				packages.Add (new AndroidInstalledPackage (sharedRuntimePackage, null, version));
			}

			if (useXamarinPackage.StartsWith ("Xamarin", StringComparison.OrdinalIgnoreCase)) {
				ShowProgressText (string.Format ("Removing old {0} framework.", sharedRuntimePackage), token);
				await Device.UninstallPackage (sharedRuntimePackage, false, token);
			}

			return needs_platform;
		}

		private void ShowProgressText (string text, CancellationToken token)
		{
			if (!token.IsCancellationRequested && ProgressReporter != null)
				ProgressReporter.ReportMessage (text);
		}

		async Task InstallPackage ()
		{
			bool force = ForcePackageInstall || (PackagingTask != null && PackagingTask.Result);

			if (!force && packages.ContainsPackage (PackageName)) {
				// If we didn't uninstall/reinstall, the app might already be running,
				// which makes our attempts to start a new one to debug fail, so we
				// are going to kill the already running copy.
				AndroidLogger.LogDebug ("InstallPackage", "Checking whether app {0} is running", PackageName);
				var pid = await Device.GetProcessId (PackageName, token);
				if (pid == 0) {
					AndroidLogger.LogDebug ("InstallPackage", "App was not running, skipping kill");
					return;
				}
				AndroidLogger.LogDebug ("InstallPackage", "Killing app");

				ProgressReporter.BeginStep ("Terminating running application");
				ShowProgressText ("Terminating running application...", token);
				await Device.KillProcessAndWaitForExit (PackageName, token);
				ProgressReporter.EndStep (null);
				return;
			}

			token.ThrowIfCancellationRequested ();

			if (packages.ContainsPackage (PackageName)) {
				ProgressReporter.BeginStep ("Removing previous version of application");
				ShowProgressText ("Removing previous version of application...", token);

				await Device.UninstallPackage (PackageName, PreserveUserData, token);
				ProgressReporter.EndStep (null);
			}

			token.ThrowIfCancellationRequested ();

			ProgressReporter.BeginStep ("Installing application on device");
			ShowProgressText ("Copying application to device...", token);

			try {
				//TODO: check the package ABI by poking inside the apk, if PackageSupportedArchs was not set
				await Device.PushAndInstallPackageAsync (new PushAndInstallCommand {
					ApkFile = PackageFile,
					PackageName = PackageName,
					ReInstall = false,
					NotifyProgress = ProgressReporter.ReportProgress,
				}, token);
			} catch (Exception exception) {
				var ex = exception;
				if (exception is AggregateException aex) {
					ex = aex.Flatten ().InnerException;
				}
				if (ex is InsufficientSpaceException) {
					throw new AndroidDeploymentException (AndroidDeploymentFailureReason.InsufficientSpaceForPackage, ex);
				}
				if (ex is SdkNotSupportedException) {
					throw new AndroidDeploymentException (AndroidDeploymentFailureReason.SdkNotSupportedByDevice, ex);
				}
				if (ex is IncompatibleCpuAbiException) {
					throw new AndroidDeploymentException (AndroidDeploymentFailureReason.ArchitectureNotSupported, this, ex);
				}
				if (!ShouldThrowIfPackageInstallFailed (ex as PackageAlreadyExistsException, token)) {
					ProgressReporter.EndStep (null);
					return;
				}
				throw;
			}
			packages.Add (new AndroidInstalledPackage (PackageName, null));
			ProgressReporter.EndStep (null);
		}

		bool ShouldThrowIfPackageInstallFailed (PackageAlreadyExistsException e, CancellationToken token)
		{
			if (e == null)
				return true;

			int    s            = (e.PackageFile ?? "").LastIndexOf ('/');
			string apkBasename  = s >= 0 ? e.PackageFile.Substring (s+1) : e.PackageFile;

			// If the runtime already exists, ignore the error
			// Sometimes android doesn't report it's installed when it is  :/
			if (apkBasename != Path.GetFileName (PackageFile))
				return false;

			// Oops; things have gotten wedged (stale/interrupted install?)
			// The file we tried to upload already exists on the device!
			// Delete and try again.
			ShowProgressText (string.Format ("Package '{0}' already exists. Retrying...", PackageName), token);
			try {
				// NOTE We NEED to delete the cache data too other wise the install will fail.
				Device.DeleteFile (e.PackageFile, true, token).Wait (token);
			} catch {
				// Ebil, yes, but...
			}
			ShowProgressText (string.Format ("Forcing complete uninstall of '{0}'...", PackageName), token);
			Device.UninstallPackage (PackageName, false, token).Wait (token);
			ShowProgressText (string.Format ("Installing '{0}'...", PackageName), token);
			Device.PushAndInstallPackageAsync (new PushAndInstallCommand {
				ApkFile = PackageFile,
				ReInstall = false,
				NotifyProgress = ProgressReporter.ReportProgress
			}, token).Wait (token);
			return false;
		}

		async Task FastDevAsync (bool useExternal)
		{
			var dest = await GetFastDevRemotePathAsync (useExternal);
			if (dest == null) {
				throw new AndroidDeploymentException (AndroidDeploymentFailureReason.FailedToDetermineFastDevPath);
			}

			ShowProgressText ("Using fast dev path: " + dest, token);

			await InstallAssemblies (dest, token);
			// do not call these before InstallAssemblies otherwise InstallAssemblies will clean up resources.
			await InstallNativeLibraries (dest, token);
			await InstallDexes (dest, token);
			await InstallPackagedResources (dest, token);
			await InstallTypemaps (dest, token);
		}

		async Task<string> GetFastDevRemotePathAsync (bool useExternal)
		{
			ShowProgressText ("Getting installation path...", token);

			if (!string.IsNullOrEmpty (fastDevRemotePath) && !string.IsNullOrEmpty (PackageRemotePath)) {
				ShowProgressText ($"Using cached value for installation path: {fastDevRemotePath}", token);
				ShowProgressText ($"Using cached value for package remote path: {PackageRemotePath}", token);
				return fastDevRemotePath;
			}

			if (useExternal) {
				External = true;
				PackageRemotePath = await Device.GetPackageRemotePathAsync (PackageName, token);

#pragma warning disable CS0618 // Type or member is obsolete
				fastDevRemotePath = await Device.GetFastDevRemotePathExternalAsync (PackageName, token);
#pragma warning restore CS0618

				return fastDevRemotePath;
			}

			var result = await Device.GetFastDevRemotePathAsync (PackageName, token);

			PackageRemotePath = result.PackageRemotePath;
			External          = result.IsExternal;
			fastDevRemotePath = result.Root;

			return fastDevRemotePath;
		}

		static readonly HashSet<string> AssemblyExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
			".dll",
			".mdb",
		};

		async Task InstallAssemblies (string destinationPath, CancellationToken token)
		{
			token.ThrowIfCancellationRequested ();

			#pragma warning disable 618
			IEnumerable<string> assemblies = FastDevAssembliesProvider != null? FastDevAssembliesProvider () : Assemblies;
			#pragma warning restore 618

			var progress = ProgressReporter;
			var device = Device;

			var action = ForcePackageInstall? AdbSyncAction.Copy : AdbSyncAction.CopyIfNewer;

			var cultureDirectories = new Dictionary<string, AdbSyncDirectory> ();
			var asmOverrideDir = new AdbSyncDirectory (Path.GetFileName (destinationPath), action, true);

			foreach (var a in assemblies) {
				string culture, name;
				AdbSyncDirectory dir;
				if (TryGetSatelliteCultureAndFileName (a, out culture, out name) && culture != null) {
					if (!cultureDirectories.TryGetValue (culture, out dir)) {
						cultureDirectories [culture] = dir = new AdbSyncDirectory (culture, action, true);
						asmOverrideDir.Add (dir);
					}
				} else {
					name = Path.GetFileName (a);
					dir = asmOverrideDir;
				}
				dir.Add (new AdbSyncFile (a, name, action));
			}

			var files = await device.ListFilesAsync (destinationPath.Replace ('\\', '/'), token);

			foreach (var remoteFile in files.Where (x => AssemblyExtensions.Contains (Path.GetExtension (x)))) {
				if (!assemblies.Any (x => Path.GetFileName (x) == Path.GetFileName (remoteFile))) {
					asmOverrideDir.Add (AdbSyncItem.Delete (remoteFile));
				}
			}

			ShowProgressText ("Synchronizing assemblies...", token);
			ProgressReporter.BeginStep ("Synchronizing assemblies");

			try {
				if (destinationPath.StartsWith ("/data", StringComparison.Ordinal)) {
					WaitForRemoteDirCreation (destinationPath, token);
				}

				token.ThrowIfCancellationRequested ();

				var t = device.PushSyncItems (asmOverrideDir,
					Path.GetDirectoryName (destinationPath).Replace ('\\', '/'),
					new AdbSyncClient.PushOptions () {
						DryRun = false,
						RemoveUnknown = true,
						CheckTimestamps = true,
						NotifySync = (a) => AndroidLogger.LogDebug ("NotifySync", "{0} {1} {2} {3}", a.Kind, a.LocalPath, a.RemotePath, a.Size),
						NotifyPhase = (b) => AndroidLogger.LogDebug ("NotifyPhase", "{0}", b),
						NotifyProgress = progress.ReportProgress,
						RemoveBeforeCopy = false,
					}, token);
				await t;
			} catch (Exception ex) {
				if (ex is DeviceNotFoundException
				    || ex is DeviceDisconnectedException
				    || ex is AndroidDeploymentException
				    || ex is OperationCanceledException)
					throw;

				throw new AndroidDeploymentException (AndroidDeploymentFailureReason.FailedToSynchronizeFastDevAssemblies, ex);
			} finally {
				ProgressReporter.EndStep (null);
			}
		}


		Task InstallNativeLibraries (string destinationPath, CancellationToken token)
		{
			// native lib overrides are copied to .__override__ like assemblies, so we follow that.
			return InstallFastDevFiles (destinationPath, FastDevNativeLibrariesProvider, "native libs", new string [] { ".__override__", "lib" }, removeUnknown: true, token: token);
		}

		Task InstallDexes (string destinationPath, CancellationToken token)
		{
			return InstallFastDevFiles (destinationPath, FastDevDexesProvider, "dexes", new string [] { ".__override__", "dexes" }, removeUnknown: true, token: token);
		}

		Task InstallPackagedResources (string destinationPath, CancellationToken token)
		{
			return InstallFastDevFiles (destinationPath, FastDevPackagedResourcesProvider, "resources", new string [] { ".__override__", "packaged" }, removeUnknown: true, token: token);
		}

		Task InstallTypemaps (string destinationPath, CancellationToken token)
		{
			return InstallFastDevFiles (destinationPath, FastDevTypemapsProvider, "typemaps", new string [] { ".__override__", "typemaps" }, removeUnknown: true, token: token);
		}

		async Task InstallFastDevFiles (string destinationPath, Func<IEnumerable<string>> filesProvider, string fileKind, string [] syncDirs, bool removeUnknown, CancellationToken token)
		{
			if (filesProvider == null) {
				ShowProgressText ($"Skipping {fileKind}, not configured...", token);
				return;
			}

			token.ThrowIfCancellationRequested ();

			var progress = ProgressReporter;
			var device = Device;

			var action = ForcePackageInstall? AdbSyncAction.Copy : AdbSyncAction.CopyIfNewer;

			AdbSyncDirectory overrideDir = null, destinationDir = null;
			foreach (var syncDir in syncDirs.Reverse ()) {
				overrideDir = new AdbSyncDirectory (syncDir, action, removeUnknown, Enumerable.Repeat (overrideDir, overrideDir == null ? 0 : 1));
				destinationDir = destinationDir ?? overrideDir;
			}

			var files = await device.ListFilesAsync (Path.Combine (destinationPath, destinationDir.Name).Replace ('\\', '/'), token);

			var localFiles = filesProvider ();
			foreach (var remoteFile in files) {
				if (string.IsNullOrEmpty (remoteFile))
					continue;
				if (!localFiles.Any (x => Path.GetFileName (x) == Path.GetFileName (remoteFile)))
					destinationDir.Add (AdbSyncItem.Delete (remoteFile));
			}

			foreach (var a in localFiles) {
				AdbSyncDirectory dir;
				string name = Path.GetFileName (a);
				dir = destinationDir;
				dir.Add (new AdbSyncFile (a, name, action));
			}

			ShowProgressText (string.Format ("Synchronizing {0}...", fileKind), token);
			ProgressReporter.BeginStep (string.Format ("Synchronizing {0}", fileKind));

			try {
				if (destinationPath.StartsWith ("/data", StringComparison.Ordinal)) {
					WaitForRemoteDirCreation (destinationPath, token);
				}

				token.ThrowIfCancellationRequested ();

				var t = device.PushSyncItems (overrideDir,
					Path.GetDirectoryName (destinationPath).Replace ('\\', '/'),
					new AdbSyncClient.PushOptions () {
						DryRun = false,
						RemoveUnknown = true,
						RemoveBeforeCopy = false,
						CheckTimestamps = true,
						NotifySync = (a) => AndroidLogger.LogDebug ("NotifySync", "{0} {1} {2} {3}", a.Kind, a.LocalPath, a.RemotePath, a.Size),
						NotifyPhase = (b) => AndroidLogger.LogDebug ("NotifyPhase", "{0}", b),
						NotifyProgress = progress.ReportProgress
					}, token);

				await t;
			} catch (Exception ex) {
				if (ex is DeviceNotFoundException
				    || ex is DeviceDisconnectedException
				    || ex is AndroidDeploymentException
				    || ex is OperationCanceledException)
					throw;

				throw new AndroidDeploymentException (AndroidDeploymentFailureReason.FailedToSynchronizeFastDevResources, ex);
			} finally {
				ProgressReporter.EndStep (null);
			}
		}

		void WaitForRemoteDirCreation (string destinationPath, CancellationToken token)
		{
			var device = Device;
			var packageName = PackageName;
			int count = 0;
			var wait = new ManualResetEvent (false);
			// the logic here is:
			//    check if the target directory exists
			//    if it isn't, don't try to push, start the app to get them created
			//    directories exist, push assemblies
			//
			// Note: checking if the directory exists before trying to push
			// is important because an emulator or rooted device may allow the
			// push to succeed, but it will create everything with root permissions.
			// If the app then tries to do anything with the files in the private dir,
			// things will fail. Directories must be created by monodroid for permissions
			// to be set correctly.
			//
			// All this is important for devices running Android 3.0+. Before that, just
			// killing the process was enough for monodroid to run and create the directories,
			// so by the time we reach this point, the directory already exists.

			var fi = device.GetRemoteFileInfo (destinationPath, token).Result;
			AndroidLogger.LogDebug ("Stat", "FileInfo for {0}: {1}", destinationPath, fi != null ? fi.GetSymbolicMode () : "NONE");

			if (fi == null || fi.IsFileType (AdbFileMode.S_IFREG)) {
				throw new AndroidDeploymentException (AndroidDeploymentFailureReason.FastDevFileConflict);
			}

			if (fi.IsFileType (AdbFileMode.S_IFDIR)) {
				return;
			}

			// If the directory doesn't exist, we're going to try starting the app to create it.
			// on Android 4.2+ broadcasts don't return until long after they actually hit the app, so
			// we don't block on the broastcast. Instead, we poll for the directory creation and app termination.
			device.SendSeppukuBroadcast (packageName, token);
			do {
				if (!fi.IsFileType (AdbFileMode.S_IFDIR)) {
					fi = device.GetRemoteFileInfo (destinationPath, token).Result;
					AndroidLogger.LogDebug ("Stat", "{0} FileInfo for {1} : {2}", count, destinationPath, fi.GetSymbolicMode ());
				}

				// If directory has been created, still need to make sure the process exited,
				// so we don't leave stray suicidal processes.
				if (fi.IsFileType (AdbFileMode.S_IFDIR)) {

					// If targeting Android 4.0+, we have a reliable hard kill.
					if (device.Properties.BuildVersionSdk >= 14) {
						device.KillProcess (packageName, token).Wait ();
						return;
					}

					// Else, just poll until it exits.
					if (device.GetProcessId (packageName, token).Result == 0) {
						return;
					}
				}

				token.ThrowIfCancellationRequested ();

				// The ResetEvent objects are handy for suspending operations for a bit without the
				// overhead of a Thread.Sleep
				wait.WaitOne (200);
			} while (++count <= 5*10); // this ends up waiting in chunks for about 10 seconds at the most, to give monodroid time to run.

			throw new AndroidDeploymentException (AndroidDeploymentFailureReason.FastDevDirectoryCreationFailed);
		}

		// culture match courtesy: http://stackoverflow.com/a/3962783/83444
		static readonly Regex SatelliteChecker = new Regex (
					Regex.Escape (Path.DirectorySeparatorChar.ToString ()) +
					"(?<culture>[a-zA-Z]{1,8}(-[a-zA-Z0-9]{1,8})*)" +
					Regex.Escape (Path.DirectorySeparatorChar.ToString ()) +
					string.Format ("(?<file>[^{0}]+.resources.dll)$", Regex.Escape (Path.DirectorySeparatorChar.ToString ())));

		public static bool TryGetSatelliteCultureAndFileName (string assemblyPath, out string culture, out string fileName)
		{
			culture = fileName = null;

			var m = SatelliteChecker.Match (assemblyPath);
			if (!m.Success)
				return false;

			culture   = m.Groups ["culture"].Value;
			fileName  = m.Groups ["file"].Value;
			return true;
		}
	}

	public class AndroidDeployRuntimeSession
	{
		private CancellationToken token;
		private IList<AndroidInstalledPackage> packages;

		public IProgressNotifier ProgressReporter { get; set; }

		public int PackageApiLevel { get; set; }
		public AndroidDevice Device { get; private set; }

		public bool UsesSharedRuntime { get; set; }

		/// <summary>Whether the user wants unstripped binaries and BCL debug symbols</summary>
		public bool ProvideFullDebugRuntime { get; set; }

		public string AaptToolPath { get; set; }

		public string PackageSupportedArchs { get; set; }

		public string PackageName { get; set; }


		public AndroidDeployRuntimeSession(AndroidDevice device, IList<AndroidInstalledPackage> packages = null)
		{
			Device = device;
			this.packages = packages;
		}

		public async Task StartAsync(CancellationToken token)
		{
			try
			{
				await RunAsync(token).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				// make sure we observe these exceptions otherwise they get logged as unhandled
				// so do this before the cancellation check below
				var aex = ex as AggregateException;
				if (aex != null)
				{
					ex = aex.Flatten().InnerException;
				}

				if (token.IsCancellationRequested)
				{
					AndroidLogger.LogInfo("Deployment cancelled");
					token.ThrowIfCancellationRequested();
				}

				if (ex is OperationCanceledException)
					throw ex;

				AndroidLogger.LogError("Deployment failed", ex);
				if (ex is AndroidDeploymentException)
				{
					throw;
				}

				if (ex is DeviceNotFoundException || ex is DeviceDisconnectedException)
				{
					throw new AndroidDeploymentException(AndroidDeploymentFailureReason.DeviceDisconnected, ex);
				}

				throw new AndroidDeploymentException(AndroidDeploymentFailureReason.InternalError, ex);
			}
		}

		async Task RunAsync(CancellationToken token)
		{
			if (!UsesSharedRuntime)
			{
				return;
			}

			this.token = token;

			await Device.EnsureProperties(token).ConfigureAwait(false);

			// Make sure the device arch is supported
			if (!string.IsNullOrWhiteSpace(PackageSupportedArchs) && !Device.CanRunPackageArchitecture(PackageSupportedArchs))
			{
				throw new AndroidDeploymentException(AndroidDeploymentFailureReason.ArchitectureNotSupported);
			}

			if (UsesSharedRuntime && !Device.CanRunPackageArchitecture(MonoDroidSdk.SharedRuntimeAbis))
			{
				throw new AndroidDeploymentException(AndroidDeploymentFailureReason.ArchitectureNotSupportedBySharedRuntime);
			}

			string redirectStdio = Device.Properties.Get("log.redirect-stdio");
			if (redirectStdio != null && string.Equals("true", redirectStdio.Trim(), StringComparison.OrdinalIgnoreCase))
			{
				throw new AndroidDeploymentException(AndroidDeploymentFailureReason.StdioRedirectionEnabled);
			}

			if (packages == null)
			{
				packages = await Device.GetPackagesAsync(PackageApiLevel, PackageName, ProvideFullDebugRuntime, token, ProgressReporter);
			}

			if (packages == null)
			{
				throw new AndroidDeploymentException(AndroidDeploymentFailureReason.FailedToGetPackageList);
			}

			if (UsesSharedRuntime)
			{
				await EnsureCorrectSharedRuntimes().ConfigureAwait(false);
			}
		}

		async Task EnsureCorrectSharedRuntimes()
		{
			// See if there are any old runtimes we need to remove
			await RemoveOldRuntimes().ConfigureAwait(false);

			// Install the shared runtime and platform

			try
			{
				await CheckAndInstallSharedRuntimeAsync().ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				if (ex is InsufficientSpaceException)
				{
					throw new AndroidDeploymentException(AndroidDeploymentFailureReason.InsufficientSpaceForRuntime, ex);
				}
				throw;
			}

			try
			{
				await InstallSharedPlatformAsync().ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				if (ex is InsufficientSpaceException)
				{
					throw new AndroidDeploymentException(AndroidDeploymentFailureReason.InsufficientSpaceForPlatform, ex);
				}
				if (ex is SdkNotSupportedException)
				{
					throw new AndroidDeploymentException(AndroidDeploymentFailureReason.SdkNotSupportedByDevice, ex);
				}
				throw;
			}
		}

		async Task RemoveOldRuntimes()
		{
			// See if we were asked to cancel
			if (token.IsCancellationRequested)
				return;

			// See if there are any old runtimes we need to remove
			var old_runtimes = packages.GetOldRuntimesAndPlatforms(PackageApiLevel).ToList();
			if (old_runtimes.Count == 0)
			{
				return;
			}

			ProgressReporter.BeginStep("Removing old runtimes");
			foreach (var runtime in old_runtimes)
			{
				ShowProgressText(string.Format("Removing old runtime: {0} [{1}]..", runtime.Name, runtime.Version), token);
				await Device.UninstallPackage(runtime.Name, false, token);
				packages.Remove(runtime);
			}
			ProgressReporter.EndStep(null);
		}

		// Returns whether a new shared runtime was installed
		private async Task<bool> CheckAndInstallSharedRuntimeAsync()
		{
			// See if we were asked to cancel
			if (token.IsCancellationRequested)
				return false;

			// See if the device already has the shared runtime
			var needs_runtime = !packages.IsCurrentRuntimeInstalled();

			// See if there is a runtime on the device that
			// we cannot determine the version of
			if (needs_runtime && packages.IsUnknownRuntimeInstalled())
			{
				ProgressReporter.ShowErrorDialog(
					"Unknown Runtime",
					"There is a shared runtime on the device whose version cannot be determined. " +
					"A new runtime will not be deployed. If the runtime needs to be replaced, please manually " +
					"remove it from the device.");
				needs_runtime = false;
			}

			// If needed, install the shared runtime
			if (!needs_runtime)
				return false;

			ProgressReporter.BeginStep("Installing shared runtime");
			await Device.InstallSharedRuntimeAsync(ProvideFullDebugRuntime, token, ProgressReporter);
			ProgressReporter.EndStep(null);

			return true;
		}

		// Returns whether a new shared platform was installed
		private async Task<bool> InstallSharedPlatformAsync()
		{
			// See if we were asked to cancel
			if (token.IsCancellationRequested)
				return false;

			// See if the device already has the shared platform
			var needs_platform = !packages.IsCurrentPlatformInstalled(PackageApiLevel);

			// See if there is a runtime on the device that
			// we cannot determine the version of
			if (needs_platform && packages.IsUnknownPlatformInstalled(PackageApiLevel))
			{
				ProgressReporter.ShowErrorDialog(
					"Unknown Platform Runtime",
					"There is a platform support runtime on the device whose version cannot be determined. " +
					"A new platform support runtime will not be deployed. If the platform support runtime needs " +
					"to be replaced, please manually remove it from the device.");
				needs_platform = false;
			}

			var platform_file = await PlatformPackage.GetPlatformPackagePathAsync (PackageApiLevel, AaptToolPath, ProgressReporter, token);

			// If needed, install the shared platform
			if (needs_platform)
			{
				ProgressReporter.BeginStep("Installing platform framework");
				ShowProgressText(string.Format("Installing the API {0} platform framework..", PackageApiLevel), token);
				await Device.InstallSharedPlatformAsync(platform_file, PackageApiLevel, ProgressReporter.ReportProgress, token);
				ProgressReporter.EndStep(null);
			}

			var sharedRuntimePackage = string.Format(AndroidPackageListExtensions.platformName, PackageApiLevel);
			var useXamarinPackage = sharedRuntimePackage;
			PlatformPackage.GetPlatformPackageVersion(PackageApiLevel, ref useXamarinPackage);
			if (useXamarinPackage.StartsWith("Xamarin", StringComparison.OrdinalIgnoreCase))
			{
				ShowProgressText(string.Format("Removing old {0} framework.", sharedRuntimePackage), token);
				await Device.UninstallPackage(sharedRuntimePackage, false, token).ConfigureAwait(false);
			}

			return needs_platform;
		}

		private void ShowProgressText(string text, CancellationToken token)
		{
			if (!token.IsCancellationRequested && ProgressReporter != null)
				ProgressReporter.ReportMessage(text);
		}
	}

}
