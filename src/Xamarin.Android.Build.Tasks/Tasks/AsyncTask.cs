using System;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.Threading;
using System.Collections;

namespace Xamarin.Android.Tasks
{
	public class AsyncTask : Task, ICancelableTask {

		CancellationTokenSource tcs = new CancellationTokenSource ();
		Queue logMessageQueue =new Queue ();
		Queue warningMessageQueue = new Queue ();
		Queue errorMessageQueue = new Queue ();
		ManualResetEvent logDataAvailable = new ManualResetEvent (false);
		ManualResetEvent errorDataAvailable = new ManualResetEvent (false);
		ManualResetEvent warningDataAvailable = new ManualResetEvent (false);
		ManualResetEvent taskCancelled = new ManualResetEvent (false);
		ManualResetEvent completed = new ManualResetEvent (false);
		bool isRunning = true;
		object _eventlock = new object ();
		int UIThreadId = 0;

		private enum WaitHandleIndex
		{
			LogDataAvailable,
			ErrorDataAvailable,
			WarningDataAvailable,
			TaskCancelled,
			Completed,
		}

		public CancellationToken Token { get { return tcs.Token; } }

		public bool YieldDuringToolExecution { get; set; }

		[Obsolete ("Do not use the Log.LogXXXX from within your Async task as it will Lock the Visual Studio UI. Use the this.LogXXXX methods instead.")]
		private new TaskLoggingHelper Log
		{
			get { return base.Log; }
		}

		public AsyncTask ()
		{
			YieldDuringToolExecution = false;
			UIThreadId = Thread.CurrentThread.ManagedThreadId;
		}

		public void Cancel ()
		{
			taskCancelled.Set ();
		}

		public void Complete()
		{
			completed.Set ();
		}

		public void LogDebugTaskItems (string message, string[] items)
		{
			LogDebugMessage (message);

			if (items == null)
				return;

			foreach (var item in items)
				LogDebugMessage ("    {0}", item);
		}

		public void LogDebugTaskItems (string message, ITaskItem[] items)
		{
			LogDebugMessage (message);

			if (items == null)
				return;

			foreach (var item in items)
				LogDebugMessage ("    {0}", item.ItemSpec);
		}

		protected void LogMessage (string message)
		{
			LogMessage (message, importance: MessageImportance.Normal);
		}

		protected void LogMessage (string message, params object[] messageArgs)
		{
			LogMessage (string.Format (message, messageArgs));
		}

		protected void LogDebugMessage (string message)
		{
			LogMessage (message , importance: MessageImportance.Low);
		}

		protected void LogDebugMessage (string message, params object[] messageArgs)
		{
			LogMessage (string.Format (message, messageArgs), importance: MessageImportance.Low);
		}

		protected void LogMessage (string message, MessageImportance importance = MessageImportance.Normal)
		{
			if (UIThreadId == Thread.CurrentThread.ManagedThreadId) {
				#pragma warning disable 618
				Log.LogMessage (importance, message);
				return;
				#pragma warning restore 618
			}

			lock (logMessageQueue.SyncRoot) {
				logMessageQueue.Enqueue (new BuildMessageEventArgs (
					message: message,
					helpKeyword : null,
					senderName: null,
					importance: importance
				));
				lock (_eventlock) {
					if (isRunning)
						logDataAvailable.Set ();
				}
			}
		}

		protected void LogError (string message)
		{
			LogError (code: null, message: message, file: null, lineNumber: 0);
		}

		protected void LogError (string message, params object[] messageArgs)
		{
			LogError (code: null, message: string.Format (message, messageArgs));
		}

		protected void LogCodedError (string code, string message)
		{
			LogError (code: code, message: message, file: null, lineNumber: 0);
		}

		protected void LogCodedError (string code, string message, params object[] messageArgs)
		{
			LogError (code: code, message: string.Format (message, messageArgs), file: null, lineNumber: 0);
		}

		protected void LogError (string code, string message, string file = null, int lineNumber = 0)
		{
			if (UIThreadId == Thread.CurrentThread.ManagedThreadId) {
				#pragma warning disable 618
				Log.LogError (
					subcategory: null,
					errorCode: code,
					helpKeyword: null,
					file: file,
					lineNumber: lineNumber,
					columnNumber: 0,
					endLineNumber: 0,
					endColumnNumber: 0,
					message: message
				);
				return;
				#pragma warning restore 618
			}

			lock (errorMessageQueue.SyncRoot) {
				errorMessageQueue.Enqueue (new BuildErrorEventArgs (
					subcategory: null,
					code: code,
					file: file,
					lineNumber: lineNumber,
					columnNumber: 0,
					endLineNumber: 0,
					endColumnNumber: 0,
					message: message,
					helpKeyword: null,
					senderName: null
				));
				lock (_eventlock) {
					if (isRunning)
						errorDataAvailable.Set ();
				}
			}
		}

		protected void LogWarning (string message, params object[] messageArgs)
		{
			LogWarning (string.Format (message, messageArgs));
		}

		protected void LogWarning (string message)
		{
			if (UIThreadId == Thread.CurrentThread.ManagedThreadId) {
				#pragma warning disable 618
				Log.LogWarning (message);
				return;
				#pragma warning restore 618
			}
			
			lock (warningMessageQueue.SyncRoot) {
				warningMessageQueue.Enqueue (new BuildWarningEventArgs (
					subcategory: null,
					code: null,
					file: null,
					lineNumber: 0,
					columnNumber: 0,
					endLineNumber: 0,
					endColumnNumber: 0,
					message: message,
					helpKeyword: null,
					senderName: null
				));
				lock (_eventlock) {
					if (isRunning)
						warningDataAvailable.Set ();
				}
			}
		}

		public override bool Execute ()
		{
			WaitForCompletion ();
			#pragma warning disable 618
			return !Log.HasLoggedErrors;
			#pragma warning restore 618
		}

		private void LogMessages ()
		{
			lock (logMessageQueue.SyncRoot) {
				while (logMessageQueue.Count > 0) {
					var args = (BuildMessageEventArgs)logMessageQueue.Dequeue ();
					#pragma warning disable 618
					Log.LogMessage (args.Importance, args.Message);
					#pragma warning restore 618
				}
				logDataAvailable.Reset ();
			}
		}

		private void LogErrors ()
		{
			lock (errorMessageQueue.SyncRoot) {
				while (errorMessageQueue.Count > 0) {
					var args = (BuildErrorEventArgs)errorMessageQueue.Dequeue ();
					#pragma warning disable 618
					Log.LogCodedError (args.Code, file: args.File, lineNumber: args.LineNumber, message: args.Message);
					#pragma warning restore 618
				}
				errorDataAvailable.Reset ();
			}
		}

		private void LogWarnings ()
		{
			lock (warningMessageQueue.SyncRoot) {
				while (warningMessageQueue.Count > 0) {
					var args = (BuildWarningEventArgs)warningMessageQueue.Dequeue ();
					#pragma warning disable 618
					Log.LogWarning (args.Message);
					#pragma warning restore 618
				}
				warningDataAvailable.Reset ();
			}
		}

		protected void WaitForCompletion ()
		{
			WaitHandle[] handles = new WaitHandle[] {
				logDataAvailable,
				errorDataAvailable,
				warningDataAvailable,
				taskCancelled,
				completed,
			};
			if (YieldDuringToolExecution && BuildEngine is IBuildEngine3)
				(BuildEngine as IBuildEngine3).Yield ();
			try {
				while (isRunning) {
					var index = (WaitHandleIndex)System.Threading.WaitHandle.WaitAny (handles, TimeSpan.FromMilliseconds (10));
					switch (index) {
					case WaitHandleIndex.LogDataAvailable:
						LogMessages ();
						break;
					case WaitHandleIndex.ErrorDataAvailable: 
						LogErrors ();
						break;
					case WaitHandleIndex.WarningDataAvailable:
						LogWarnings ();
						break;
					case WaitHandleIndex.TaskCancelled:
						tcs.Cancel ();
						isRunning = false;
						break;
					case WaitHandleIndex.Completed:
						isRunning = false;
						break;
					}
				}

			}
			finally {
				if (YieldDuringToolExecution && BuildEngine is IBuildEngine3)
					(BuildEngine as IBuildEngine3).Reacquire ();
			}
		}
	}
}

