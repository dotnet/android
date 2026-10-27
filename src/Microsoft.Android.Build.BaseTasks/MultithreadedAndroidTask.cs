using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Android.Build.Tasks
{
	// Each concrete task must also declare MSBuildMultiThreadableTaskAttribute.
	// Unlike AndroidTask, this base does not capture the process working directory.
	public abstract class MultithreadedAndroidTask : Task, IMultiThreadableTask
	{
		public abstract string TaskPrefix { get; }

		public TaskEnvironment TaskEnvironment { get; set; } = TaskEnvironment.Fallback;

		public override bool Execute ()
		{
			try {
				return RunTask ();
			} catch (Exception ex) {
				Log.LogUnhandledException (TaskPrefix, ex, TaskEnvironment);
				return false;
			}
		}

		public abstract bool RunTask ();
	}
}
