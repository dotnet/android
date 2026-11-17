using System.Diagnostics;
using Microsoft.Build.Framework;
using Task = Microsoft.Build.Utilities.Task;

namespace Xamarin.Android.Tools.BootstrapTasks;

public class KillProcess : Task
{
	[Required]
	public                  int             ProcessId       {get; set;}

	public override bool Execute()
	{
		Log.LogMessage(MessageImportance.Low, $"Task {nameof(KillProcess)}");
		Log.LogMessage(MessageImportance.Low, $"  {nameof (ProcessId)}: {ProcessId}");

		using (var p = Process.GetProcessById (ProcessId)) {
			if (!p.HasExited)
				p.Kill ();
		}

		return !Log.HasLoggedErrors;
	}
}
