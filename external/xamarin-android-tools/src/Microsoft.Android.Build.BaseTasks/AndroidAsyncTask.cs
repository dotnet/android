// https://github.com/xamarin/xamarin-android/blob/9fca138604c53989e1cff7fc0c2e939583b4da28/src/Xamarin.Android.Build.Tasks/Tasks/AndroidTask.cs#L27

using System;
using Xamarin.Build;
using static System.Threading.Tasks.TaskExtensions;

namespace Microsoft.Android.Build.Tasks
{
	public abstract class AndroidAsyncTask : AsyncTask
	{
		public abstract string TaskPrefix { get; }

		public override bool Execute ()
		{
			try {
				return RunTask ();
			} catch (Exception ex) {
				this.LogUnhandledException (TaskPrefix, ex);
				return false;
			}
		}

		/// <summary>
		/// Typically `RunTaskAsync` will be the preferred method to override,
		///  however this method can be overridden instead for Tasks that will
		///  run quickly and do not need to be asynchronous.
		/// </summary>
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
		public virtual System.Threading.Tasks.Task RunTaskAsync () => System.Threading.Tasks.Task.CompletedTask;

		protected object ProjectSpecificTaskObjectKey (object key) => (key, WorkingDirectory);
	}
}
