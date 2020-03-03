using System;
using System.Diagnostics;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Tasks = System.Threading.Tasks;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	public class RunParallelCmds : Task
	{
		[Required]
		public ITaskItem[] Commands { get; set; }

		public string ManagedRuntime { get; set; }

		public string ManagedRuntimeArguments { get; set; }

		public override bool Execute ()
		{
			bool success = true;

			Tasks.Parallel.ForEach (Commands, new Tasks.ParallelOptions () { MaxDegreeOfParallelism = Environment.ProcessorCount },
				cmd => {
					var useManagedRuntime = !string.IsNullOrEmpty (ManagedRuntime);
					var argumentsBeginning = useManagedRuntime ? ManagedRuntimeArguments + " " : "";
					var procStartInfo = new ProcessStartInfo () {
						FileName =  useManagedRuntime ? ManagedRuntime : cmd.GetMetadata ("Command"),
						Arguments = $"{argumentsBeginning}{cmd.GetMetadata ("Arguments")}",
						CreateNoWindow = true,
						UseShellExecute = false,
						RedirectStandardOutput = true,
						RedirectStandardError = true,
					};

					Log.LogMessage (MessageImportance.Normal, $"Starting Command: {cmd.GetMetadata ("Command")} Arguments: {cmd.GetMetadata ("Arguments")}");

					try {
						using (var proc = Process.Start (procStartInfo)) {
							proc.WaitForExit ();

							var output = proc.StandardOutput.ReadToEnd ();
							var errOutput = proc.StandardError.ReadToEnd ();

							if (proc.ExitCode != 0) {
								Log.LogMessage (MessageImportance.High, $"Non-zero exit code: {proc.ExitCode}  Error output: {errOutput}");
								success = false;
							}

							if (!string.IsNullOrEmpty (output))
								Log.LogMessage (MessageImportance.Normal, $"Output: {output}");
						}
					} catch (Exception e) {
						Log.LogError ($"Unable to run command: {cmd.GetMetadata ("Command")}\nException:\n{e}");
						success = false;
					}
				});

			if (!success)
				return false;

			return !Log.HasLoggedErrors;
		}
	}
}
