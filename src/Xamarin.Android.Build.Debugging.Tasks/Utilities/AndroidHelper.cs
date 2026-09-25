using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
		static readonly object NullDevice = new object ();

		static Tuple<string, string, string> GetKey (string target, string adbPath) =>
			new Tuple<string, string, string> ($"{nameof (AndroidHelper)}_AndroidDevice", target ?? "", adbPath);

		static void RegisterDevice (IBuildEngine4 engine, string target, string adbPath, AdbDeviceInfo device)
		{
			engine?.RegisterTaskObjectAssemblyLocal (GetKey (target, adbPath), device ?? NullDevice, Lifetime, allowEarlyCollection: false);
		}

		static object GetRegisteredDevice (IBuildEngine4 engine, string target, string adbPath) =>
			engine?.GetRegisteredTaskObjectAssemblyLocal (GetKey (target, adbPath), Lifetime);

		public static AdbRunner CreateAdbRunner (string adbToolPath, string adbToolExe)
		{
			return new AdbRunner (GetAdbPath (adbToolPath, adbToolExe));
		}

		static string GetAdbPath (string adbToolPath, string adbToolExe)
		{
			var exe = string.IsNullOrEmpty (adbToolExe) ? (OS.IsWindows ? "adb.exe" : "adb") : adbToolExe;
			return string.IsNullOrEmpty (adbToolPath) ? exe : Path.Combine (adbToolPath, exe);
		}

		public static AdbDeviceInfo ParseTarget (string target, TaskLoggingHelper log, bool logErrors = true, IBuildEngine4 engine4 = null, string adbToolPath = null, string adbToolExe = null) =>
			ParseTarget (target, m => log.LogDebugMessage (m), (c, m) => log.LogCodedError (c, m), logErrors, engine4, adbToolPath, adbToolExe);

		public static AdbDeviceInfo ParseTarget (string target, Action<string> logMessage, Action<string, string> logError, bool logErrors = true, IBuildEngine4 engine4 = null, string adbToolPath = null, string adbToolExe = null)
		{
			string adbPath = GetAdbPath (adbToolPath, adbToolExe);
			try {
				var device = GetRegisteredDevice (engine4, target, adbPath);
				if (device != null) {
					logMessage ("Using cached value from RegisterTaskObject");
					if (ReferenceEquals (device, NullDevice))
						NoDeviceFound (target, adbPath, logError, logErrors, engine4);
					return device as AdbDeviceInfo;
				}
				var devices = CreateAdbRunner (adbToolPath, adbToolExe).ListDevicesWithoutAvdNamesAsync ().GetAwaiter ().GetResult ();
				var selected = SelectDevice (devices, target);
				if (selected != null) {
					RegisterDevice (engine4, target, adbPath, selected);
					if (string.IsNullOrEmpty (target))
						RegisterDevice (engine4, $"-s {selected.Serial}", adbPath, selected);
					return selected;
				}
				if (target != null && target.Length > 0 && !target.StartsWith ("-e", StringComparison.Ordinal) &&
					!target.StartsWith ("-d", StringComparison.Ordinal) && !target.StartsWith ("-s", StringComparison.Ordinal)) {
					if (logErrors)
						logError (DefaultErrorCode, string.Format (Resources.XA0010_AdbTarget, target));
				} else {
					NoDeviceFound (target, adbPath, logError, logErrors, engine4);
				}
			} catch (Exception ex) {
				RegisterDevice (engine4, target, adbPath, null);
				if (logErrors)
					logError (DefaultErrorCode, string.Format (Resources.XA0010_Adb, ex));
				else
					logMessage (string.Format (Resources.XA0010_Adb, ex));
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

		static void NoDeviceFound (string target, string adbPath, Action<string, string> logError, bool logErrors, IBuildEngine4 engine4)
		{
			RegisterDevice (engine4, target, adbPath, null);
			if (logErrors)
				logError (DefaultErrorCode, string.IsNullOrEmpty (target) ? Resources.XA0010_NoDevice : Resources.XA0010_Selected);
		}
	}
}
