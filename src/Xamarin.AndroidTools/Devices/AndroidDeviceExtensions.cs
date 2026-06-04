
// AndroidDeviceExtensions.cs
//
// Authors:
//       Jonathan Pobst <jpobst@xamarin.com>
//
// Copyright 2011 Xamarin Inc. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Mono.AndroidTools;
using Mono.AndroidTools.Adb;
using Xamarin.AndroidTools;
using Xamarin.AndroidTools.Debugging;

// These are methods for Mono.AndroidTools.AndroidDevice, but
// are too MFA specific to go into Mono.AndroidTools.


public static class AndroidDeviceExtensions
{
	[Obsolete ("Use the async overload")]
	public static void EnsureProperties (this AndroidDevice device)
	{
		EnsureProperties (device, CancellationToken.None).Wait ();
	}

	public static Task EnsureProperties (this AndroidDevice device, CancellationToken cancellationToken)
	{
		if (device.Properties == null) {
			return device.RefreshProperties (cancellationToken);
		}
		var tcs = new TaskCompletionSource<object> ();
		tcs.SetResult (null);
		return tcs.Task;
	}

	public static Task SendSeppukuBroadcast (this AndroidDevice device, string packageName)
	{
		return SendSeppukuBroadcast (device, packageName, CancellationToken.None);
	}

	public static Task SendSeppukuBroadcast (this AndroidDevice device, string packageName, CancellationToken cancellationToken)
	{
		var action = "mono.android.intent.action.SEPPUKU";
		var category = string.Format ("mono.android.intent.category.SEPPUKU.{0}", packageName);
		var name     = packageName + "/mono.android.Seppuku";

		return device.Broadcast (action, new[] { category }, null, name, null, cancellationToken);
	}

	public static Task KillProcess (this AndroidDevice device, string packageName)
	{
		return KillProcess (device, packageName, CancellationToken.None);
	}

	public static Task KillProcess (this AndroidDevice device, string packageName, CancellationToken cancellationToken)
	{
		if (device.Properties.BuildVersionSdk >= 14) {
			return device.ForceStop (packageName, cancellationToken);
		}
		return SendSeppukuBroadcast (device, packageName, cancellationToken);
	}

	public static Task<bool> KillProcessIfRunningAndWaitForExit (this AndroidDevice device, string packageName, CancellationToken token)
	{
		AndroidLogger.LogDebug ("KillProcessIfRunningAndWaitForExit", "Checking whether app {0} is running", packageName);
		var tcs = new TaskCompletionSource<bool> ();
		device.GetProcessId (packageName).ContinueWith (t => {
			try {
				if (t.IsFaulted) {
					tcs.SetException (t.Exception);
				} else if (token.IsCancellationRequested) {
					tcs.SetCanceled ();
				} else if (t.Result == 0) {
					AndroidLogger.LogDebug ("KillProcessIfRunningAndWaitForExit", "App was not running, skipping kill");
					tcs.SetResult (false);
				} else {
					KillProcessAndWaitForExit (device, packageName, token).ContinueWith (t2 => {
						if (t2.IsFaulted) {
							tcs.SetException (t2.Exception);
						} else if (token.IsCancellationRequested) {
							tcs.SetCanceled ();
						} else {
							tcs.SetResult (true);
						}
					}, token, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
				}
			} catch (Exception ex) {
				tcs.TrySetException (ex);
			}
		}, token, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
		return tcs.Task;
	}

	public static Task KillProcessAndWaitForExit (this AndroidDevice device, string packageName, CancellationToken token)
	{
		//if using 4.0+, we have a reliable hard kill, no need to seppuku and poll
		if (device.Properties.BuildVersionSdk >= 14) {
			return device.KillProcess (packageName, token);
		}

		return KillProcessAndWaitForExitPreIcs (device, packageName, token);
	}

	static Task KillProcessAndWaitForExitPreIcs (AndroidDevice device, string packageName, CancellationToken token)
	{
		var tcs = new TaskCompletionSource<bool> ();
		KillProcess (device, packageName, token).ContinueWith (r => {
			try {
				if (r.IsFaulted) {
					tcs.SetException (r.Exception);
				} else if (token.IsCancellationRequested) {
					tcs.SetCanceled ();
				} else {
					AndroidLogger.LogDebug ("KillProcessIfRunning", "Waiting for process to exit");
					RepeatTaskUntilTrue (() => device.GetProcessId (packageName, token), i => i == 0, token)
					.ContinueWith (t2 => {
						try {
							if (t2.IsFaulted) {
								tcs.SetException (t2.Exception);
							} else if (token.IsCancellationRequested) {
								tcs.SetCanceled ();
							} else {
								AndroidLogger.LogDebug ("KillProcessIfRunning", "Process exited");
								tcs.SetResult (true);
							}
						} catch (Exception ex) {
							tcs.TrySetException (ex);
						}
					}, token, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
				}
			} catch (Exception ex) {
				tcs.TrySetException (ex);
			}
		}, token, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
		return tcs.Task;
	}

	public static async Task<int> GetProcessIDAsync(this AndroidDevice device, string packageName, int maxAttempts, int timeBetweenAttempts, CancellationToken token)
	{
		var retryCount = 1;
		var pidOfResult = await device.GetProcessId(packageName, token);
		AndroidLogger.LogDebug("GetProcessIDAsync", "PID of the application :: " + pidOfResult);

		while (pidOfResult <= 0 && retryCount <= maxAttempts)
		{
			await Task.Delay(timeBetweenAttempts);
			pidOfResult = await device.GetProcessId(packageName, token);
			AndroidLogger.LogDebug("GetProcessIDAsync", "Retrying " + retryCount + " time(s) to get PID of the application");
			retryCount++;
		}
		return pidOfResult;
	}


	static Task RepeatTaskUntilTrue<T> (Func<Task<T>> createTask, Func<T,bool> checkResult, CancellationToken token)
	{
		var tcs = new TaskCompletionSource<object> ();
		Action<Task<T>> f = null;
		f = t => {
			try {
				if (t.IsFaulted) {
					tcs.SetException (t.Exception);
				} else if (token.IsCancellationRequested) {
					tcs.SetCanceled ();
				} else if (checkResult (t.Result)) {
					tcs.SetResult (null);
				} else {
					createTask ().ContinueWith (f, token, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
				}
			} catch (Exception ex) {
				tcs.TrySetException (ex);
			}
		};
		createTask ().ContinueWith (f, token, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
		return tcs.Task;
	}

	[Obsolete ("Use PushAndInstallPackageAsync (PushAndInstallCommand command) instead.")]
	public static Task PushAndInstallPackage (this AndroidDevice device, string apkFile, bool reinstall, AdbProgressReporter notifyProgress = null, CancellationToken token = new CancellationToken ())
	{
		return PushAndInstallPackage (device, apkFile, null, reinstall, notifyProgress: notifyProgress, token: token);
	}

	[Obsolete ("Use PushAndInstallPackageAsync (PushAndInstallCommand command) instead.")]
	public static Task PushAndInstallPackage(this AndroidDevice device, string apkFile, string packageName, bool reinstall, AdbProgressReporter notifyProgress = null, CancellationToken token = new CancellationToken())
	{
		var command = new PushAndInstallCommand () {
			ApkFile = apkFile,
			PackageName = packageName,
			ReInstall = reinstall,
			NotifyProgress = notifyProgress,
		};
		return PushAndInstallPackageAsync (device, command, token: token);
	}

	public static async Task PushAndInstallPackageAsync (this AndroidDevice device, PushAndInstallCommand command, CancellationToken token = new CancellationToken ())
	{
		var remoteApkFile = AndroidDevice.GetRemoteTempApkPath (command.ApkFile);

		PmInstallCommand pmInstallCommand = new PmInstallCommand () {
			RemoteApkFile = remoteApkFile,
			User = command.User,
		};

		try {
			// Copy the apk over to a temp location
			await device.Push (command.ApkFile, remoteApkFile, command.NotifyProgress, token);
			await device.EnsureProperties (token);

			// Tell Android to install it
			var flags = device.Properties.BuildVersionSdk < 19 ? AdbInstallFlags.None : AdbInstallFlags.AllowDowngrade;
			if (command.ReInstall)
				flags |= AdbInstallFlags.Reinstall;
			if (command.TestOnly)
				flags |= AdbInstallFlags.TestOnly;
			pmInstallCommand.Flags = flags;
			await device.InstallPackage (pmInstallCommand, token);
		} catch (PackageAlreadyExistsException) {
			if (!string.IsNullOrEmpty (command.PackageName)) {
				AndroidLogger.LogInfo ($"Looks like the package `{command.PackageName}` is already installed. Trying to uninstall it first.");
				pmInstallCommand.Flags = AdbInstallFlags.None;
				await device.UninstallPackage (command.PackageName, preserveData: true, cancellationToken: token);
				await device.InstallPackage (pmInstallCommand, token);
			} else {
				throw;
			}
		} finally {
			try {
				// Delete the temp apk
				await device.DeleteFile (remoteApkFile, false, token);
			} catch (Exception ex) {
				// Don't let this crash
				AndroidLogger.LogInfo ($"Failed to delete package file: {ex.Message}");
			}
		}
	}

	[Obsolete ("Use StartWithDebuggingAsync")]
	public static Task StartActivityWithDebugging (this AndroidDevice device, string package, string activity,
		IPAddress address, int sdbPort, int stdoutPort, bool server)
	{
		return StartActivityWithDebugging (device, package, activity, address, sdbPort, stdoutPort, server, CancellationToken.None);
	}

	[Obsolete("Use StartWithDebuggingAsync")]
	public async static Task StartActivityWithDebugging (this AndroidDevice device, string package, string activity,
        IPAddress address, int sdbPort, int stdoutPort, bool server, CancellationToken token)
	{
		var androidDevice = (IAndroidDevice)device;

		var debuggerOptions = new DebuggerOptions(address, sdbPort, stdoutPort, server);

		await androidDevice.SetDebugPropertiesAsync(package, debuggerOptions, token).ConfigureAwait(false);

		// if the startCommand is null it is because there is no activity to start, in which case
		// we will return a completed task and the user will have to manually start up the process
		if (string.IsNullOrEmpty(activity))
			return;

		var command = new AmStartCommand(package, activity);
		command.Action = command.Action ?? "android.intent.action.MAIN";
		command.Categories = command.Categories ?? new[] { "android.intent.category.LAUNCHER" };
		await device.ExecuteIntentCommandAsync(command, null, token).ConfigureAwait(false);
	}

	[Obsolete ("Use SetDebugPropertiesAsync")]
	public static Task SetDebugProperties (this AndroidDevice device, IPAddress address, int sdbPort, int stdoutPort,
		bool server, CancellationToken token)
	{
		return SetDebugProperties (device, null, address, sdbPort, stdoutPort, server, token);
	}

	/// <summary>
	/// Returns a Task which sets up the debug property
	/// </summary>
	[Obsolete ("Use SetDebugPropertiesAsync")]
	public static Task SetDebugProperties (this AndroidDevice device, AmStartCommand  startCommand, IPAddress address, int sdbPort, int stdoutPort, bool server, CancellationToken token)
	{
		const int loglevel = 0;

		// Get the time the device thinks it is, and add 30 seconds
		return device.GetDate (token).ContinueWith (t => {
			long expire_date = t.Result + 30; // 30 seconds
			string endpoint = stdoutPort > -1
				? string.Format ("{0}:{1}:{2}", address, sdbPort, stdoutPort)
				: string.Format ("{0}:{1}", address, sdbPort);

			// Set property to tell the device to launch in debug mode
			string debugArg = string.Format (
				                  "debug={0},timeout={1},loglevel={2},server={3}",
				                  endpoint, expire_date, loglevel, server ? "y" : "n"
			                  );

			return device.SetProperty ("debug.mono.extra", debugArg, token).ContinueWith (r => {
				return device.SetFastDevPropertyFile (startCommand?.PackageName, "debug.mono.extra", debugArg, token);
			}, token, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default).Unwrap ();
		}, token, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default).Unwrap ();
	}

	public static Task StartActivityWithoutDebugging (this AndroidDevice device, string package, string activity)
	{
		return StartActivityWithoutDebugging (device, package, activity, CancellationToken.None);
	}

	public static Task StartActivityWithoutDebugging (this AndroidDevice device, string package, string activity, CancellationToken token)
	{
		return StartActivityWithoutDebugging (device, new AmStartCommand (package, activity), token);
	}

	public static Task StartActivityWithoutDebugging (this AndroidDevice device, AmStartCommand startCommand, CancellationToken token = default(CancellationToken))
	{
		// In case the user is quick and the 30 second timeout is still valid, we'll go ahead and reset it
		return device.SetProperty ("debug.mono.extra", string.Empty, token).ContinueWith (r => {
			return device.SetFastDevPropertyFile (startCommand?.PackageName, "debug.mono.extra", string.Empty, token);
		}).ContinueWith (t => {
			if (t.IsFaulted)
				throw t.Exception;
			token.ThrowIfCancellationRequested ();
			// Launch the activity
			var command = new AmStartCommand (startCommand);
			command.Action = command.Action ?? "android.intent.action.MAIN";
			command.Categories = command.Categories ?? new [] {"android.intent.category.LAUNCHER"};
			return device.ExecuteIntentCommandAsync (command, null, token);
		}, token, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default).Unwrap ();
	}

	public static Task SetFastDevPropertyFile (this AndroidDevice device, string package, string property, string value,
		CancellationToken token)
	{
		return device.RefreshProperties (token).ContinueWith (r => {
			var propertyValue = device.Properties.Get (property);
			if (string.IsNullOrEmpty (propertyValue) || propertyValue != value || string.IsNullOrEmpty (value)) {
				return device.SetInternalPropertyFile (package, property, value, token);
			}
			return r;
		}, token, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default).Unwrap ();
	}

	static Task TaskDelay (int delayMilliseconds)
	{
		var tcs = new TaskCompletionSource<object>();
		var timer = new System.Timers.Timer {
			Interval = delayMilliseconds,
			AutoReset = false
		};
		timer.Elapsed += (obj, args) => {
			tcs.TrySetResult (true);
			timer.Dispose ();
		};
		timer.Start();
		return tcs.Task;
	}

	static public Task<AndroidCommandSession> StartActivityWithCommandSession (this AndroidDevice device, string package, string activity, IPAddress address, int port, CancellationToken token)
	{
		var tcs = new TaskCompletionSource<AndroidCommandSession> ();
		var connection = new AndroidConnectCommandSession(address, port);

		var launchTask = AdbServer.Default.ForwardPort (device, port, port, token)
			.ContinueWith (t => {
				t.Wait ();
				return device.SetProperty ("debug.mono.connect", "port=" + port);
			}, token, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default).Unwrap ()
			.ContinueWith (t => {
				t.Wait ();
				return device.KillProcessIfRunningAndWaitForExit (package, token);
			}, token, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default).Unwrap ()
			.ContinueWith (t => {
				t.Wait ();
				return device.StartActivity (
					"android.intent.action.MAIN", new [] { "android.intent.category.LAUNCHER" },
					package, activity, false, token);
			}, token, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default).Unwrap ();

		// This just initializes the socket, but doesn't actually do any work yet
		Action<Task> onConnect = null;

		Action cleanup = () => {
			try {
				connection.Dispose();
			} catch (Exception ex) {
				AndroidLogger.LogError ("Failed to clean up Android command session", ex);
			}
		};

		int retries = 10;
		onConnect = t => {
			//faulted, and out of retries or not an IOException
			if (t.IsFaulted && (!(t.Exception.Flatten().InnerException is IOException) || retries < 0)) {
				tcs.TrySetException(t.Exception);
				cleanup();
				return;
			}
			//cancelled
			if (t.IsCanceled || token.IsCancellationRequested) {
				tcs.TrySetCanceled();
				cleanup();
				return;
			}
			//success
			if (t.IsCompleted && !t.IsFaulted) {
				tcs.TrySetResult (connection);
				device.SetProperty ("debug.mono.connect", "", token).Wait ();
				return;
			}

			cleanup();

			//retry after a small delay
			retries--;
			TaskDelay(200).ContinueWith (td => {
				if (td.IsFaulted) {
					tcs.TrySetException (td.Exception);
				} else if (td.IsCanceled || token.IsCancellationRequested) {
					tcs.TrySetCanceled ();
				} else {
					try {
						connection = new AndroidConnectCommandSession(address, port);
						Task.Factory.FromAsync (connection.BeginHandshake (), connection.EndHandshake).ContinueWith (onConnect);
					} catch (Exception ex) {
						tcs.TrySetException (ex);
					}
				}
			});
		};

		launchTask.ContinueWith (t => {
			try {
				t.Wait();
				token.Register (cleanup);
				Task.Factory.FromAsync(connection.BeginHandshake (), connection.EndHandshake)
					.ContinueWith (onConnect);
			} catch (Exception ex) {
				tcs.TrySetException (ex);
			}
		});

		return tcs.Task;
	}

	public static async Task InstallSharedRuntimeAsync (this AndroidDevice device, string runtimeFile, AdbProgressReporter progress, CancellationToken token)
	{
		// See if we were asked to cancel
		token.ThrowIfCancellationRequested ();

		try {
			await device.PushAndInstallPackageAsync (new PushAndInstallCommand () {
				ApkFile = runtimeFile,
				ReInstall = false,
				NotifyProgress = progress
			}, token);
		} catch (Exception ex) {
			var aex = ex as AggregateException;
			if (aex != null) {
				ex = aex.Flatten ().InnerException;
			}

			// If the runtime already exists, ignore the error
			// Sometimes android doesn't report it's installed when it is  :/
			if (ex is PackageAlreadyExistsException)
				return;

			throw;
		}
	}

	[Obsolete ("Use InstallSharedRuntimeAsync")]
	public static void InstallSharedRuntime (this AndroidDevice device, string runtimeFile, AdbProgressReporter progress, CancellationToken token)
	{
		// See if we were asked to cancel
		token.ThrowIfCancellationRequested ();

		try {
			device.PushAndInstallPackageAsync (new PushAndInstallCommand () {
				ApkFile = runtimeFile,
				ReInstall = false,
				NotifyProgress = progress,
			}, token).Wait ();
		} catch (Exception ex) {
			var aex = ex as AggregateException;
			if (aex != null) {
				ex = aex.Flatten ().InnerException;
			}

			// If the runtime already exists, ignore the error
			// Sometimes android doesn't report it's installed when it is  :/
			if (ex is PackageAlreadyExistsException)
				return;

			throw;
		}
	}

	public static async Task InstallSharedPlatformAsync (this AndroidDevice device, string platformFile, int packageApiLevel, AdbProgressReporter progress, CancellationToken token)
	{
		// See if we were asked to cancel
		token.ThrowIfCancellationRequested ();

		try {
			await device.PushAndInstallPackageAsync (new PushAndInstallCommand () {
				ApkFile = platformFile,
				PackageName = GetPlatformPackageName (packageApiLevel),
				ReInstall = false,
				NotifyProgress = progress,
			}, token);
		} catch (Exception ex) {
			var aex = ex as AggregateException;
			if (aex != null) {
				ex = aex.Flatten ().InnerException;
			}

			// If the runtime already exists, ignore the error
			// Sometimes android doesn't report it's installed when it is  :/
			if (ex is PackageAlreadyExistsException)
				return;

			throw;
		}
	}

	[Obsolete ("Use InstallSharedPlatformAsync")]
	public static void InstallSharedPlatform (this AndroidDevice device, string platformFile, int packageApiLevel, AdbProgressReporter progress, CancellationToken token)
	{
		// See if we were asked to cancel
		token.ThrowIfCancellationRequested ();

		try {
			device.PushAndInstallPackageAsync (new PushAndInstallCommand () {
				ApkFile = platformFile,
				PackageName = GetPlatformPackageName (packageApiLevel),
				ReInstall = false,
				NotifyProgress = progress,
			}, token).Wait ();
		} catch (Exception ex) {
			var aex = ex as AggregateException;
			if (aex != null) {
				ex = aex.Flatten ().InnerException;
			}

			// If the runtime already exists, ignore the error
			// Sometimes android doesn't report it's installed when it is  :/
			if (ex is PackageAlreadyExistsException)
				return;

			throw;
		}
	}

	public static AndroidDeploySession GetDeploySession (this AndroidDevice device, IList<AndroidInstalledPackage> packages = null)
	{
		return new AndroidDeploySession (device, packages);
	}

	public static async Task<IList<AndroidInstalledPackage>> GetPackagesAsync (this AndroidDevice device, int packageApiLevel, string packageName, bool provideFullDebugRuntime, CancellationToken cancellationToken, IProgressNotifier progressReporter)
	{
		// See what API level the device supports
		await device.EnsureProperties (cancellationToken).ConfigureAwait(false);

		progressReporter.BeginStep ("Detecting installed packages");
		progressReporter.ReportMessage ("Detecting installed packages...");
		try {
			string versions = await GetPackageVersionsAsync (device, packageApiLevel, packageName, cancellationToken).ConfigureAwait(false);
			if (!string.IsNullOrWhiteSpace (versions))
				return GetInstalledPackages (versions);

			var p = await GetInstalledPackagesFromDatabaseAsync (device, true, cancellationToken).ConfigureAwait(false);
			if (p != null)
				return p;

			return await GetInstalledPackagesFromDatabaseAsync (device, false, cancellationToken).ConfigureAwait(false);
		} finally {
			progressReporter.EndStep ("");
		}
	}

	[Obsolete ("Use GetPackagesAsync")]
	public static IList<AndroidInstalledPackage> GetPackages (this AndroidDevice device, int packageApiLevel, string packageName, bool provideFullDebugRuntime, CancellationToken cancellationToken, IProgressNotifier progressReporter)
	{
		// See what API level the device supports
		device.EnsureProperties (cancellationToken).Wait ();

		progressReporter.BeginStep ("Detecting installed packages");
		progressReporter.ReportMessage ("Detecting installed packages...");
		try {
			string versions = GetPackageVersions (device, packageApiLevel, packageName, cancellationToken);
			if (!string.IsNullOrWhiteSpace (versions))
				return GetInstalledPackages (versions);

			var p = GetInstalledPackagesFromDatabase (device, true, cancellationToken);
			if (p != null)
				return p;

			return GetInstalledPackagesFromDatabase (device, false, cancellationToken);
		} finally {
			progressReporter.EndStep ("");
		}
	}

	[Obsolete("Use GetInstalledPackagesFromDatabaseAsync")]
	static IList<AndroidInstalledPackage> GetInstalledPackagesFromDatabase (AndroidDevice device, bool requireVersions, CancellationToken cancellationToken)
	{
		return device.GetPackages (requireVersions, cancellationToken).Result;
	}

	static Task<List<AndroidInstalledPackage>> GetInstalledPackagesFromDatabaseAsync (AndroidDevice device, bool requireVersions, CancellationToken cancellationToken)
	{
		return device.GetPackages (requireVersions, cancellationToken);
	}

	static string GetPlatformPackageName (int packageApiLevel)
	{
		return string.Format (AndroidPackageListExtensions.platformName, packageApiLevel);
	}

	[Obsolete("Use GetPackageVersionsAsync")]
	static string GetPackageVersions (AndroidDevice device, int packageApiLevel, string packageName, CancellationToken cancellationToken)
	{
		var action    = "mono.android.intent.action.PACKAGE_VERSIONS";
		var platform  = GetPlatformPackageName (packageApiLevel);
		PlatformPackage.GetPlatformPackageVersion (packageApiLevel, ref platform);
		var packages  = string.Join (",", new[]{
				AndroidPackageListExtensions.runtimeName,
				platform,
				packageName,
		});
		var extras = new Dictionary<string, string> {
			{ "packages", packages },
		};
		return device.Broadcast (action,
					null,
					extras,
					"Mono.Android.DebugRuntime/com.xamarin.mono.android.PackageVersions",
					cancellationToken).Result;
	}

	async static Task<string> GetPackageVersionsAsync(AndroidDevice device, int packageApiLevel, string packageName, CancellationToken cancellationToken)
	{
		var action = "mono.android.intent.action.PACKAGE_VERSIONS";
		var platform = GetPlatformPackageName(packageApiLevel);
		var packages = string.Join(",", new[]{
				AndroidPackageListExtensions.runtimeName,
				platform,
				packageName,
		});
		var extras = new Dictionary<string, string> {
			{ "packages", packages },
		};
		return await device.Broadcast(action,
					null,
					extras,
					"Mono.Android.DebugRuntime/com.xamarin.mono.android.PackageVersions",
                    cancellationToken).ConfigureAwait(false);
	}

	static IList<AndroidInstalledPackage> GetInstalledPackages (string packageVersions)
	{
		return packageVersions.Split (new[]{','}, StringSplitOptions.RemoveEmptyEntries).Select (v => {
				string[] p = v.Split ('=');
				return new AndroidInstalledPackage (p [0], null, int.Parse (p [1]));
		}).ToList ();
	}

	internal static async Task InstallSharedRuntimeAsync (this AndroidDevice device, bool provideFullDebugRuntime, CancellationToken cancellationToken, IProgressNotifier progressReporter)
	{
		var arch = device.Properties.ProductCpuAbi;
		progressReporter.ReportMessage ("Target device is " + arch + ".");

		var runtime_file = MonoDroidSdk.GetSharedRuntimePackage (provideFullDebugRuntime, arch);
		var runtime_desc = runtime_file.EndsWith ("-debug.apk", StringComparison.Ordinal) ? "debug" : arch;

		// Install the runtime
		var text = string.Format ("Installing the Mono shared runtime ({0} - {1})...", runtime_desc, MonoDroidSdk.SharedRuntimeVersion);
		progressReporter.ReportMessage (text);
		await device.InstallSharedRuntimeAsync (runtime_file, progressReporter.ReportProgress, cancellationToken);
	}

	[Obsolete ("Use InstallSharedRuntimeAsync")]
	internal static void InstallSharedRuntime (this AndroidDevice device, bool provideFullDebugRuntime, CancellationToken cancellationToken, IProgressNotifier progressReporter)
	{
		var arch = device.Properties.ProductCpuAbi;
		progressReporter.ReportMessage ("Target device is " + arch + ".");

		var runtime_file = MonoDroidSdk.GetSharedRuntimePackage (provideFullDebugRuntime, arch);
		var runtime_desc = runtime_file.EndsWith ("-debug.apk", StringComparison.Ordinal) ? "debug" : arch;

		// Install the runtime
		var text = string.Format ("Installing the Mono shared runtime ({0} - {1})...", runtime_desc, MonoDroidSdk.SharedRuntimeVersion);
		progressReporter.ReportMessage (text);
		device.InstallSharedRuntime (runtime_file, progressReporter.ReportProgress, cancellationToken);
	}

	const string PackageInstallLocationFormat = "data/{0}/files/.__override__";

	[Obsolete ("Use GetPackageRemotePathAsync")]
	public static string GetPackageRemotePath (this AndroidDevice device, string packageName, CancellationToken cancellationToken)
	{
		var x = device.RunShellCommand (cancellationToken, "pm", "path", packageName).Result;

		string[] packagePathInfo = x.Split (':');
		if (packagePathInfo.Length <= 1) {
			throw new AndroidDeploymentException (AndroidDeploymentFailureReason.InternalError,
					new InvalidOperationException (
						string.Format ("Could not determine the installation path for package {0}. " +
							"`adb shell pm path {0}` returned '{1}'.", packageName, x)));
		}
		return packagePathInfo [1];
	}

	public static async Task<string> GetPackageRemotePathAsync (this AndroidDevice device, string packageName, CancellationToken cancellationToken)
	{
		var x = await device.RunShellCommand (cancellationToken, "pm", "path", packageName);

		string[] packagePathInfo = x.Split (':');
		if (packagePathInfo.Length <= 1) {
			throw new AndroidDeploymentException (AndroidDeploymentFailureReason.InternalError,
				new InvalidOperationException (
					string.Format ("Could not determine the installation path for package {0}. " +
						"`adb shell pm path {0}` returned '{1}'.", packageName, x)));
		}
		return packagePathInfo [1];
	}

	public static async Task<string> GetFastDevRemotePathInternalAsync (this AndroidDevice device, string packageName, CancellationToken cancellationToken)
	{
		var internalPath = await device.RunShellCommand (cancellationToken, "run-as", packageName, "pwd");
		if (internalPath.IndexOf ("Permission denied", StringComparison.OrdinalIgnoreCase) >= 0) {
			internalPath = await device.RunShellCommand (packageName, "readlink", "-f", ".");
		}
		if (internalPath.IndexOf ("run-as:", StringComparison.OrdinalIgnoreCase) >= 0 ||
				internalPath.IndexOf ("package not debuggable", StringComparison.OrdinalIgnoreCase) >= 0 ||
				internalPath.IndexOf ("unknown", StringComparison.OrdinalIgnoreCase) >= 0 ||
				internalPath.IndexOf ("Permission denied", StringComparison.OrdinalIgnoreCase) >= 0) {
			return null;
		}
		return internalPath;
	}

	[Obsolete ("Use GetFastDevRemotePathExternalAsync()")]
	public static string GetFastDevRemotePathExternal (this AndroidDevice device, string packageName, CancellationToken cancellationToken)
	{
		return GetFastDevRemotePathExternalAsync (device, packageName, cancellationToken).Result;
	}

	[Obsolete ("Use GetFastDevRemotePathInternalAsync. Shared Runtime is no longer supported.")]
	public static async Task<string> GetFastDevRemotePathExternalAsync (this AndroidDevice device, string packageName, CancellationToken cancellationToken)
	{
		// EMULATED_STORAGE_SOURCE=/mnt/shell/emulated
		// EMULATED_STORAGE_TARGET=/storage/emulated
		// EXTERNAL_STORAGE_DIRECTORY broadcast returns:
		//  *   Primary user: "/mnt/shell/emulated/0" or "/storage/emulated/legacy"
		//  * Secondary user: "/storage/emulated/11"

		var source  = device.RunShellCommand (cancellationToken, "echo", "-n", "${EMULATED_STORAGE_SOURCE}").Result;
		var target  = device.RunShellCommand (cancellationToken, "echo", "-n", "${EMULATED_STORAGE_TARGET}").Result;
		var esd     = await device.Broadcast (
				new AmBroadcastCommand {
					Action    = "mono.android.intent.action.EXTERNAL_STORAGE_DIRECTORY",
					Component = "Mono.Android.DebugRuntime/com.xamarin.mono.android.ExternalStorageDirectory",
				},
				cancellationToken);

		if (string.IsNullOrEmpty (esd)) {
			esd = await device.RunShellCommand (cancellationToken, "echo", "-n", "${EXTERNAL_STORAGE}");
		}
		if (!string.IsNullOrEmpty (source) && !string.IsNullOrEmpty (target) && esd.StartsWith (target, StringComparison.Ordinal)) {
			esd = esd.Replace (target, source);
		}
		return string.Format ("{0}/Android/{1}", esd, string.Format (PackageInstallLocationFormat, packageName));
	}

	public class FastDevRemotePathInfo
	{
		public FastDevRemotePathInfo (string fastDevRemotePath, string packageRemotePath, bool external)
		{
			this.Root = fastDevRemotePath;
			PackageRemotePath = packageRemotePath;
			this.IsExternal = external;
		}

		public string Root { get; set; }
		public string PackageRemotePath { get; set; }
		public bool IsExternal { get; set; }
	}

	[Obsolete ("Use GetFastDevRemotePathAsync()")]
	public static string GetFastDevRemotePath (this AndroidDevice device, string packageName, CancellationToken cancellationToken, out string packageRemotePath, out bool external)
	{
		var ret = GetFastDevRemotePathAsync (device, packageName, cancellationToken).Result;
		packageRemotePath = ret.PackageRemotePath;
		external = ret.IsExternal;
		return ret.Root;
	}

	public static async Task<FastDevRemotePathInfo> GetFastDevRemotePathAsync (this AndroidDevice device, string packageName, CancellationToken cancellationToken)
	{
		string packageRemotePath = await GetPackageRemotePathAsync (device, packageName, cancellationToken);
		bool external = !packageRemotePath.StartsWith ("/data", StringComparison.Ordinal);

		var root = "/data/";
		if (external) {
			var ex = await device.Broadcast ("mono.android.intent.action.EXTERNAL_STORAGE_DIRECTORY", null, cancellationToken);
			if (!string.IsNullOrEmpty (ex))
				root = ex + "/Android/";
		}

		return new FastDevRemotePathInfo (root + string.Format (PackageInstallLocationFormat, packageName), packageRemotePath, external);
	}
}

public class PushAndInstallCommand {
	public string ApkFile { get; set;}
	public string User { get; set;}
	public bool ReInstall { get; set; }
	public string PackageName { get; set; }
	public bool TestOnly { get; set; } = false;
	public AdbProgressReporter NotifyProgress { get; set;}
}
