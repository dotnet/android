#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks {
	public class WaitForAppDetection : AsyncTask
	{
		public override string TaskPrefix => "WFAD";

		System.Threading.Tasks.Task<Exception?>? getPackagesAsync;

		public override bool Execute ()
		{
			var key =  ProjectSpecificTaskObjectKey (DetectIfAppWasUninstalled.GetPackagesAsyncKey);
			getPackagesAsync = BuildEngine4.GetRegisteredTaskObjectAssemblyLocal<System.Threading.Tasks.Task<Exception?>> (key, RegisteredTaskObjectLifetime.Build);
			return base.Execute ();
		}

		public async override System.Threading.Tasks.Task RunTaskAsync ()
		{
			LogDebugMessage ("Waiting for DetectIfAppWasUninstalled...");
			if (getPackagesAsync == null)
				return;
			var error = await getPackagesAsync;
			if (error != null)
				LogDebugMessage ($"DetectIfAppWasUninstalled failed with {error}");
			LogDebugMessage ("DetectIfAppWasUninstalled Completed.");
			return;
		}
	}
}