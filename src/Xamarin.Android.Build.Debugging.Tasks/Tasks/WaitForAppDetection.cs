#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks {
	[MSBuildMultiThreadableTask]
	public class WaitForAppDetection : MultithreadedAndroidTask, ICancelableTask
	{
		public override string TaskPrefix => "WFAD";

		readonly CancellationTokenSource cancellation = new CancellationTokenSource ();

		public void Cancel () => cancellation.Cancel ();

		public override bool RunTask ()
		{
			var key = (DetectIfAppWasUninstalled.GetPackagesAsyncKey, TaskEnvironment.ProjectDirectory);
			var getPackagesAsync = BuildEngine4.GetRegisteredTaskObjectAssemblyLocal<Task<Exception?>> (key, RegisteredTaskObjectLifetime.Build);
			Log.LogDebugMessage ("Waiting for DetectIfAppWasUninstalled...");
			if (getPackagesAsync == null)
				return !Log.HasLoggedErrors;
			try {
				// Cancel only the wait, not the adb query.
				var error = getPackagesAsync.ContinueWith (task => task.GetAwaiter ().GetResult (),
					cancellation.Token, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default)
					.GetAwaiter ().GetResult ();
				if (error != null)
					Log.LogDebugMessage ($"DetectIfAppWasUninstalled failed with {error}");
				Log.LogDebugMessage ("DetectIfAppWasUninstalled Completed.");
			} catch (OperationCanceledException) {
				// Cancellation is not a build error.
			}
			return !Log.HasLoggedErrors;
		}
	}
}