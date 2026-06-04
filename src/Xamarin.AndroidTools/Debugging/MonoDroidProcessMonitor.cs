//
// MonoDroidProcessMonitor.cs
//
// Author:
//       Greg Munn <greg.munn@xamarin.com>
//
// Copyright (c) 2015 Xamarin Inc
//

using System;
using System.Threading;
using System.Threading.Tasks;
using Mono.AndroidTools;

namespace Xamarin.AndroidTools.Debugging
{
	/// <summary>
	/// Monitors a running MonoDroid process during a debugging session
	/// 
	/// something will launch the process, 
	/// this will monitor the device for the processes that match the given package name
	/// once we find a process that matches we watch for that process to exit
	/// 
	/// - onCompleted => end debugger session
	/// 
	/// monitors logcat and outputs stdout and error messages
	/// can optionally kill the process when disposed
	/// 
	/// how to test?
	/// - launch task
	/// - get process task
	/// - get logcat task
	/// - kill process
	/// - need to mock / augment device, maybe make the methods virtual on device
	/// - or have an IDevice and we can pass a mock in instead
	/// 
	/// 
	/// </summary>
	public sealed class MonoDroidProcessMonitor : IDisposable
	{
		const int UNASSIGNED_PID = -1;
		public static int RefreshPidInterval = 1000;

		// Common system tags that we may want to ignore
		readonly static string [] excludedLogTags = {
			"dalvikvm",
			"ActivityThread",
			"mkestner",
			"MonoDroid-Debugger"
		};

		readonly ManualResetEvent endHandle = new ManualResetEvent (false);
		readonly object lockObj = new object ();
		readonly CancellationTokenSource cancellationSource;
		readonly IAndroidDevice device;
		readonly string packageName;
		readonly Action<string> onStdOut;
		readonly Action<string> onStdError;
		readonly Func<IAndroidDevice, string, Task> killProcessOnExit;
		readonly Action onCompleted;
	
		/// <summary>
		/// Func to return a task that will reset the debugger timeout, we use this to extend the debugger timeout to give the user
		/// time to launch their app in the case where there is no launchable activity
		/// </summary>
		readonly Func<Task> resetTimeout;

		Task<int> getPidTask;
		Task loggingTask;
		volatile int pid = UNASSIGNED_PID;

		public MonoDroidProcessMonitor (IAndroidDevice device, string packageName, Action<string> stdout, Action<string> stderr,  
			CancellationTokenSource cancellationSource, Func<IAndroidDevice, string, Task> killProcessOnExit, Func<Task> resetTimeout, Action onCompleted)
		{
			if (device == null)
				throw new ArgumentNullException ("device");
			if (packageName == null)
				throw new ArgumentNullException ("packageName");
			if (stdout == null)
				throw new ArgumentNullException ("stdout");
			if (stderr == null)
				throw new ArgumentNullException ("stderr");
			if (cancellationSource == null)
				throw new ArgumentNullException ("cancellationSource");

			this.cancellationSource = cancellationSource;
			this.device = device;
			this.packageName = packageName;
			this.onStdOut = stdout;
			this.onStdError = stderr;
			this.killProcessOnExit = killProcessOnExit;
			this.resetTimeout = resetTimeout;
			this.onCompleted = onCompleted;
		}

		public bool IsStarted { get; private set; }

		public bool IsCompleted { get; private set; }

		/// <summary>
		/// Starts the monitoring process. If processLaunchTask is non-null, waits for the launch task to complete before monitoring
		/// and completes the monitor if the launch failed
		/// </summary>
		public void Start (Task processLaunchTask = null)
		{
			if (this.IsStarted)
				throw new InvalidOperationException ("Already Started");
			
			this.IsStarted = true;
			this.StartMonitoring (processLaunchTask);
		}

		public void Dispose ()
		{
			if (!cancellationSource.IsCancellationRequested)
				cancellationSource.Cancel ();
			cancellationSource.Dispose ();
		}

		public void Cancel ()
		{
			lock (lockObj) {
				if (IsCompleted)
					return;

				// Make sure our tracking operations are finished first
				cancellationSource.Cancel ();

				// Try to kill the activity if we were able to actually get its pid
				if (pid != UNASSIGNED_PID && killProcessOnExit != null) {
					var killTask = killProcessOnExit (device, packageName);
					killTask.ContinueWith (t => {
						if (t.IsFaulted)
							AndroidLogger.LogError ("Failed to kill application", t.Exception.Flatten ().InnerException);
					});
				}
			}

			SetCompleted ();
		}

		/// <summary>
		/// Waits for the monitor to be completed, usually when the process has exited
		/// </summary>
		public void WaitForCompleted ()
		{
			lock (lockObj) {
				if (IsCompleted)
					return;
			}

			endHandle.WaitOne ();
		}

		/// <summary>
		/// Waits for the monitor to be completed, usually when the process has exited
		/// </summary>
		public void WaitForCompleted (int timeout)
		{
			lock (lockObj) {
				if (IsCompleted)
					return;
			}

			endHandle.WaitOne (timeout);
		}

		/// <summary>
		/// Starts the monitoring process. If processLaunchTask is null or completed, starts tracking the process on the device
		/// </summary>
		void StartMonitoring (Task processLaunchTask)
		{
			if (processLaunchTask == null) {
				StartTrackingProcess ();
				return;
			}

			processLaunchTask.ContinueWith (t => {
				if (t.IsFaulted) {
					var ex = t.Exception.Flatten ().InnerException;
					onStdOut (string.Format ("\n Failed to launch app: {0}\n", ex.Message));
					AndroidLogger.LogError ("Failed to launch app", ex);
					SetCompleted ();
				} else if (cancellationSource.IsCancellationRequested || t.IsCanceled) {
					SetCompleted ();
				} else {
					StartTrackingProcess ();
				}
			}, cancellationSource.Token, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
		}

		/// <summary>
		/// Starts tracking the process on the device. We determine the process to track by the package name.
		/// </summary>
		void StartTrackingProcess ()
		{
			if (cancellationSource.IsCancellationRequested)
				return;

			try {
				getPidTask = device.GetProcessId (packageName, cancellationSource.Token);
				if (getPidTask == null)
					throw new InvalidOperationException ("device.GetProcessId returned null, cannot track process");
			} catch (Exception ex) {
				AndroidLogger.LogError ("Failure to get ProcessId task", ex);
				SetCompleted ();
				return;
			}

			getPidTask.ContinueWith (RefreshPid);
		}

		/// <summary>
		/// Refreshs the pid of the process on the device, if the process has not yet started calls StartTrackingProcess again after a timeout.
		/// </summary>
		void RefreshPid (Task<int> processIdTask)
		{
			if (cancellationSource.IsCancellationRequested)
				return;
			
			if (processIdTask.IsFaulted) {
				AndroidLogger.LogError ("Failed to get PID", processIdTask.Exception.Flatten ().InnerException);
				SetCompleted ();
				return;
			}
			if (processIdTask.IsCanceled) {
				return;
			}

			int resultPid = processIdTask.Result;
			if (pid == UNASSIGNED_PID) {
				// Ignore if the activity is still starting, and thus doesn't show up in 'ps'
				if (resultPid > 0) {
					pid = resultPid;
					StartLogTracking (); // track log *after* getting the pid
				}
			} else {
				if (resultPid == 0 || pid != resultPid) {
					SetCompleted ();
					return;
				}
			}

			//carry on polling the PID, so we can detect when the app exits
			StartTrackingAfterTimeout ();
		}

		static Task Delay(double milliseconds, CancellationToken token)
		{
			var tcs = new TaskCompletionSource<bool>();
			System.Timers.Timer timer = new System.Timers.Timer();
			timer.Elapsed+= (obj, args) => tcs.TrySetResult (true);
			timer.Interval = milliseconds;
			timer.AutoReset = false;
			timer.Start();
			return tcs.Task;
		}

		void StartTrackingAfterTimeout ()
		{
			if (cancellationSource.IsCancellationRequested)
				return;
			
			// TODO: use a Task.Delay once we can use .NET 4.5
			var delay = Delay (RefreshPidInterval, cancellationSource.Token);
			delay.ContinueWith ((d) => {
				StartTrackingProcess ();

				// if we are debugging an app that has no start up activity, like a watchface app, then
				// periodically reset the debug timeout so that the user has time to start up the watchface
				if (pid == UNASSIGNED_PID) {
					ResetDebugerTimeout ();
				}
			});
		}

		/// <summary>
		/// Starts monitoring logcat and outputs stdout and stderror
		/// </summary>
		void StartLogTracking ()
		{
			try {
				loggingTask = device.GetLogCat (ProcessLogLine, cancellationSource.Token, excludedLogTags);
				if (loggingTask == null)
					throw new InvalidOperationException ("device.GetLogCat returned null, cannot monitor logcat");
			} catch (Exception ex) {
				AndroidLogger.LogError ("Failure to get LogCat task", ex);
				SetCompleted ();
				return;
			}

			loggingTask.ContinueWith (t => {
				if (t.IsFaulted) {
					AndroidLogger.LogError ("Logcat ended unexpectedly", t.Exception.Flatten ().InnerException);
					SetCompleted ();
				}
			}, cancellationSource.Token, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
		}

		/// <summary>
		/// Resets the debugger output, if the process has not started then bump the debugger timeout to give additional time for it to start
		/// This is used when the user has to manually start up the process (eg. watchface apps)
		/// </summary>
		void ResetDebugerTimeout ()
		{
			if (cancellationSource.IsCancellationRequested)
				return;
			
			if (resetTimeout != null) {
				var timeoutTask = resetTimeout ();
				try {
					timeoutTask.Wait (cancellationSource.Token);
				} 
				catch (AggregateException ex) {
					if (ex.InnerException is OperationCanceledException) {
						// expected, nothing to do
					} else
						throw;
				}
				catch (OperationCanceledException) {
					// expected, nothing to do
				}
				catch (Exception ex) {
					AndroidLogger.LogError ("Failed to reset timeout for debugger", ex);
				}
			}
		}

		/// <summary>
		/// Processes a line from logcat
		/// </summary>
		void ProcessLogLine (AndroidLogCatEntry entry)
		{
			// Disable the time check for now, as we need to use device-only dates
			// We may implement a date retrieval later if needed
			//if (pid != this.pid || time < startTime)
			if (entry.Pid != this.pid)
				return;

			switch (entry.Tag) {
			case "mono-stdout":
			case "stdout":
				onStdOut (entry.Message + "\n");
				break;
			case "mono-stderr":
			case "stderr":
				onStdError (entry.Message + "\n");
				break;
			default:
				onStdOut (string.Format ("[{0}] {1}\n", entry.Tag, entry.Message));
				break;
			}
		}

		void SetCompleted ()
		{
			lock (lockObj) {
				if (IsCompleted)
					return;

				endHandle.Set ();
				IsCompleted = true;
			}

			try {
				if (onCompleted != null)
					onCompleted ();

				cancellationSource.Cancel ();
			} catch (Exception ex) {
				AndroidLogger.LogError ("Unhandled error completing MonoDroidProcess", ex);
			}
		}
	}
}