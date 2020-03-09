using System;
using System.Text;
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

					Log.LogMessage (MessageImportance.Normal, $"Starting Command: {cmd.GetMetadata ("Command")} Arguments: {cmd.GetMetadata ("Arguments")}");

					try {
						using (var proc = new Process ()) {
							StringBuilder standardOutput = new StringBuilder (), errorOutput = new StringBuilder ();

							proc.StartInfo.FileName = useManagedRuntime ? ManagedRuntime : cmd.GetMetadata ("Command");
							proc.StartInfo.Arguments = $"{argumentsBeginning}{cmd.GetMetadata ("Arguments")}";
							proc.StartInfo.CreateNoWindow = true;
							proc.StartInfo.UseShellExecute = false;
							proc.StartInfo.RedirectStandardOutput = true;
							proc.StartInfo.RedirectStandardError = true;

							proc.OutputDataReceived += new DataReceivedEventHandler ((sender, e) => {
								if (!string.IsNullOrEmpty (e.Data))
									standardOutput.Append (e.Data);
							});
							proc.ErrorDataReceived += new DataReceivedEventHandler ((sender, e) => {
								if (!string.IsNullOrEmpty (e.Data))
									errorOutput.Append (e.Data);
							});

							proc.Start ();
							proc.BeginOutputReadLine ();
							proc.BeginErrorReadLine ();
							proc.WaitForExit ();

							var output = standardOutput.ToString ();
							var errOutput = errorOutput.ToString ();

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
