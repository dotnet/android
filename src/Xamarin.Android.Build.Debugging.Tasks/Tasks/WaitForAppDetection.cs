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
			var owner = BuildEngine4.GetRegisteredTaskObjectAssemblyLocal<DetectIfAppWasUninstalled.PackageQuery> (
				(DetectIfAppWasUninstalled.QueryOwnerKey, TaskEnvironment.ProjectDirectory), RegisteredTaskObjectLifetime.Build);
			using var registration = cancellation.Token.Register (() => owner?.Cancel ());
			try {
				// Legacy registrations contain only a Task; owned queries must also finish cleanup.
				var error = (owner == null ? getPackagesAsync.WaitAsync (cancellation.Token) : getPackagesAsync)
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