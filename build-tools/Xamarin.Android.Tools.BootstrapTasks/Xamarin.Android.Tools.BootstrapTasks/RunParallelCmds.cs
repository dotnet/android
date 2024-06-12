using System;
using System.IO;
using System.Text;
using System.Diagnostics;

using Microsoft.Android.Build.Tasks;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Tasks = System.Threading.Tasks;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	public class RunParallelCmds : AsyncTask
	{
		[Required]
		public ITaskItem[] Commands { get; set; }

		public string ManagedRuntime { get; set; }

		public string ManagedRuntimeArguments { get; set; }

		public override bool Execute ()
		{
			bool success = true;
			int tasks = 0;

			LogMessage ($"RunParallelCmds max parallelism: {Environment.ProcessorCount}");

			Tasks.Parallel.ForEach (Commands, new Tasks.ParallelOptions () { MaxDegreeOfParallelism = Environment.ProcessorCount },
				cmd => {
					var command = cmd.GetMetadata ("Command");
					var useManagedRuntime = !string.IsNullOrEmpty (ManagedRuntime);
					var argumentsBeginning = useManagedRuntime ? $"{ManagedRuntimeArguments} {command} " : "";

					LogMessage ($"[{++tasks}/{Commands.Length}] Starting Command: {command} Arguments: {cmd.GetMetadata ("Arguments")}");

					try {
						using (var proc = new Process ()) {
							StringBuilder standardOutput = new StringBuilder (), errorOutput = new StringBuilder ();

							proc.StartInfo.FileName = useManagedRuntime ? ManagedRuntime : command;
							proc.StartInfo.Arguments = $"{argumentsBeginning}{cmd.GetMetadata ("Arguments")}".Replace ('\\', Path.DirectorySeparatorChar);
							proc.StartInfo.CreateNoWindow = true;
							proc.StartInfo.UseShellExecute = false;
							proc.StartInfo.RedirectStandardOutput = true;
							proc.StartInfo.RedirectStandardError = true;

							var wd = cmd.GetMetadata ("WorkingDirectory");
							if (!string.IsNullOrEmpty (wd))
								proc.StartInfo.WorkingDirectory = wd;

							proc.OutputDataReceived += new DataReceivedEventHandler ((sender, e) => {
								if (!string.IsNullOrEmpty (e.Data))
									standardOutput.Append (Environment.NewLine + e.Data);
							});
							proc.ErrorDataReceived += new DataReceivedEventHandler ((sender, e) => {
								if (!string.IsNullOrEmpty (e.Data))
									errorOutput.Append (Environment.NewLine + e.Data);
							});

							proc.Start ();
							proc.BeginOutputReadLine ();
							proc.BeginErrorReadLine ();
							proc.WaitForExit ();

							var output = standardOutput.ToString ();
							var errOutput = errorOutput.ToString ();

							if (proc.ExitCode != 0) {
								LogError ($"\"{proc.StartInfo.FileName} {proc.StartInfo.Arguments}\" failed with code: {proc.ExitCode}  Error output: {errOutput}");
								success = false;
							}

							if (!string.IsNullOrEmpty (output))
								LogMessage ($"Output: {output}");
						}
					} catch (Exception e) {
						LogError ($"Unable to run command: {command}\nException:\n{e}");
						success = false;
					}

					Complete ();
				});

			WaitForCompletion ();

			LogMessage ($"RunParallelCmds completed {tasks} commands");

			if (!success)
				return false;

			return !Log.HasLoggedErrors;
		}
	}
}
