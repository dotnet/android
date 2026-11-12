using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Tasks = System.Threading.Tasks;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	public class RunParallelCmds : Task, ICancelableTask
	{
		const int OutputTimeout = 30; // seconds after process exit

		sealed class CommandResult
		{
			public string Command { get; set; }
			public ProcessStartInfo StartInfo { get; set; }
			public StringBuilder StandardOutput { get; } = new StringBuilder ();
			public StringBuilder StandardError { get; } = new StringBuilder ();
			public int ExitCode { get; set; } = -1;
			public Exception Error { get; set; }
		}

		readonly Lock cancellationLock = new Lock ();
		CancellationTokenSource commandCancellation;
		volatile bool canceled;

		public string TaskPrefix => "RPCMD";

		[Required]
		public ITaskItem[] Commands { get; set; }

		public string ManagedRuntime { get; set; }

		public string ManagedRuntimeArguments { get; set; }

		public bool YieldDuringToolExecution { get; set; }

		public void Cancel ()
		{
			lock (cancellationLock) {
				canceled = true;
				commandCancellation?.Cancel ();
			}
		}

		public override bool Execute ()
		{
			using var cancellation = new CancellationTokenSource ();
			lock (cancellationLock) {
				if (canceled)
					return false;
				commandCancellation = cancellation;
			}

			bool yielded = false;
			try {
				if (YieldDuringToolExecution && BuildEngine is IBuildEngine3 engine) {
					engine.Yield ();
					yielded = true;
				}

				var commands = Commands.Select (CreateCommand).ToArray ();
				int started = 0;
				int completed = 0;
				Log.LogMessage ($"RunParallelCmds max parallelism: {Environment.ProcessorCount}");
				using var notifications = new BlockingCollection<(CommandResult Command, bool Completed)> ();
				var execution = RunCommandsAsync ();
				try {
					foreach (var notification in notifications.GetConsumingEnumerable ()) {
						var result = notification.Command;
						if (!notification.Completed) {
							Log.LogMessage ($"[{++started}/{Commands.Length}] Starting Command: {result.Command} Arguments: {result.StartInfo.Arguments}");
							continue;
						}

						completed++;
						if (result.StandardOutput.Length > 0)
							Log.LogMessage ($"Output: {result.StandardOutput}");
						if (result.Error is OperationCanceledException && cancellation.IsCancellationRequested)
							Log.LogMessage ($"Command canceled: {result.Command}\nError output: {result.StandardError}");
						else if (result.Error != null)
							Log.LogError ($"Unable to run command: {result.Command}\nException:\n{result.Error}\nError output: {result.StandardError}");
						else if (result.ExitCode != 0)
							Log.LogError ($"\"{result.StartInfo.FileName} {result.StartInfo.Arguments}\" failed with code: {result.ExitCode}  Error output: {result.StandardError}");
						else if (result.StandardError.Length > 0)
							Log.LogMessage ($"Error output: {result.StandardError}");
					}
				} finally {
					if (!execution.IsCompleted)
						cancellation.Cancel ();
					execution.GetAwaiter ().GetResult ();
				}

				Log.LogMessage ($"RunParallelCmds completed {completed} commands");
				return !cancellation.IsCancellationRequested && !Log.HasLoggedErrors;

				async Tasks.Task RunCommandsAsync ()
				{
					try {
						await Tasks.Parallel.ForEachAsync (commands,
							new Tasks.ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
							async (command, _) => {
								var result = new CommandResult {
									Command = command.Command,
									StartInfo = command.StartInfo,
								};
								try {
									cancellation.Token.ThrowIfCancellationRequested ();
									notifications.Add ((result, false));
									await RunCommandAsync (result, cancellation.Token).ConfigureAwait (false);
								} catch (Exception ex) {
									result.Error = ex;
								} finally {
									notifications.Add ((result, true));
								}
							}).ConfigureAwait (false);
					} finally {
						notifications.CompleteAdding ();
					}
				}
			} finally {
				try {
					if (yielded && BuildEngine is IBuildEngine3 engine)
						engine.Reacquire ();
				} finally {
					lock (cancellationLock) commandCancellation = null;
				}
			}
		}

		(string Command, ProcessStartInfo StartInfo) CreateCommand (ITaskItem item)
		{
			string command = item.GetMetadata ("Command");
			var arguments = new CommandLineBuilder ();
			if (!string.IsNullOrEmpty (ManagedRuntime)) {
				arguments.AppendTextUnquoted (ManagedRuntimeArguments);
				arguments.AppendFileNameIfNotNull (command);
			}
			arguments.AppendTextUnquoted (" " + item.GetMetadata ("Arguments"));
			return (command, new ProcessStartInfo {
				FileName = string.IsNullOrEmpty (ManagedRuntime) ? command : ManagedRuntime,
				Arguments = arguments.ToString ().Replace ('\\', Path.DirectorySeparatorChar),
				WorkingDirectory = item.GetMetadata ("WorkingDirectory"),
				CreateNoWindow = true,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
			});
		}

		static async Tasks.Task RunCommandAsync (CommandResult result, CancellationToken cancellationToken)
		{
			using var process = new Process { StartInfo = result.StartInfo };
			process.Start ();
			using var exitCancellation = CancellationTokenSource.CreateLinkedTokenSource (cancellationToken);
			using var outputCancellation = CancellationTokenSource.CreateLinkedTokenSource (cancellationToken);
			var exit = WaitForExitAsync ();
			try {
				await foreach (var line in process.ReadAllLinesAsync (outputCancellation.Token)) {
					(line.StandardError ? result.StandardError : result.StandardOutput).AppendLine (line.Content);
				}
				result.ExitCode = (await exit.ConfigureAwait (false)).ExitCode;
			} catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && !exitCancellation.IsCancellationRequested) {
				if (exit.IsFaulted)
					await exit.ConfigureAwait (false);
				throw new TimeoutException ($"Command '{result.Command}' output did not close within {OutputTimeout}s after exit.", ex);
			} finally {
				exitCancellation.Cancel ();
				outputCancellation.Cancel ();
				try {
					try {
						await exit.ConfigureAwait (false);
					} catch (OperationCanceledException) when (exitCancellation.IsCancellationRequested) {
					}
				} finally {
					try {
						try {
							if (!process.HasExited)
								process.Kill (entireProcessTree: true);
						} catch (InvalidOperationException) when (process.HasExited) {
						}
						if (!process.SafeHandle.TryWaitForExit (TimeSpan.FromSeconds (5), out _))
							throw new TimeoutException ($"Command '{result.Command}' did not exit after termination.");
					} finally {
						using var stdout = process.StandardOutput;
						using var stderr = process.StandardError;
					}
				}
			}

			async Tasks.Task<ProcessExitStatus> WaitForExitAsync ()
			{
				try {
					var status = await process.SafeHandle.WaitForExitAsync (exitCancellation.Token).ConfigureAwait (false);
					outputCancellation.CancelAfter (TimeSpan.FromSeconds (OutputTimeout));
					return status;
				} catch {
					outputCancellation.Cancel ();
					throw;
				}
			}
		}
	}
}
