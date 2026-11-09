#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks {
	[MSBuildMultiThreadableTask]
	public class DetectIfAppWasUninstalled : MultithreadedAndroidTask, ICancelableTask {

		readonly CancellationTokenSource tcs = new CancellationTokenSource ();
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

		public override bool RunTask ()
		{
			var device = AndroidHelper.ParseTarget (AdbTarget, Log, logErrors: false, engine4: BuildEngine4,
				adbToolPath: AdbToolPath, adbToolExe: AdbToolExe, taskEnvironment: TaskEnvironment, cancellationToken: Token);
			if (device == null) {
				Log.LogDebugMessage ($"No device found: {nameof (AdbTarget)}=\"{AdbTarget}\"");
				return true;
			}
			Log.LogDebugMessage ($"Found device: {device.Serial}");
			var flagFilePath = TaskEnvironment.GetAbsolutePath (UploadFlagFile);
			var runner = AndroidHelper.CreateAdbRunner (AdbToolPath, AdbToolExe, TaskEnvironment);
			var task = QueryPackages (runner, device.Serial, flagFilePath);
			BuildEngine4.RegisterTaskObjectAssemblyLocal (
				(GetPackagesAsyncKey, TaskEnvironment.ProjectDirectory),
				task,
				RegisteredTaskObjectLifetime.Build,
				allowEarlyCollection: false);
			return !Log.HasLoggedErrors;
		}

		async System.Threading.Tasks.Task<Exception?> QueryPackages (AdbRunner runner, string serial, AbsolutePath uploadFlagFile)
		{
			// Log from the waiter; this query can outlive Execute.
			try {
				var args = new List<string> { "list", "packages" };
				if (UserID is { Length: > 0 } userId) {
					args.Add ("--user");
					args.Add (userId);
				}
				var packages = await runner.ExecuteShellCommandAsync (serial, "pm", args.ToArray (), tcs.Token).ConfigureAwait (false);
				if (!packages.Split (new [] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
					.Any (line => string.Equals (line.Trim (), $"package:{PackageName}", StringComparison.OrdinalIgnoreCase)))
					File.Delete (uploadFlagFile);
				return null;
			} catch (Exception ex) when (ex is not OperationCanceledException) {
				return ex;
			}
		}
	}
}
