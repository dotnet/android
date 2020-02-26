using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xamarin.Build;

namespace Xamarin.Android.Tasks
{
	static class AsyncTaskExtensions
	{
		/// <summary>
		/// Creates a collection of Task with proper CancellationToken and error handling and waits via Task.WhenAll
		/// </summary>
		public static Task WhenAll<TSource>(this AsyncTask asyncTask, IEnumerable<TSource> source, Action<TSource> body)
		{
			var tasks = new List<Task> ();
			foreach (var s in source) {
				tasks.Add (Task.Run (() => {
					try {
						body (s);
					} catch (Exception exc) {
						LogErrorAndCancel (asyncTask, exc);
					}
				}, asyncTask.CancellationToken));
			}
			return Task.WhenAll (tasks);
		}

		/// <summary>
		/// Creates a collection of Task with proper CancellationToken and error handling and waits via Task.WhenAll
		/// Passes an object the inner method can use for locking. The callback is of the form: (T item, object lockObject)
		/// </summary>
		public static Task WhenAllWithLock<TSource> (this AsyncTask asyncTask, IEnumerable<TSource> source, Action<TSource, object> body)
		{
			var lockObject = new object ();
			var tasks = new List<Task> ();
			foreach (var s in source) {
				tasks.Add (Task.Run (() => {
					try {
						body (s, lockObject);
					} catch (Exception exc) {
						LogErrorAndCancel (asyncTask, exc);
					}
				}, asyncTask.CancellationToken));
			}
			return Task.WhenAll (tasks);
		}

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
			asyncTask.LogCodedError ("XA0000", Properties.Resources.XA0000_Exception, exc);
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
