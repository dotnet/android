using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Xamarin.ProjectTools
{
	public class GradleCLI
	{
		public string ProcessLogFile { get; set; } = string.Empty;
		
		public string JavaSdkPath { get; set; } = AndroidSdkResolver.GetJavaSdkPath ();

		public string GradlePath { get; set; } = Path.Combine (XABuildPaths.TopDirectory, "build-tools", "gradle", TestEnvironment.IsWindows ? "gradlew.bat" : "gradlew");

		public string ProjectDirectory { get; set; } = string.Empty;

		public bool Execute (params string [] args)
		{
			return ExecuteAsync (args).GetAwaiter ().GetResult ();
		}

		async Task<bool> ExecuteAsync (string [] args)
		{
			if (!File.Exists (GradlePath)) {
				throw new FileNotFoundException ($"Gradle tool was not found at {GradlePath}.");
			}

			if (string.IsNullOrEmpty (ProcessLogFile)) {
				Directory.CreateDirectory (ProjectDirectory);
				ProcessLogFile = Path.Combine (ProjectDirectory, $"gradle{DateTime.Now.ToString ("yyyyMMddHHmmssff")}-process.log");
			}

			var procOutput = new StringBuilder ();
			var psi = new ProcessStartInfo (GradlePath, string.Join (" ", args) + " --no-daemon") {
				CreateNoWindow = true,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				InheritedHandles = [],
			};
			psi.Environment ["JAVA_HOME"] = JavaSdkPath;
			if (Directory.Exists (ProjectDirectory))
				psi.WorkingDirectory = ProjectDirectory;
			procOutput.AppendLine ($"Running: {psi.FileName} {psi.Arguments}");
			bool succeeded = false;
			using var executionDeadline = new CancellationTokenSource (TimeSpan.FromMinutes (5));
			using var outputDeadline = CancellationTokenSource.CreateLinkedTokenSource (executionDeadline.Token);
			using var process = Process.Start (psi) ?? throw new InvalidOperationException ($"Failed to start '{GradlePath}'.");
			async Task LimitOutputDrainAsync ()
			{
				await process.WaitForExitAsync (executionDeadline.Token).ConfigureAwait (false);
				executionDeadline.CancelAfter (Timeout.InfiniteTimeSpan);
				outputDeadline.CancelAfter (TimeSpan.FromSeconds (2));
			}
			var exited = LimitOutputDrainAsync ();
			try {
				await foreach (var line in process.ReadAllLinesAsync (outputDeadline.Token).ConfigureAwait (false))
					procOutput.AppendLine (line.Content);
				await exited.ConfigureAwait (false);
				succeeded = process.ExitCode == 0;
			} catch (OperationCanceledException) when (executionDeadline.IsCancellationRequested) {
				procOutput.AppendLine ("Process timed out after 5 minutes.");
			} catch (OperationCanceledException) when (outputDeadline.IsCancellationRequested) {
				procOutput.AppendLine ("Process exited with redirected output still open after 2 seconds.");
				succeeded = process.ExitCode == 0;
			} finally {
				using var stdoutReader = process.StandardOutput;
				using var stderrReader = process.StandardError;
				executionDeadline.Cancel ();
				try {
					await exited.ConfigureAwait (false);
				} catch (OperationCanceledException) when (executionDeadline.IsCancellationRequested) {
					// The execution deadline owns this exit observer.
				}
				if (!process.HasExited) {
					try {
						process.Kill (entireProcessTree: true);
					} catch (InvalidOperationException) when (process.HasExited) {
						// The process exited before the kill request.
					}
					if (!process.WaitForExit (30000))
						throw new TimeoutException ($"Process {process.Id} did not exit after termination.");
				}
			}

			procOutput.AppendLine ($"Exit Code: {process.ExitCode}");
			File.WriteAllText (ProcessLogFile, procOutput.ToString ());
			return succeeded;
		}

		public bool Init (string projectDirectory, string projectType = "basic", string dsl = "kotlin", string packageName = "")
		{
			ProjectDirectory = projectDirectory;
			Directory.CreateDirectory (projectDirectory);
			var projName = Path.GetFileName (projectDirectory);
			var arguments = new List<string> {
				"init",
				"--dsl", dsl,
				"--incubating",
				"--project-name", projName,
				"--type", projectType,
			};
			if (!string.IsNullOrEmpty (packageName)) {
				arguments.Add ("--package");
				arguments.Add (packageName);
			}
			return Execute (arguments.ToArray ());
		}

	}
}
