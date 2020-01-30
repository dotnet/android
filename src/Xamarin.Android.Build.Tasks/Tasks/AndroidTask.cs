using System;
using Microsoft.Build.Utilities;
using Xamarin.Build;
using static System.Threading.Tasks.TaskExtensions;

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
		/// <summary>
		/// A helper for non-async overrides of RunTaskAsync, etc.
		/// </summary>
		public static readonly System.Threading.Tasks.Task Done =
			System.Threading.Tasks.Task.FromResult (true);

		public abstract string TaskPrefix { get; }

		[Obsolete ("You should not use the 'Log' property directly for AsyncTask. Use the 'Log*' methods instead.", error: true)]
		public new TaskLoggingHelper Log {
			get => base.Log;
		}

		public override bool Execute ()
		{
			try {
				return RunTask ();
			} catch (Exception ex) {
				this.LogUnhandledException (TaskPrefix, ex);
				return false;
			}
		}

		public virtual bool RunTask ()
		{
			Yield ();
			try {
				this.RunTask (() => RunTaskAsync ())
					.Unwrap ()
					.ContinueWith (Complete);

				// This blocks on AsyncTask.Execute, until Complete is called
				return base.Execute ();
			} finally {
				Reacquire ();
			}
		}

		/// <summary>
		/// Override this method for simplicity of AsyncTask usage:
		/// * Yield / Reacquire is handled for you
		/// * RunTaskAsync is already on a background thread
		/// </summary>
		public virtual System.Threading.Tasks.Task RunTaskAsync () => Done;
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
