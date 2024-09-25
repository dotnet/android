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
				throw new FileNotFoundException ($"Gradle tool was found at {GradlePath}, please run the xaprepare 'AndroidTestDependencies' scenario.");
			}

			if (string.IsNullOrEmpty (ProcessLogFile)) {
				Directory.CreateDirectory (ProjectDirectory);
				ProcessLogFile = Path.Combine (ProjectDirectory, $"gradle{DateTime.Now.ToString ("yyyyMMddHHmmssff")}-process.log");
			}

			var procOutput = new StringBuilder ();
			bool succeeded;

			using (var p = new Process ()) {
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
					if (e.Data != null) {
						procOutput.AppendLine (e.Data);
					}
				};
				p.ErrorDataReceived += (sender, e) => {
					if (e.Data != null) {
						procOutput.AppendLine (e.Data);
					}
				};

				procOutput.AppendLine ($"Running: {p.StartInfo.FileName} {p.StartInfo.Arguments}");
				p.Start ();
				p.BeginOutputReadLine ();
				p.BeginErrorReadLine ();
				bool completed = p.WaitForExit ((int) new TimeSpan (0, 5, 0).TotalMilliseconds);
				succeeded = completed && p.ExitCode == 0;
				procOutput.AppendLine ($"Exit Code: {p.ExitCode}");
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
