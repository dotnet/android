#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using Microsoft.Build.Framework;
using TPL = System.Threading.Tasks;
using Xamarin.Android.Tools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	internal class Aapt2Daemon : IDisposable
	{
		static readonly string TypeFullName = typeof (Aapt2Daemon).FullName ?? throw new InvalidOperationException ("AAPT2 daemon type name is unavailable.");
		static readonly TimeSpan OutputFileTimeout = TimeSpan.FromSeconds (30);
		static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds (5);

		internal static object RegisterTaskObjectKey => TypeFullName;

		public static Aapt2Daemon GetInstance (IBuildEngine4 engine, Action<string> log, string aapt2, int numberOfInstances, int initalNumberOfDaemons, bool registerInDomain = false)
		{
			var area = registerInDomain ? RegisteredTaskObjectLifetime.AppDomain : RegisteredTaskObjectLifetime.Build;
			var daemon = engine.GetRegisteredTaskObjectAssemblyLocal<Aapt2Daemon> (RegisterTaskObjectKey, area);
			if (daemon != null && daemon.IsStopped) {
				daemon.Dispose ();
				engine.UnregisterTaskObjectAssemblyLocal (RegisterTaskObjectKey, area);
				daemon = null;
			}
			if (daemon == null)
			{
				daemon = new Aapt2Daemon (aapt2, numberOfInstances, initalNumberOfDaemons, log);
				engine.RegisterTaskObjectAssemblyLocal (RegisterTaskObjectKey, daemon, area, allowEarlyCollection: false);
			}
			return daemon;
		}

		public class Job
		{
			TPL.TaskCompletionSource<bool> tcs = new TPL.TaskCompletionSource<bool> (TPL.TaskCreationOptions.RunContinuationsAsynchronously);
			List<OutputLine> output = new List<OutputLine> ();
			public string[] Commands { get; private set; }
			public long JobId { get; private set; }
			public string OutputFile { get; private set; }
			public bool Succeeded { get; set; }
			public CancellationToken Token { get; private set; }
			public TPL.Task Task => tcs.Task;
			public IList<OutputLine> Output => output;
			public Job (string[] commands, long jobId, string outputFile, CancellationToken token)
			{
				Commands = commands;
				JobId = jobId;
				OutputFile = outputFile;
				Token = token;
			}

			public void Complete (bool errored)
			{
				Succeeded = !errored;
				tcs.TrySetResult (!errored);
			}
		}

		readonly object gate = new object ();
		readonly Channel<Job> pendingJobs = Channel.CreateUnbounded<Job> ();
		readonly ConcurrentDictionary<long, Job> jobs = new ConcurrentDictionary<long, Job> ();
		readonly CancellationTokenSource cancellation = new CancellationTokenSource ();
		readonly ConcurrentBag<TPL.Task> daemons = new ConcurrentBag<TPL.Task> ();

		long jobsRunning = 0;
		long jobId = 0;
		readonly int maxInstances;
		readonly Action<string> logger;
		Exception? failure;
		bool stopped;
		bool disposed;

		public CancellationToken Token => cancellation.Token;

		public bool JobsInQueue => pendingJobs.Reader.Count > 0;

		bool IsStopped {
			get {
				lock (gate)
					return stopped;
			}
		}

		public bool JobsRunning
		{
			get
			{
				return Interlocked.Read (ref jobsRunning) > 0;
			}
		}
		public string Aapt2 { get; private set; }

		public string ToolName { get { return Path.GetFileName (Aapt2); } }

		public int MaxInstances => maxInstances;

		public int CurrentInstances => daemons.Count;

		public Aapt2Daemon (string aapt2, int maxNumberOfInstances, int initalNumberOfDaemons, Action<string> log)
		{
			ArgumentOutOfRangeException.ThrowIfNegativeOrZero (maxNumberOfInstances);
			Aapt2 = aapt2;
			maxInstances = maxNumberOfInstances;
			logger = log;
			for (int i = 0; i < initalNumberOfDaemons; i++) {
				SpawnAapt2Daemon ();
			}
		}

		void SpawnAapt2Daemon ()
		{
			lock (gate) {
				if (stopped || daemons.Count >= maxInstances)
					return;
				daemons.Add (TPL.Task.Run (Aapt2DaemonStart));
			}
		}

		public void Dispose ()
		{
			TPL.Task [] workers;
			lock (gate) {
				if (disposed)
					return;
				disposed = true;
				Stop ();
				workers = daemons.ToArray ();
			}
			cancellation.Cancel ();
			TPL.Task.WhenAll (workers).WaitAsync (ShutdownTimeout * 3).GetAwaiter ().GetResult ();
			cancellation.Dispose ();
		}

		public long QueueCommand (string[] job, string outputFile, CancellationToken token)
		{
			lock (gate) {
				long id = Interlocked.Add (ref jobId, 1);
				var j = new Job (job, id, outputFile, token);
				jobs [j.JobId] = j;
				if (stopped || !pendingJobs.Writer.TryWrite (j)) {
					FailJob (j, failure ?? new InvalidOperationException ("The AAPT2 daemon has stopped."));
					j.Complete (errored: true);
				} else if (pendingJobs.Reader.Count > (daemons.Count * 2)) {
					SpawnAapt2Daemon ();
				}
				return j.JobId;
			}
		}

		public bool JobSucceded (long jobid) {
			return jobs [jobid].Succeeded;
		}

		public Job [] WaitForJobsToComplete (IEnumerable <long> jobIds)
		{
			List<TPL.Task> completedJobsTasks = new List<TPL.Task> ();
			List<Job> results = new List<Job> ();
			foreach (var job in jobIds) {
				completedJobsTasks.Add (jobs [job].Task);
				results.Add (jobs [job]);
			}
			TPL.Task.WaitAll (completedJobsTasks.ToArray ());
			return results.ToArray ();
		}

		public void Stop ()
		{
			lock (gate) {
				stopped = true;
				pendingJobs.Writer.TryComplete ();
			}
		}

		void FailJob (Job job, Exception exception)
		{
			job.Output.Add (new OutputLine (exception.Message, stdError: true, errored: true, job.JobId));
		}

		void FailPendingJobs (Exception exception)
		{
			while (pendingJobs.Reader.TryRead (out var job)) {
				FailJob (job, exception);
				job.Complete (errored: true);
			}
		}

		void FailWorkers (Exception exception)
		{
			lock (gate) {
				failure ??= exception;
				Stop ();
			}
			cancellation.Cancel ();
			FailPendingJobs (exception);
		}

		async TPL.Task Aapt2DaemonStart ()
		{
			try {
				while (await pendingJobs.Reader.WaitToReadAsync (Token).ConfigureAwait (false)) {
					using var aapt2 = new Process { StartInfo = CreateStartInfo () };
					Token.ThrowIfCancellationRequested ();
					aapt2.Start ();
					using var execution = CancellationTokenSource.CreateLinkedTokenSource (Token);
					using var reading = CancellationTokenSource.CreateLinkedTokenSource (execution.Token);
					var exit = aapt2.SafeHandle.WaitForExitOrKillOnCancellationAsync (execution.Token);
					using var stdout = aapt2.StandardOutput;
					using var stderr = aapt2.StandardError;
					var input = aapt2.StandardInput;
					input.AutoFlush = false;
					var output = stdout.BaseStream.CopyToAsync (Stream.Null, reading.Token);
					var monitor = BoundDrainAsync ();
					try {
						while (await pendingJobs.Reader.WaitToReadAsync (Token).ConfigureAwait (false)) {
							if (!pendingJobs.Reader.TryRead (out var job))
								continue;
							Interlocked.Increment (ref jobsRunning);
							bool errored = true;
							bool restart = false;
							try {
								job.Token.ThrowIfCancellationRequested ();
								using var registration = job.Token.Register (execution.Cancel);
								using var jobCancellation = CancellationTokenSource.CreateLinkedTokenSource (job.Token, reading.Token);
								errored = await RunJobAsync (input, stderr, job, exit, jobCancellation.Token).ConfigureAwait (false);
							} catch (Exception ex) when (job.Token.IsCancellationRequested && !Token.IsCancellationRequested &&
								(ex is OperationCanceledException || ex is IOException)) {
								FailJob (job, ex);
								restart = execution.IsCancellationRequested;
							} catch (Exception ex) {
								FailJob (job, failure ?? ex);
								FailWorkers (ex);
								throw;
							} finally {
								Interlocked.Decrement (ref jobsRunning);
								job.Complete (errored);
							}
							if (restart || execution.IsCancellationRequested)
								break;
						}
					} finally {
						try {
							await ShutdownAsync (input, execution, exit).ConfigureAwait (false);
						} finally {
							reading.Cancel ();
							try {
								await output.WaitAsync (ShutdownTimeout).ConfigureAwait (false);
							} catch (OperationCanceledException) when (reading.IsCancellationRequested) {
								// Cancellation also releases inherited stdout handles.
							} finally {
								try {
									await monitor.WaitAsync (ShutdownTimeout).ConfigureAwait (false);
								} finally {
									try {
										input.Dispose ();
									} catch (IOException ex) {
										LogShutdown (ex.Message);
									}
								}
							}
						}
					}

					async TPL.Task BoundDrainAsync ()
					{
						await TPL.Task.WhenAny (output, exit).ConfigureAwait (false);
						if (output.IsFaulted) {
							var exception = output.Exception;
							if (exception != null)
								FailWorkers (exception.GetBaseException ());
							await output.ConfigureAwait (false);
						}
						await exit.ConfigureAwait (false);
						reading.CancelAfter (ShutdownTimeout);
					}
				}
			} catch (OperationCanceledException) when (Token.IsCancellationRequested) {
				// Disposal and a failed worker cancel the remaining workers.
			} catch (Exception ex) {
				FailWorkers (ex);
			} finally {
				if (Token.IsCancellationRequested)
					FailPendingJobs (failure ?? new OperationCanceledException (Token));
			}
		}

		ProcessStartInfo CreateStartInfo ()
		{
			return new ProcessStartInfo (Aapt2) {
				Arguments = "daemon",
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden,
				RedirectStandardInput = true,
				RedirectStandardError = true,
				RedirectStandardOutput = true,
				UseShellExecute = false,
				WorkingDirectory = Path.GetTempPath (),
				StandardErrorEncoding = Encoding.UTF8,
				StandardOutputEncoding = Encoding.UTF8,
				// A BOM would be interpreted as part of the first command.
				StandardInputEncoding = Files.UTF8withoutBOM,
			};
		}

		async TPL.Task<bool> RunJobAsync (StreamWriter writer, StreamReader stderr, Job job, TPL.Task<ProcessExitStatus> exit, CancellationToken token)
		{
			bool errored = false;
			var diagnostics = new List<string> ();
			try {
				foreach (var arg in job.Commands)
					await writer.WriteLineAsync (arg.AsMemory (), token).ConfigureAwait (false);
				await writer.WriteLineAsync (ReadOnlyMemory<char>.Empty, token).ConfigureAwait (false);
				await writer.FlushAsync (token).ConfigureAwait (false);

				bool done = false;
				string? line;
				// stderr EOF must not be hidden by a daemon or descendant keeping stdout open.
				while ((line = await stderr.ReadLineAsync (token).ConfigureAwait (false)) != null) {
					if (string.Equals (line, "Done", StringComparison.OrdinalIgnoreCase)) {
						done = true;
						break;
					}
					if (string.Equals (line, "Error", StringComparison.OrdinalIgnoreCase))
						errored = true;
					else
						diagnostics.Add (line);
				}
				if (!done)
					throw new EndOfStreamException ($"{ToolName} closed its output before reporting Done.");
				if (errored && diagnostics.Count == 0)
					diagnostics.Add ($"{ToolName} reported Error.");

				if (!job.OutputFile.IsNullOrEmpty () && !errored) {
					var timer = Stopwatch.StartNew ();
					while (!File.Exists (job.OutputFile)) {
						token.ThrowIfCancellationRequested ();
						if (exit.IsCompleted)
							throw new EndOfStreamException ($"{ToolName} exited before creating '{job.OutputFile}'.");
						if (timer.Elapsed >= OutputFileTimeout)
							throw new TimeoutException ($"{ToolName} did not create '{job.OutputFile}' within {OutputFileTimeout.TotalSeconds} seconds of reporting Done.");
						await TPL.Task.Delay (10, token).ConfigureAwait (false);
					}
				}
				return errored;
			} catch {
				errored = true;
				throw;
			} finally {
				// Error follows the diagnostics, so classification must wait for the whole response.
				foreach (var line in diagnostics)
					job.Output.Add (new OutputLine (line, stdError: !IsAapt2Warning (line), errored: errored, jobId: job.JobId));
			}
		}

		async TPL.Task ShutdownAsync (StreamWriter input, CancellationTokenSource execution, TPL.Task<ProcessExitStatus> exit)
		{
			try {
				if (!execution.IsCancellationRequested && !exit.IsCompleted) {
					execution.CancelAfter (ShutdownTimeout);
					try {
						await input.WriteLineAsync ("quit".AsMemory (), execution.Token).ConfigureAwait (false);
						await input.WriteLineAsync (ReadOnlyMemory<char>.Empty, execution.Token).ConfigureAwait (false);
						await input.FlushAsync (execution.Token).ConfigureAwait (false);
						await exit.WaitAsync (ShutdownTimeout).ConfigureAwait (false);
					} catch (IOException ex) {
						LogShutdown (ex.Message);
					} catch (OperationCanceledException ex) when (execution.IsCancellationRequested) {
						LogShutdown (ex.Message);
					} catch (TimeoutException ex) {
						LogShutdown (ex.Message);
					}
				}
			} finally {
				execution.Cancel ();
				await exit.WaitAsync (ShutdownTimeout).ConfigureAwait (false);
			}
		}

		void LogShutdown (string message)
		{
			try {
				logger ($"{nameof (Aapt2Daemon)}: {message}");
			} catch (ObjectDisposedException) {
				// A build-scoped logger can be disposed before an AppDomain-scoped daemon.
				Debug.WriteLine (message);
			}
		}

		bool IsAapt2Warning (string singleLine)
		{
			var match = AndroidRunToolTask.AndroidErrorRegex.Match (singleLine.Trim ());
			if (match.Success)
			{
				var file = match.Groups ["file"].Value;
				var level = match.Groups ["level"].Value.ToLowerInvariant();
				var message = match.Groups ["message"].Value;
				if (singleLine.StartsWith ($"{ToolName} W", StringComparison.OrdinalIgnoreCase))
					return true;
				if (file.StartsWith ("W/", StringComparison.OrdinalIgnoreCase))
					return true;
				if (message.Contains ("warn:"))
					return true;
				if (level.Contains ("warning"))
					return true;
			}
			return false;
		}
	}
}
