using System;
using Microsoft.Build.Utilities;
using Xamarin.Build;

namespace Xamarin.Android.Tasks
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

	public abstract class AndroidAsyncTask : AsyncTask
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

		public virtual bool RunTask () => base.Execute ();
	}

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
