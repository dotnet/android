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
			public string Message;
		}

		BlockingCollection<LogMessage> lineQueue = new BlockingCollection<LogMessage> (new ConcurrentQueue<LogMessage> ());
		Task loggerTask;
		CancellationTokenSource cts;
		CancellationToken token;

		void InitOS ()
		{
			cts = new CancellationTokenSource ();
			token = cts.Token;
			loggerTask = Task.Run (() => LogWorker (), token);
		}

		void ShutdownOS ()
		{
			try {
				cts.Cancel ();
				loggerTask.Wait (500);
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
			while (!token.IsCancellationRequested) {
				LogMessage message = lineQueue.Take ();
				if (message == null)
					continue;

				if (message.IsLine) {
					Console.WriteLine (message.Message);
				} else {
					Console.Write (message.Message);
				}
			}
		}
	}
}
