using System;
using Microsoft.Build.Utilities;

namespace Microsoft.Android.Build.Tasks;

// Each concrete task must also declare MSBuildMultiThreadableTaskAttribute.
// Unlike AndroidToolTask, this base does not capture the process working directory.
// ToolTask implements IMultiThreadableTask and starts the tool with TaskEnvironment.
public abstract class MultithreadedAndroidToolTask : ToolTask
{
	public abstract string TaskPrefix { get; }

	public override bool Execute ()
	{
		try {
			return RunTask ();
		} catch (Exception ex) {
			Log.LogUnhandledException (TaskPrefix, ex, TaskEnvironment);
			return false;
		}
	}

	public virtual bool RunTask () => base.Execute ();
}