using System;
using System.Linq;
using Mono.AndroidTools;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Build.Debugging.Tasks.Properties;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class AndroidHelper
	{
		const string DefaultErrorCode = "XA0010";
		const RegisteredTaskObjectLifetime Lifetime = RegisteredTaskObjectLifetime.Build;
		static readonly object NullDevice = new object ();

		static Tuple<string, string> GetKey (string target) =>
			new Tuple<string, string> ($"{nameof (AndroidHelper)}_AndroidDevice", target ?? "");

		/// <summary>
		/// Stores the AndroidDevice with a lifetime for the current build
		/// </summary>
		static void RegisterDevice (IBuildEngine4 engine, string target, AndroidDevice device)
		{
			var key = GetKey (target);
			engine?.RegisterTaskObjectAssemblyLocal (key, device ?? NullDevice, Lifetime, allowEarlyCollection: false);
		}

		/// <summary>
		/// Gets a cached AndroidDevice cached from the current build
		/// </summary>
		static object GetRegisteredDevice (IBuildEngine4 engine, string target)
		{
			var key = GetKey (target);
			return engine?.GetRegisteredTaskObjectAssemblyLocal (key, Lifetime);
		}

		public static AndroidDevice ParseTarget (string target, TaskLoggingHelper log, bool logErrors = true, IBuildEngine4 engine4 = null) =>
			ParseTarget (target, m => log.LogDebugMessage (m), (c, m) => log.LogCodedError (c, m), logErrors, engine4);

		public static AndroidDevice ParseTarget (string target, Action<string> logMessage, Action<string, string> logError, bool logErrors = true, IBuildEngine4 engine4 = null)
		{
			try {
				var device = GetRegisteredDevice (engine4, target);
				if (device != null) {
					logMessage ("Using cached value from RegisterTaskObject");
					if (device == NullDevice) {
						NoDeviceFound (target, logError, logErrors, engine4);
					}
					return device as AndroidDevice;
				}
				var t = AdbServer.Default.GetDevices ();
				if (string.IsNullOrEmpty (target)) {
					var e = t.Result.FirstOrDefault ();
					if (e != null) {
						// Register for a blank target and -s
						RegisterDevice (engine4, target, e);
						RegisterDevice (engine4, $"-s {e.ID}", e);
						return e;
					} else {
						NoDeviceFound (target, logError, logErrors, engine4);
						return null;
					}
				} else if (target.StartsWith ("-e")) {
					var e = t.Result.Where (x => x.IsEmulator).FirstOrDefault ();
					if (e != null) {
						RegisterDevice (engine4, target, e);
						return e;
					} else {
						NoDeviceFound (target, logError, logErrors, engine4);
						return null;
					}
				} else if (target.StartsWith ("-d")) {
					var e = t.Result.Where (x => !x.IsEmulator).FirstOrDefault ();
					if (e != null) {
						RegisterDevice (engine4, target, e);
						return e;
					} else {
						NoDeviceFound (target, logError, logErrors, engine4);
						return null;
					}
				} else if (target.StartsWith ("-s")) {
					string deviceId = target.Substring (2).Trim ();
					var e = t.Result.Where (x => deviceId == x.ID).FirstOrDefault ();
					if (e != null) {
						RegisterDevice (engine4, target, e);
						return e;
					} else {
						NoDeviceFound (target, logError, logErrors, engine4);
						return null;
					}
				}
				if (logErrors)
					logError (DefaultErrorCode, string.Format (Resources.XA0010_AdbTarget, target));
			} catch (Exception ex) {
				// Register that no device was found for the current build
				RegisterDevice (engine4, target, null);

				if (logErrors) {
					logError (DefaultErrorCode, string.Format (Resources.XA0010_Adb, ex));
				} else {
					logMessage (string.Format (Resources.XA0010_Adb, ex));
				}
			}
			return null;
		}

		static void NoDeviceFound (string target, Action<string, string> logError, bool logErrors, IBuildEngine4 engine4)
		{
			// Register that no device was found for the current build
			RegisterDevice (engine4, target, null);

			if (logErrors) {
				if (string.IsNullOrEmpty (target)) {
					logError (DefaultErrorCode, Resources.XA0010_NoDevice);
				} else {
					logError (DefaultErrorCode, Resources.XA0010_Selected);
				}
			}
		}
	}
}
