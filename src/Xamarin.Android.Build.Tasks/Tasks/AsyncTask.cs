using System;
using System.IO;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.Threading;
using System.Collections;

namespace Xamarin.Android.Tasks
{
	public class AsyncTask : CancelableTask {

		Queue logMessageQueue =new Queue ();
		Queue warningMessageQueue = new Queue ();
		Queue errorMessageQueue = new Queue ();
		Queue customMessageQueue = new Queue ();
		ManualResetEvent logDataAvailable = new ManualResetEvent (false);
		ManualResetEvent errorDataAvailable = new ManualResetEvent (false);
		ManualResetEvent warningDataAvailable = new ManualResetEvent (false);
		ManualResetEvent customDataAvailable = new ManualResetEvent (false);
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
			CustomDataAvailable,
			TaskCancelled,
			Completed,
		}

		protected struct OutputLine {
			public string Line;
			public bool StdError;

			public OutputLine (string line, bool stdError)
			{
				Line = line;
				StdError = stdError;
			}
		}

		public bool YieldDuringToolExecution { get; set; }

		protected string WorkingDirectory { get; private set; } 

		[Obsolete ("Do not use the Log.LogXXXX from within your Async task as it will Lock the Visual Studio UI. Use the this.LogXXXX methods instead.")]
		private new TaskLoggingHelper Log
		{
			get { return base.Log; }
		}

		public AsyncTask ()
		{
			YieldDuringToolExecution = false;
			UIThreadId = Thread.CurrentThread.ManagedThreadId;
			WorkingDirectory = Directory.GetCurrentDirectory ();
		}

		public override void Cancel ()
		{
			taskCancelled.Set ();
		}

		protected void Complete (System.Threading.Tasks.Task task)
		{
			if (task.Exception != null) {
				var ex = task.Exception.GetBaseException ();
				LogError (ex.Message + Environment.NewLine + ex.StackTrace);
			}
			Complete ();
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

		public void LogMessage (string message)
		{
			LogMessage (message, importance: MessageImportance.Normal);
		}

		public void LogMessage (string message, params object[] messageArgs)
		{
			LogMessage (string.Format (message, messageArgs));
		}

		public void LogDebugMessage (string message)
		{
			LogMessage (message , importance: MessageImportance.Low);
		}

		public void LogDebugMessage (string message, params object[] messageArgs)
		{
			LogMessage (string.Format (message, messageArgs), importance: MessageImportance.Low);
		}

		public void LogMessage (string message, MessageImportance importance = MessageImportance.Normal)
		{
			if (UIThreadId == Thread.CurrentThread.ManagedThreadId) {
				#pragma warning disable 618
				Log.LogMessage (importance, message);
				return;
				#pragma warning restore 618
			}

			var data = new BuildMessageEventArgs (
					message: message,
					helpKeyword: null,
					senderName: null,
					importance: importance
			);
			EnqueueMessage (logMessageQueue, data, logDataAvailable);
		}

		public void LogError (string message)
		{
			LogCodedError (code: null, message: message, file: null, lineNumber: 0);
		}

		public void LogError (string message, params object [] messageArgs)
		{
			LogCodedError (code: null, message: string.Format (message, messageArgs));
		}

		public void LogCodedError (string code, string message)
		{
			LogCodedError (code: code, message: message, file: null, lineNumber: 0);
		}

		public void LogCodedError (string code, string message, params object [] messageArgs)
		{
			LogCodedError (code: code, message: string.Format (message, messageArgs), file: null, lineNumber: 0);
		}

		public void LogCodedError (string code, string file, int lineNumber, string message, params object [] messageArgs)
		{
			LogCodedError (code: code, message: string.Format (message, messageArgs), file: file, lineNumber: lineNumber);
		}

		public void LogCodedError (string code, string message, string file, int lineNumber)
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

			var data = new BuildErrorEventArgs (
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
			);
			EnqueueMessage (errorMessageQueue, data, errorDataAvailable);
		}

		public void LogWarning (string message)
		{
			LogCodedWarning (code: null, message: message, file: null, lineNumber: 0);
		}

		public void LogWarning (string message, params object[] messageArgs)
		{
			LogCodedWarning (code: null, message: string.Format (message, messageArgs));
		}

		public void LogCodedWarning (string code, string message)
		{
			LogCodedWarning (code: code, message: message, file: null, lineNumber: 0);
		}

		public void LogCodedWarning (string code, string message, params object [] messageArgs)
		{
			LogCodedWarning (code: code, message: string.Format (message, messageArgs), file: null, lineNumber: 0);
		}

		public void LogCodedWarning (string code, string file, int lineNumber, string message, params object [] messageArgs)
		{
			LogCodedWarning (code: code, message: string.Format (message, messageArgs), file: file, lineNumber: lineNumber);
		}

		public void LogCodedWarning (string code, string message, string file, int lineNumber)
		{
			if (UIThreadId == Thread.CurrentThread.ManagedThreadId) {
				#pragma warning disable 618
				Log.LogWarning (
					subcategory: null,
					warningCode: code,
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
			var data = new BuildWarningEventArgs (
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
			);
			EnqueueMessage (warningMessageQueue, data, warningDataAvailable);
		}

		public void LogCustomBuildEvent (CustomBuildEventArgs e)
		{
			if (UIThreadId == Thread.CurrentThread.ManagedThreadId) {
				#pragma warning disable 618
				BuildEngine.LogCustomEvent (e);
				return;
				#pragma warning restore 618
			}
			EnqueueMessage (customMessageQueue, e, customDataAvailable);
		}

		public override bool Execute ()
		{
			WaitForCompletion ();
			#pragma warning disable 618
			return !Log.HasLoggedErrors;
			#pragma warning restore 618
		}

		private void EnqueueMessage (Queue queue, object item, ManualResetEvent resetEvent)
		{
			lock (queue.SyncRoot) {
				queue.Enqueue (item);
				lock (_eventlock) {
					if (isRunning)
						resetEvent.Set ();
				}
			}
		}

		private void LogInternal<T> (Queue queue, Action<T> action, ManualResetEvent resetEvent)
		{
			lock (queue.SyncRoot) {
				while (queue.Count > 0) {
					var args = (T)queue.Dequeue ();
					action (args);
				}
				resetEvent.Reset ();
			}
		}

		protected void Yield ()
		{
			if (YieldDuringToolExecution && BuildEngine is IBuildEngine3)
				((IBuildEngine3)BuildEngine).Yield ();
		}

		protected void Reacquire ()
		{
			if (YieldDuringToolExecution && BuildEngine is IBuildEngine3)
				((IBuildEngine3)BuildEngine).Reacquire ();
		}

		protected void WaitForCompletion ()
		{
			WaitHandle[] handles = new WaitHandle[] {
				logDataAvailable,
				errorDataAvailable,
				warningDataAvailable,
				customDataAvailable,
				taskCancelled,
				completed,
			};
			try {
				while (isRunning) {
					var index = (WaitHandleIndex)System.Threading.WaitHandle.WaitAny (handles, TimeSpan.FromMilliseconds (10));
					switch (index) {
					case WaitHandleIndex.LogDataAvailable:
						LogInternal<BuildMessageEventArgs> (logMessageQueue, (e) => {
							#pragma warning disable 618
							Log.LogMessage (e.Importance, e.Message);
							#pragma warning restore 618
						}, logDataAvailable);
						break;
					case WaitHandleIndex.ErrorDataAvailable:
						LogInternal<BuildErrorEventArgs> (errorMessageQueue, (e) => {
							#pragma warning disable 618
							Log.LogCodedError (e.Code, file: e.File, lineNumber: e.LineNumber, message: e.Message);
							#pragma warning restore 618
						}, errorDataAvailable); 
						break;
					case WaitHandleIndex.WarningDataAvailable:
						LogInternal<BuildWarningEventArgs> (warningMessageQueue, (e) => {
							#pragma warning disable 618
							Log.LogCodedWarning (e.Code, file: e.File, lineNumber: e.LineNumber, message: e.Message);
							#pragma warning restore 618
						}, warningDataAvailable);
						break;
					case WaitHandleIndex.CustomDataAvailable:
						LogInternal<CustomBuildEventArgs> (customMessageQueue, (e) => {
							BuildEngine.LogCustomEvent (e);
						}, customDataAvailable);
						break;
					case WaitHandleIndex.TaskCancelled:
						base.Cancel ();
						isRunning = false;
						break;
					case WaitHandleIndex.Completed:
						isRunning = false;
						break;
					}
				}

			}
			finally {

			}
		}
	}
}

