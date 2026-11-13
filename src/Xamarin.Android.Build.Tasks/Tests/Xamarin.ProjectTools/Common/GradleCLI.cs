using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

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
			if (!File.Exists (GradlePath)) {
				throw new FileNotFoundException ($"Gradle tool was not found at {GradlePath}.");
			}

			if (string.IsNullOrEmpty (ProcessLogFile)) {
				Directory.CreateDirectory (ProjectDirectory);
				ProcessLogFile = Path.Combine (ProjectDirectory, $"gradle{DateTime.Now.ToString ("yyyyMMddHHmmssff")}-process.log");
			}

			var procOutput = new StringBuilder ();
			bool succeeded;

			var psi = new ProcessStartInfo (GradlePath, string.Join (" ", args) + " --no-daemon") {
				CreateNoWindow = true,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
			};
			psi.SetEnvironmentVariable ("JAVA_HOME", JavaSdkPath);
			if (Directory.Exists (ProjectDirectory)) {
				psi.WorkingDirectory = ProjectDirectory;
			}

			procOutput.AppendLine ($"Running: {psi.FileName} {psi.Arguments}");
			try {
				var result = Process.RunAndCaptureText (psi, TimeSpan.FromMinutes (5));
				procOutput.AppendLine (result.StandardOutput);
				procOutput.AppendLine (result.StandardError);
				succeeded = result.ExitStatus.ExitCode == 0;
				procOutput.AppendLine ($"Exit Code: {result.ExitStatus.ExitCode}");
			} catch (TimeoutException) {
				succeeded = false;
				procOutput.AppendLine ("Process timed out after 5 minutes.");
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
