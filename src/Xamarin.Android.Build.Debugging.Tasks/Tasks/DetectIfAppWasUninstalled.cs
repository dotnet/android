#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks {
	public class DetectIfAppWasUninstalled : AndroidTask, ICancelableTask {

		CancellationTokenSource tcs = new CancellationTokenSource ();
		public override string TaskPrefix => "DIAWI";

		public string? AdbTarget { get; set; }
		public string? AdbToolPath { get; set; }
		public string? AdbToolExe { get; set; }

		[Required]
		public string PackageName { get; set; } = "";

		[Required]
		public string UploadFlagFile { get; set; } = "";

		public string? UserID { get; set; }

		public CancellationToken Token { get { return tcs.Token; } }

		internal const string GetPackagesAsyncKey = nameof (DetectIfAppWasUninstalled) + ".QueryPackages";

		public void Cancel ()
		{
			tcs.Cancel ();
		}

		public override bool RunTask()
		{
			// kick off a background task to check the device via adb.
			// and exit the task immediately. The background task will
			// continue to run. We need to get the device on the main
			// thread here, otherwise GetRegisteredTaskObject returns
			// null.
			var device = AndroidHelper.ParseTarget (AdbTarget, Log, logErrors: false, engine4: BuildEngine4, adbToolPath: AdbToolPath, adbToolExe: AdbToolExe);
			if (device == null) {
				Log.LogDebugMessage ($"No device found: {nameof (AdbTarget)}=\"{AdbTarget}\"");
				return true;
			}
			Log.LogDebugMessage ($"Found device: {device.Serial}");
			var flagFilePath = Path.GetFullPath (UploadFlagFile);
			var task = QueryPackages (device.Serial, flagFilePath);
			BuildEngine4.RegisterTaskObjectAssemblyLocal (
				ProjectSpecificTaskObjectKey (GetPackagesAsyncKey),
				task,
				RegisteredTaskObjectLifetime.Build,
				allowEarlyCollection: false);
			return !Log.HasLoggedErrors;
		}


		async System.Threading.Tasks.Task<Exception?> QueryPackages (string serial, string uploadFlagFileFullPath)
		{
			// DO NOT use the Log.XXXX methods in this method.
			// Because this is running on a background thread they will
			// end up locking the UI in VS.
			try {
				var args = new List<string> { "list", "packages" };
				if (UserID is { Length: > 0 } userId) {
					args.Add ("--user");
					args.Add (userId);
				}
				var packages = await AndroidHelper.CreateAdbRunner (AdbToolPath, AdbToolExe)
					.ExecuteShellCommandAsync (serial, "pm", args.ToArray (), tcs.Token);
				if (!packages.Split (new [] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
					.Any (line => string.Equals (line.Trim (), $"package:{PackageName}", StringComparison.OrdinalIgnoreCase)))
					File.Delete (uploadFlagFileFullPath);
				return null;
			} catch (Exception ex) when (ex is not OperationCanceledException) {
				return ex;
			}
		}
	}
}
