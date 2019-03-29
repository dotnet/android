using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xamarin.Build;

namespace Xamarin.Android.Tasks
{
	static class AsyncTaskExtensions
	{
		/// <summary>
		/// Calls Parallel.ForEach() with appropriate ParallelOptions and exception handling.
		/// </summary>
		public static ParallelLoopResult ParallelForEach<TSource> (this AsyncTask asyncTask, IEnumerable<TSource> source, Action<TSource> body)
		{
			var options = ParallelOptions (asyncTask);
			return Parallel.ForEach (source, options, s => {
				try {
					body (s);
				} catch (Exception exc) {
					LogErrorAndCancel (asyncTask, exc);
				}
			});
		}

		/// <summary>
		/// Calls Parallel.ForEach() with appropriate ParallelOptions and exception handling.
		/// Passes an object the inner method can use for locking. The callback is of the form: (T item, object lockObject)
		/// </summary>
		public static ParallelLoopResult ParallelForEachWithLock<TSource> (this AsyncTask asyncTask, IEnumerable<TSource> source, Action<TSource, object> body)
		{
			var options = ParallelOptions (asyncTask);
			var lockObject = new object ();
			return Parallel.ForEach (source, options, s => {
				try {
					body (s, lockObject);
				} catch (Exception exc) {
					LogErrorAndCancel (asyncTask, exc);
				}
			});
		}

		static ParallelOptions ParallelOptions (AsyncTask asyncTask) => new ParallelOptions {
			CancellationToken = asyncTask.CancellationToken,
			TaskScheduler = TaskScheduler.Default,
		};

		static void LogErrorAndCancel (AsyncTask asyncTask, Exception exc)
		{
			asyncTask.LogCodedError ("XA0000", "Unhandled exception: {0}", exc);
			asyncTask.Cancel ();
		}

		/// <summary>
		/// Calls Task.Run() with a proper CancellationToken.
		/// </summary>
		public static Task RunTask (this AsyncTask asyncTask, Action body) => 
			Task.Run (body, asyncTask.CancellationToken);


		/// <summary>
		/// Calls Task.Run<T>() with a proper CancellationToken.
		/// </summary>
		public static Task<TSource> RunTask<TSource> (this AsyncTask asyncTask, Func<TSource> body) => 
			Task.Run (body, asyncTask.CancellationToken);
	}
}
