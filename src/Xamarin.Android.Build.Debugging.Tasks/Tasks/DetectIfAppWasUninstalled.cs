#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.AndroidTools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks {
	public class DetectIfAppWasUninstalled : AndroidTask, ICancelableTask {

		CancellationTokenSource tcs = new CancellationTokenSource ();
		public override string TaskPrefix => "DIAWI";

		public string? AdbTarget { get; set; }

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
			var device = AndroidHelper.ParseTarget (AdbTarget, Log, logErrors: false, engine4: BuildEngine4);
			if (device == null) {
				Log.LogDebugMessage ($"No device found: {nameof (AdbTarget)}=\"{AdbTarget}\"");
				return true;
			}
			Log.LogDebugMessage ($"Found device: {device.ID}");
			var flagFilePath = Path.GetFullPath (UploadFlagFile);
			var task = QueryPackages (device, flagFilePath);
			BuildEngine4.RegisterTaskObjectAssemblyLocal (
				ProjectSpecificTaskObjectKey (GetPackagesAsyncKey),
				task,
				RegisteredTaskObjectLifetime.Build,
				allowEarlyCollection: false);
			return !Log.HasLoggedErrors;
		}


		async System.Threading.Tasks.Task<List<AndroidInstalledPackage>?> QueryPackages (AndroidDevice device, string uploadFlagFileFullPath)
		{
			// DO NOT use the Log.XXXX methods in this method.
			// Because this is running on a background thread they will
			// end up locking the UI in VS.
			try {
				var pmPackages = new PmListPackagesCommand () {
					RequireVersions = false,
					User = UserID,
				};
				var packages = await device.GetPackages (pmPackages, tcs.Token);
				if (!packages.Any (x => string.Compare (x.Name, PackageName, StringComparison.OrdinalIgnoreCase) == 0)) {
					File.Delete (uploadFlagFileFullPath);
				}
				return packages;
			} catch (Exception ex) {
				System.Diagnostics.Debug.WriteLine ($"DetectIfAppWasUninstalled failed with {ex}");
			}
			return null;
		}
	}
}
