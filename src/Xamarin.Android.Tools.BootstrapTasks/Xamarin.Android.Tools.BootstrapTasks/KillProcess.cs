using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Xamarin.Android.BuildTools.PrepTasks;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	public class KillProcess : Task
	{
		[Required]
		public                  int             ProcessId       {get; set;}

		public override bool Execute()
		{
			Log.LogMessage(MessageImportance.Low, $"Task {nameof(KillProcess)}");
			Log.LogMessage(MessageImportance.Low, $"  {nameof (ProcessId)}: {ProcessId}");

			using (var p = Process.GetProcessById (ProcessId)) {
				p.Kill ();
			}

			return !Log.HasLoggedErrors;
		}
	}
}
