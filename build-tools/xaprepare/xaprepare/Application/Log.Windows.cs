using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	partial class Log
	{
		sealed class LogMessage
		{
			public bool IsLine;
			public string Message = String.Empty;
		}

		BlockingCollection<LogMessage> lineQueue = new BlockingCollection<LogMessage> (new ConcurrentQueue<LogMessage> ());
		Task? loggerTask;
		CancellationTokenSource cts = new CancellationTokenSource ();

		void InitOS ()
		{
			loggerTask = Task.Run (() => LogWorker (), cts.Token);
		}

		void ShutdownOS ()
		{
			try {
				cts.Cancel ();
				loggerTask?.Wait (500);
			} catch (AggregateException ex) {
				if (ex.InnerException is TaskCanceledException)
					return;
				throw;
			}
		}

		void DoConsoleWrite (string message)
		{
			lineQueue.Add (new LogMessage { IsLine = false, Message = message });
		}

		void DoConsoleWriteLine (string message)
		{
			lineQueue.Add (new LogMessage { IsLine = true, Message = message });
		}

		void LogWorker ()
		{
			while (!cts.Token.IsCancellationRequested) {
				LogMessage message = lineQueue.Take ();

				if (message.IsLine) {
					Console.WriteLine (message.Message);
				} else {
					Console.Write (message.Message);
				}
			}
		}
	}
}
