#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.Build.Framework;
using TPL = System.Threading.Tasks;
using Xamarin.Android.Tools;
using Microsoft.Android.Build.Tasks;
using Xamarin.Android.Tasks;

namespace Microsoft.Android.Tasks
{
	internal class Aapt2Daemon : IDisposable
	{
		static readonly object instanceLock = new object ();

		/// <summary>
		/// Returns the daemon pool shared by all AAPT2 tasks in the build.
		/// </summary>
		public static Aapt2Daemon GetInstance (IBuildEngine4 engine, string aapt2, int numberOfInstances, int initalNumberOfDaemons)
		{
			var key = (typeof (Aapt2Daemon).FullName, aapt2);
			lock (instanceLock) {
				var daemon = engine.GetRegisteredTaskObjectAssemblyLocal<Aapt2Daemon> (key, RegisteredTaskObjectLifetime.Build);
				if (daemon == null) {
					daemon = new Aapt2Daemon (aapt2, numberOfInstances, initalNumberOfDaemons);
					engine.RegisterTaskObjectAssemblyLocal (key, daemon, RegisteredTaskObjectLifetime.Build, allowEarlyCollection: false);
				}
				return daemon;
			}
		}

		public class Job
		{
			TPL.TaskCompletionSource<bool> tcs = new TPL.TaskCompletionSource<bool> ();
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

			public void Complete (bool result)
			{
				Succeeded = !result;
				tcs.TrySetResult (!result);
			}
		}

		readonly BlockingCollection<Job> pendingJobs = new BlockingCollection<Job> ();
		readonly ConcurrentDictionary<long, Job> jobs = new ConcurrentDictionary<long, Job> ();
		readonly CancellationTokenSource tcs = new CancellationTokenSource ();
		readonly ConcurrentBag<Thread> daemons = new ConcurrentBag<Thread> ();

		long jobsRunning = 0;
		long jobId = 0;
		int maxInstances = 0;

		public CancellationToken Token => tcs.Token;

		public bool JobsInQueue => pendingJobs.Count > 0;

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

		public Aapt2Daemon (string aapt2, int maxNumberOfInstances, int initalNumberOfDaemons)
		{
			Aapt2 = aapt2;
			maxInstances = maxNumberOfInstances;
			for (int i = 0; i < initalNumberOfDaemons; i++) {
				SpawnAapt2Daemon ();
			}
		}

		void SpawnAapt2Daemon ()
		{
			lock (pendingJobs) {
				// Don't spawn too many or start a daemon after shutdown.
				if (pendingJobs.IsAddingCompleted || daemons.Count >= maxInstances)
					return;
				var thread = new Thread (Aapt2DaemonStart)
				{
					IsBackground = true
				};
				thread.Start ();
				daemons.Add(thread);
			}
		}

		public void Dispose ()
		{
			Stop ();
			tcs.Cancel ();
			foreach (var thread in daemons) {
				thread.Join ();
			}
			while (pendingJobs.TryTake (out var job)) {
				job.Complete (true);
			}
		}

		public long QueueCommand (string[] job, string outputFile, CancellationToken token)
		{
			lock (pendingJobs) {
				if (pendingJobs.IsAddingCompleted && !token.IsCancellationRequested)
					throw new InvalidOperationException ("The AAPT2 daemon is not accepting new commands.");
				long id = Interlocked.Add (ref jobId, 1);
				var j = new Job (job, id, outputFile, token);
				jobs [j.JobId] = j;
				if (token.IsCancellationRequested) {
					j.Complete (true);
					return j.JobId;
				}
				pendingJobs.Add (j);
				// if we have allot of pending jobs, spawn more daemons
				if (pendingJobs.Count > (daemons.Count * 2)) {
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
			lock (pendingJobs) {
				// This causes GetConsumingEnumerable to exit when the queue is empty.
				pendingJobs.CompleteAdding ();
			}
		}

		Process StartAapt2Daemon ()
		{
			// aapt2 reports results on stderr; nothing reads stdout, so discard it rather than let the pipe fill up.
			// The child gets its own copy of the handle, so ours can be closed once it has started.
			using var standardOutput = File.OpenNullHandle ();
			var info = new ProcessStartInfo (Aapt2)
			{
				Arguments = "daemon",
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden,
				RedirectStandardInput = true,
				RedirectStandardError = true,
				StandardOutputHandle = standardOutput,
				// Don't leak other daemons' pipes into this process; they would keep those pipes open.
				InheritedHandles = [],
				UseShellExecute = false,
				WorkingDirectory = Path.GetTempPath (),
				StandardErrorEncoding = Encoding.UTF8,
				// We need to FORCE the StandardInput to be UTF8 so we can use
				// accented characters. Also DONT INCLUDE A BOM!!
				// otherwise aapt2 will try to interpret the BOM as an argument.
				StandardInputEncoding = Files.UTF8withoutBOM,
			};
			if (OperatingSystem.IsWindows () || OperatingSystem.IsLinux ()) {
				info.KillOnParentExit = true;
			}
			return Process.Start (info) ?? throw new InvalidOperationException ($"Failed to start `{Aapt2} daemon`.");
		}

		private void Aapt2DaemonStart ()
		{
			using var aapt2 = StartAapt2Daemon ();
			Action stopProcess = () => {
				try {
					aapt2.Kill ();
				} catch (InvalidOperationException) {
					// The process already exited.
				}
			};
			try {
				foreach (var job in pendingJobs.GetConsumingEnumerable (tcs.Token)) {
					Interlocked.Add (ref jobsRunning, 1);
					bool errored = false;
					try {
						// Unblocks a hung aapt2 when the pool is disposed at the end of the build.
						using var cancellation = tcs.Token.Register (stopProcess);
						tcs.Token.ThrowIfCancellationRequested ();
						job.Token.ThrowIfCancellationRequested ();
						// try to write Unicode UTF8 to aapt2
						using (StreamWriter writer = new StreamWriter (aapt2.StandardInput.BaseStream, Files.UTF8withoutBOM, bufferSize: 1024, leaveOpen: true)) {
							foreach (var arg in job.Commands) {
								writer.WriteLine (arg);
							}
							writer.WriteLine ();
							writer.Flush ();
						}
						string? line;

						Queue<string> stdError = new Queue<string> ();
						while ((line = aapt2.StandardError.ReadLine ()) != null) {
							if (string.Equals (line, "Done", StringComparison.OrdinalIgnoreCase)) {
								break;
							}
							if (string.Equals (line, "Error", StringComparison.OrdinalIgnoreCase)) {
								errored = true;
								continue;
							}
							// we have to queue the output because the "Done"/"Error" lines are
							//written after all the messages. So to process the warnings/errors
							// correctly we need to do this after we know if worked or failed.
							stdError.Enqueue (line);
						}
						tcs.Token.ThrowIfCancellationRequested ();
						job.Token.ThrowIfCancellationRequested ();
						//now processed the output we queued up
						while (stdError.Count > 0) {
							line = stdError.Dequeue ();
							job.Output.Add (new OutputLine (line, stdError: !IsAapt2Warning (line), errored: errored, jobId: job.JobId));
						}
						// wait for the file we expect to be created. There can be a delay between
						// the daemon saying "Done" and the file finally being written to disk.
						if (!job.OutputFile.IsNullOrEmpty () && !errored) {
							while (!File.Exists (job.OutputFile)) {
								// If either the AsyncTask.CancellationToken or tcs.Token are cancelled, we need to abort
								tcs.Token.ThrowIfCancellationRequested ();
								job.Token.ThrowIfCancellationRequested ();
								Thread.Sleep (10);
							}
						}
					} catch (Exception ex) {
						errored = true;
						job.Output.Add (new OutputLine (ex.Message, stdError: true, errored: errored, job.JobId));
					} finally {
						Interlocked.Decrement (ref jobsRunning);
						jobs [job.JobId].Complete (errored);
					}
				}
			}
			catch (OperationCanceledException)
			{
				// Ignore this error. It occurs when the Task is cancelled.
			}
			try {
				aapt2.StandardInput.WriteLine ("quit");
				aapt2.StandardInput.WriteLine ();
			} catch (IOException) {
				// aapt2 already exited, e.g. it was killed during shutdown.
			}
			if (!aapt2.WaitForExit (TimeSpan.FromSeconds (5))) {
				stopProcess ();
				aapt2.WaitForExit ();
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
