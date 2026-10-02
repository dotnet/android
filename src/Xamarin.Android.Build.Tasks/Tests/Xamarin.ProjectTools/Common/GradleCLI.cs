using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
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
			return Execute (5 * 60 * 1000, args);
		}

		bool Execute (int timeoutMilliseconds, params string [] args)
		{
			if (!File.Exists (GradlePath)) {
				throw new FileNotFoundException ($"Gradle tool was not found at {GradlePath}.");
			}

			if (string.IsNullOrEmpty (ProcessLogFile)) {
				Directory.CreateDirectory (ProjectDirectory);
				ProcessLogFile = Path.Combine (ProjectDirectory, $"gradle{DateTime.Now.ToString ("yyyyMMddHHmmssff")}-process.log");
			}

			var procOutput = new StringBuilder ();
			var outputLock = new object ();
			bool acceptingOutput = true;
			void WriteOutput (string line)
			{
				lock (outputLock) {
					if (acceptingOutput)
						procOutput.AppendLine (line);
				}
			}
			bool succeeded;

			using (var p = new Process ()) {
				var errorDone = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
				var outputDone = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
				p.StartInfo.FileName = GradlePath;
				p.StartInfo.Arguments = string.Join (" ", args);
				p.StartInfo.Arguments += $" --no-daemon";

				p.StartInfo.CreateNoWindow = true;
				p.StartInfo.UseShellExecute = false;
				p.StartInfo.RedirectStandardOutput = true;
				p.StartInfo.RedirectStandardError = true;
				p.StartInfo.SetEnvironmentVariable ("JAVA_HOME", JavaSdkPath);

				if (Directory.Exists (ProjectDirectory)) {
					p.StartInfo.WorkingDirectory = ProjectDirectory;
				};

				p.ErrorDataReceived += (sender, e) => {
					if (e.Data == null)
						errorDone.TrySetResult (true);
					else
						WriteOutput (e.Data);
				};
				p.OutputDataReceived += (sender, e) => {
					if (e.Data == null)
						outputDone.TrySetResult (true);
					else
						WriteOutput (e.Data);
				};

				try {
					WriteOutput ($"Running: {p.StartInfo.FileName} {p.StartInfo.Arguments}");
					p.Start ();
					p.BeginOutputReadLine ();
					p.BeginErrorReadLine ();
					bool completed = p.WaitForExit (timeoutMilliseconds);
					if (!completed) {
						WriteOutput ($"Process timed out after {timeoutMilliseconds}ms.");
						if (!p.HasExited) {
							try {
								p.Kill (entireProcessTree: true);
							} catch (InvalidOperationException) when (p.HasExited) {
								// The process exited before the kill request.
							}
							if (!p.WaitForExit (30000))
								WriteOutput ("Process did not exit after termination.");
						}
					}
					if (!Builder.WaitForRedirectedOutput (outputDone.Task, errorDone.Task))
						WriteOutput ("Process exited or timed out with redirected output still open.");
					succeeded = completed && p.ExitCode == 0;
					lock (outputLock) {
						acceptingOutput = false;
						procOutput.AppendLine (completed ? $"Exit Code: {p.ExitCode}" : "Exit Code: <timed out>");
					}
				} finally {
					lock (outputLock) {
						acceptingOutput = false;
					}
				}
			}

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
