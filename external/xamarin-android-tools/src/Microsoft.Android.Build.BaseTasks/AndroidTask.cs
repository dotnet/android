// https://github.com/xamarin/xamarin-android/blob/9fca138604c53989e1cff7fc0c2e939583b4da28/src/Xamarin.Android.Build.Tasks/Tasks/AndroidTask.cs#L10

using System;
using Microsoft.Build.Utilities;

namespace Microsoft.Android.Build.Tasks
{
	// We use this task to ensure that no unhandled exceptions
	// escape our tasks which would cause an MSB4018
	public abstract class AndroidTask : Task
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

		public abstract bool RunTask ();
	}
}
