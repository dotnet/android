using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Reflection;
using Microsoft.Build.Framework;
using TPL = System.Threading.Tasks;
using Xamarin.Android.Tools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	internal class Aapt2Daemon : IDisposable
	{
		static readonly string TypeFullName = typeof (Aapt2Daemon).FullName;

		internal static object RegisterTaskObjectKey => TypeFullName;

		public static Aapt2Daemon GetInstance (IBuildEngine4 engine, Action<string> log, string aapt2, int numberOfInstances, int initalNumberOfDaemons, bool registerInDomain = false)
		{
			var area = registerInDomain ? RegisteredTaskObjectLifetime.AppDomain : RegisteredTaskObjectLifetime.Build;
			var daemon = engine.GetRegisteredTaskObjectAssemblyLocal<Aapt2Daemon> (RegisterTaskObjectKey, area);
			if (daemon == null)
			{
				daemon = new Aapt2Daemon (aapt2, numberOfInstances, initalNumberOfDaemons, log);
				engine.RegisterTaskObjectAssemblyLocal (RegisterTaskObjectKey, daemon, area, allowEarlyCollection: false);
			}
			return daemon;
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

		readonly object lockObject = new object ();
		readonly BlockingCollection<Job> pendingJobs = new BlockingCollection<Job> ();
		readonly ConcurrentDictionary<long, Job> jobs = new ConcurrentDictionary<long, Job> ();
		readonly CancellationTokenSource tcs = new CancellationTokenSource ();
		readonly ConcurrentBag<Thread> daemons = new ConcurrentBag<Thread> ();

		long jobsRunning = 0;
		long jobId = 0;
		int maxInstances = 0;
		Action<string> logger = null;

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

		public Aapt2Daemon (string aapt2, int maxNumberOfInstances, int initalNumberOfDaemons, Action<string> log)
		{
			Aapt2 = aapt2;
			maxInstances = maxNumberOfInstances;
			logger = log;
			for (int i = 0; i < initalNumberOfDaemons; i++) {
				SpawnAapt2Daemon ();
			}
		}

		void SpawnAapt2Daemon ()
		{
			// Don't spawn too many
			if (daemons.Count >= maxInstances)
				return;
			var thread = new Thread (Aapt2DaemonStart)
			{
				IsBackground = true
			};
			thread.Start ();
			daemons.Add(thread);
		}

		public void Dispose ()
		{
			Stop ();
			tcs.Cancel ();
		}

		public long QueueCommand (string[] job, string outputFile, CancellationToken token)
		{
			if (!pendingJobs.IsAddingCompleted)
			{
				long id = Interlocked.Add (ref jobId, 1);
				var j = new Job (job, id, outputFile, token);
				jobs [j.JobId] = j;
				pendingJobs.Add (j);
				// if we have allot of pending jobs, spawn more daemons
				if (pendingJobs.Count > (daemons.Count * 2)) {
					SpawnAapt2Daemon ();
				}
				return j.JobId;
			}
			return -1;
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
			//This will cause '_jobs.GetConsumingEnumerable' to stop blocking and exit when it's empty
			pendingJobs.CompleteAdding ();
		}

		private bool SetConsoleInputEncoding (Encoding encoding)
		{
			try {
				if (Console.InputEncoding != encoding) {
					Console.InputEncoding = encoding;
					return true;
				}
			} catch (IOException) {
				//In a DesignTime Build on VS Windows sometimes this exception is raised.
				//We should catch it, but there is nothing we can do about it.
			}
			return false;
		}

		private bool SetProcessInputEncoding (ProcessStartInfo info, Encoding encoding)
		{
			Type type = info.GetType ();
			PropertyInfo prop = type.GetRuntimeProperty ("StandardInputEncoding");
			if (prop == null)
				prop = type.GetProperty ("StandardInputEncoding", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			if(prop?.CanWrite ?? false) {
				prop.SetValue (info, encoding, null);
				return true;
			}
			return false;
		}

		private void Aapt2DaemonStart ()
		{
			ProcessStartInfo info = new ProcessStartInfo (Aapt2)
			{
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
				// We need to FORCE the StandardInput to be UTF8 so we can use
				// accented characters. Also DONT INCLUDE A BOM!!
				// otherwise aapt2 will try to interpret the BOM as an argument.
				// Cant use this cos its netstandard 2.1 only
				// and we are using netstandard 2.0
				//StandardInputEncoding = Files.UTF8withoutBOM,
			};
			Process aapt2;
			Encoding currentEncoding = Console.InputEncoding;
 			lock (lockObject) {
				try {
					if (!SetProcessInputEncoding (info, Files.UTF8withoutBOM))
						SetConsoleInputEncoding (Files.UTF8withoutBOM);
					aapt2 = new Process ();
					aapt2.StartInfo = info;
					aapt2.Start ();
				} finally {
					SetConsoleInputEncoding (currentEncoding);
				}
			}
			try {
				foreach (var job in pendingJobs.GetConsumingEnumerable (tcs.Token)) {
					Interlocked.Add (ref jobsRunning, 1);
					bool errored = false;
					try {
						// try to write Unicode UTF8 to aapt2
						using (StreamWriter writer = new StreamWriter (aapt2.StandardInput.BaseStream, Files.UTF8withoutBOM, bufferSize: 1024, leaveOpen: true)) {
							foreach (var arg in job.Commands) {
								writer.WriteLine (arg);
							}
							writer.WriteLine ();
							writer.Flush ();
						}
						string line;

						Queue<string> stdError = new Queue<string> ();
						while ((line = aapt2.StandardError.ReadLine ()) != null) {
							if (string.Compare (line, "Done", StringComparison.OrdinalIgnoreCase) == 0) {
								break;
							}
							if (string.Compare (line, "Error", StringComparison.OrdinalIgnoreCase) == 0) {
								errored = true;
								continue;
							}
							// we have to queue the output because the "Done"/"Error" lines are
							//written after all the messages. So to process the warnings/errors
							// correctly we need to do this after we know if worked or failed.
							stdError.Enqueue (line);
						}
						//now processed the output we queued up
						while (stdError.Count > 0) {
							line = stdError.Dequeue ();
							job.Output.Add (new OutputLine (line, stdError: !IsAapt2Warning (line), errored: errored, jobId: job.JobId));
						}
						// wait for the file we expect to be created. There can be a delay between
						// the daemon saying "Done" and the file finally being written to disk.
						if (!string.IsNullOrEmpty (job.OutputFile) && !errored) {
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
				aapt2.WaitForExit ((int)TimeSpan.FromSeconds (5).TotalMilliseconds);
			} catch (IOException) {
				// Ignore this error. It occurs when the Build it cancelled.
				logger?.Invoke ($"{nameof (Aapt2Daemon)}: Ignoring IOException. Build was cancelled.");
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
