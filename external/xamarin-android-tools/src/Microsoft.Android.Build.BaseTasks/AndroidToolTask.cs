// https://github.com/xamarin/xamarin-android/blob/9fca138604c53989e1cff7fc0c2e939583b4da28/src/Xamarin.Android.Build.Tasks/Tasks/AndroidTask.cs#L75

using System;
using Microsoft.Build.Utilities;

namespace Microsoft.Android.Build.Tasks
{
	public abstract class AndroidToolTask : ToolTask
	{
		public abstract string TaskPrefix { get; }

		public override bool Execute ()
		{
			try {
				return RunTask ();
			} catch (Exception ex) {
				Log.LogUnhandledException (TaskPrefix, ex);
				return false;
			}
		}

		// Most ToolTask's do not override Execute and
		// just expect the base to be called
		public virtual bool RunTask () => base.Execute ();
	}
}
