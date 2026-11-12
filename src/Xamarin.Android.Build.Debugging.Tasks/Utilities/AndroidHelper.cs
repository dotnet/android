#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;
using Xamarin.Android.Build.Debugging.Tasks.Properties;

namespace Xamarin.Android.Tasks
{
	public class AndroidHelper
	{
		const string DefaultErrorCode = "XA0010";
		const RegisteredTaskObjectLifetime Lifetime = RegisteredTaskObjectLifetime.Build;

		/// <summary>
		/// Builds the device cache key for every ParseTarget call. GetPrimaryCpuAbi, DetectIfAppWasUninstalled
		/// and FastDeploy share the cached device only when they use this method with the same TaskEnvironment.
		/// </summary>
		internal static object GetDeviceCacheKey (string target, string adbToolPath, string adbToolExe, TaskEnvironment taskEnvironment)
		{
			if (taskEnvironment == null)
				throw new ArgumentNullException (nameof (taskEnvironment));
			return GetDeviceCacheKey (target, GetAdbPath (adbToolPath, adbToolExe, taskEnvironment), taskEnvironment);
		}

		static object GetDeviceCacheKey (string target, string adbPath, TaskEnvironment taskEnvironment) =>
			(Tuple.Create ($"{nameof (AndroidHelper)}_AndroidDevice", target ?? "", adbPath), taskEnvironment.ProjectDirectory);

		// This file is compiled into both Xamarin.Android.Build.Debugging.Tasks.dll and
		// Microsoft.Android.Build.Tasks.dll (FastDeploy). Cache devices as framework types so both
		// copies share them; an empty array means no device was found.
		static void RegisterDevice (IBuildEngine4 engine, object key, AdbDeviceInfo device) =>
			engine?.RegisterTaskObject (key, device == null ? [] : new object [] {
				device.Serial, device.Description, (int) device.Type, (int) device.Status, device.AvdName,
				device.Model, device.Product, device.Device, device.TransportId, device.LongOutput,
			}, Lifetime, allowEarlyCollection: false);

		static AdbDeviceInfo ToDevice (object [] cached) => new AdbDeviceInfo {
			Serial = (string) cached [0],
			Description = (string) cached [1],
			Type = (AdbDeviceType) (int) cached [2],
			Status = (AdbDeviceStatus) (int) cached [3],
			AvdName = (string) cached [4],
			Model = (string) cached [5],
			Product = (string) cached [6],
			Device = (string) cached [7],
			TransportId = (string) cached [8],
			LongOutput = (string) cached [9],
		};

		public static AdbRunner CreateAdbRunner (string adbToolPath, string adbToolExe)
		{
			return new AdbRunner (GetAdbPath (adbToolPath, adbToolExe));
		}

		internal static AdbRunner CreateAdbRunner (string adbToolPath, string adbToolExe, TaskEnvironment taskEnvironment)
		{
			var path = GetAdbPath (adbToolPath, adbToolExe, taskEnvironment);
			var temp = OS.IsWindows ?
				new [] { "TMP", "TEMP", "USERPROFILE" }.Select (taskEnvironment.GetEnvironmentVariable)
					.FirstOrDefault (value => !string.IsNullOrEmpty (value)) ??
					Path.Combine (taskEnvironment.GetEnvironmentVariable ("SystemRoot") ??
						throw new InvalidOperationException ("The task environment has no temporary directory."), "Temp") :
				taskEnvironment.GetEnvironmentVariable ("TMPDIR");
			if (string.IsNullOrEmpty (temp))
				temp = "/tmp";
			var psi = taskEnvironment.GetProcessStartInfo ();
			// The adb server must not retain the project directory on Windows.
			psi.WorkingDirectory = taskEnvironment.GetAbsolutePath (temp);
			// GetProcessStartInfo overlays the process environment in older MSBuild versions.
			psi.EnvironmentVariables.Clear ();
			foreach (var pair in taskEnvironment.GetEnvironmentVariables ())
				psi.EnvironmentVariables [pair.Key] = pair.Value;
			return AdbRunner.CreateWithProcessEnvironment (path, psi);
		}

		static string GetAdbPath (string adbToolPath, string adbToolExe)
		{
			var exe = string.IsNullOrEmpty (adbToolExe) ? (OS.IsWindows ? "adb.exe" : "adb") : adbToolExe;
			return string.IsNullOrEmpty (adbToolPath) ? exe : Path.Combine (adbToolPath, exe);
		}

		static string GetAdbPath (string adbToolPath, string adbToolExe, TaskEnvironment taskEnvironment)
		{
			var path = GetAdbPath (adbToolPath, adbToolExe);
			if (OS.IsWindows && string.IsNullOrEmpty (Path.GetExtension (path)))
				path += ".exe";
			var projectPath = taskEnvironment.GetAbsolutePath (path);
			if (string.IsNullOrEmpty (Path.GetDirectoryName (path)) && !File.Exists (projectPath)) {
				foreach (var directory in (taskEnvironment.GetEnvironmentVariable ("PATH") ?? "").Split (Path.PathSeparator)) {
					var candidate = taskEnvironment.GetAbsolutePath (Path.Combine (directory.Trim ('"'), path));
					if (File.Exists (candidate))
						return candidate;
				}
			}
			return projectPath;
		}

		public static AdbDeviceInfo ParseTarget (string target, TaskLoggingHelper log, bool logErrors = true, IBuildEngine4 engine4 = null, string adbToolPath = null, string adbToolExe = null) =>
			ParseTarget (target, m => log.LogDebugMessage (m), (c, m) => log.LogCodedError (c, m), logErrors, engine4, adbToolPath, adbToolExe);

		internal static AdbDeviceInfo ParseTarget (string target, TaskLoggingHelper log, bool logErrors, IBuildEngine4 engine4, string adbToolPath, string adbToolExe, TaskEnvironment taskEnvironment, CancellationToken cancellationToken) =>
			ParseTarget (target, m => log.LogDebugMessage (m), (c, m) => log.LogCodedError (c, m), logErrors, engine4, adbToolPath, adbToolExe, taskEnvironment, cancellationToken);

		// MSBuild gives TaskEnvironment.Fallback to multithreadable tasks in multi-process builds.
		public static AdbDeviceInfo ParseTarget (string target, Action<string> logMessage, Action<string, string> logError, bool logErrors = true, IBuildEngine4 engine4 = null, string adbToolPath = null, string adbToolExe = null) =>
			ParseTarget (target, logMessage, logError, logErrors, engine4, adbToolPath, adbToolExe, TaskEnvironment.Fallback, default);

		internal static AdbDeviceInfo ParseTarget (string target, Action<string> logMessage, Action<string, string> logError, bool logErrors, IBuildEngine4 engine4, string adbToolPath, string adbToolExe, TaskEnvironment taskEnvironment, CancellationToken cancellationToken)
		{
			if (taskEnvironment == null)
				throw new ArgumentNullException (nameof (taskEnvironment));
			string adbPath = GetAdbPath (adbToolPath, adbToolExe, taskEnvironment);
			var key = GetDeviceCacheKey (target, adbPath, taskEnvironment);
			try {
				if (engine4?.GetRegisteredTaskObject (key, Lifetime) is object [] cached) {
					logMessage ("Using cached value from RegisterTaskObject");
					if (cached.Length == 0) {
						NoDeviceFound (target, key, logError, logErrors, engine4);
						return null;
					}
					return ToDevice (cached);
				}
				var runner = CreateAdbRunner (adbToolPath, adbToolExe, taskEnvironment);
				var devices = runner.ListDevicesWithoutAvdNamesAsync (cancellationToken).GetAwaiter ().GetResult ();
				var selected = SelectDevice (devices, target);
				if (selected != null) {
					RegisterDevice (engine4, key, selected);
					if (string.IsNullOrEmpty (target))
						RegisterDevice (engine4, GetDeviceCacheKey ($"-s {selected.Serial}", adbPath, taskEnvironment), selected);
					return selected;
				}
				if (target != null && target.Length > 0 && !target.StartsWith ("-e", StringComparison.Ordinal) &&
					!target.StartsWith ("-d", StringComparison.Ordinal) && !target.StartsWith ("-s", StringComparison.Ordinal)) {
					if (logErrors)
						logError (DefaultErrorCode, string.Format (System.Globalization.CultureInfo.CurrentCulture, Resources.XA0010_AdbTarget, target));
				} else {
					NoDeviceFound (target, key, logError, logErrors, engine4);
				}
			} catch (Exception ex) {
				RegisterDevice (engine4, key, null);
				if (logErrors)
					logError (DefaultErrorCode, string.Format (System.Globalization.CultureInfo.CurrentCulture, Resources.XA0010_Adb, ex));
				else
					logMessage (string.Format (System.Globalization.CultureInfo.CurrentCulture, Resources.XA0010_Adb, ex));
			}
			return null;
		}

		internal static AdbDeviceInfo SelectDevice (IReadOnlyList<AdbDeviceInfo> devices, string target)
		{
			if (string.IsNullOrEmpty (target))
				return devices.FirstOrDefault ();
			if (target.StartsWith ("-e", StringComparison.Ordinal))
				return devices.FirstOrDefault (x => x.IsEmulator);
			if (target.StartsWith ("-d", StringComparison.Ordinal))
				return devices.FirstOrDefault (x => !x.IsEmulator);
			if (target.StartsWith ("-s", StringComparison.Ordinal)) {
				string serial = target.Substring (2).Trim ();
				return devices.FirstOrDefault (x => serial == x.Serial);
			}
			return null;
		}

		static void NoDeviceFound (string target, object key, Action<string, string> logError, bool logErrors, IBuildEngine4 engine4)
		{
			RegisterDevice (engine4, key, null);
			if (logErrors)
				logError (DefaultErrorCode, string.IsNullOrEmpty (target) ? Resources.XA0010_NoDevice : Resources.XA0010_Selected);
		}
	}
}
