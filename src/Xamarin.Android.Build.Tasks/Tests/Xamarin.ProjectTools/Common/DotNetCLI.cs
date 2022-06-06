using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Xamarin.ProjectTools
{
	public class DotNetCLI
	{
		public string BuildLogFile { get; set; }
		public string ProcessLogFile { get; set; }
		public string Verbosity { get; set; } = "n";
		public string AndroidSdkPath { get; set; } = AndroidSdkResolver.GetAndroidSdkPath ();
		public string JavaSdkPath { get; set; } = AndroidSdkResolver.GetJavaSdkPath ();

		public string ProjectDirectory { get; private set; }

		readonly XASdkProject project;
		readonly string projectOrSolution;

		public DotNetCLI (string projectOrSolution)
		{
			this.projectOrSolution = projectOrSolution;
			ProjectDirectory = Path.GetDirectoryName (projectOrSolution);
		}

		public DotNetCLI (XASdkProject project, string projectOrSolution)
			: this (projectOrSolution)
		{
			this.project = project;
		}

		/// <summary>
		/// Runs the `dotnet` tool with the specified arguments.
		/// </summary>
		/// <param name="args">command arguments</param>
		/// <returns>Whether or not the command succeeded.</returns>
		protected bool Execute (params string [] args)
		{
			if (string.IsNullOrEmpty (ProcessLogFile))
				ProcessLogFile = Path.Combine (XABuildPaths.TestOutputDirectory, $"dotnet{DateTime.Now.ToString ("yyyyMMddHHmmssff")}-process.log");

			var procOutput = new StringBuilder ();
			bool succeeded;

			using (var p = new Process ()) {
				p.StartInfo.FileName = Path.Combine (AndroidSdkResolver.GetDotNetPreviewPath (), "dotnet");
				p.StartInfo.Arguments = string.Join (" ", args);
				p.StartInfo.CreateNoWindow = true;
				p.StartInfo.UseShellExecute = false;
				p.StartInfo.RedirectStandardOutput = true;
				p.StartInfo.RedirectStandardError = true;
				p.StartInfo.SetEnvironmentVariable ("DOTNET_MULTILEVEL_LOOKUP", "0");
				// Ensure any variable alteration from DotNetXamarinProject.Construct is cleared.
				if (!Builder.UseDotNet && !TestEnvironment.IsWindows) {
					p.StartInfo.SetEnvironmentVariable ("MSBUILD_EXE_PATH", null);
				}

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
				bool completed = p.WaitForExit ((int) new TimeSpan (0, 15, 0).TotalMilliseconds);
				succeeded = completed && p.ExitCode == 0;
				procOutput.AppendLine ($"Exit Code: {p.ExitCode}");
			}

			File.WriteAllText (ProcessLogFile, procOutput.ToString ());
			return succeeded;
		}

		public bool New (string template, string output = null)
		{
			var arguments = new List<string> {
				"new",
				template,
				"--output", $"\"{output ?? ProjectDirectory}\"",
			};
			return Execute (arguments.ToArray ());
		}

		public bool Build (string target = null, string runtimeIdentifier = null, string [] parameters = null)
		{
			var arguments = GetDefaultCommandLineArgs ("build", target, runtimeIdentifier, parameters);
			return Execute (arguments.ToArray ());
		}

		public bool Pack (string target = null, string runtimeIdentifier = null, string [] parameters = null)
		{
			var arguments = GetDefaultCommandLineArgs ("pack", target, runtimeIdentifier, parameters);
			return Execute (arguments.ToArray ());
		}

		public bool Publish (string target = null, string runtimeIdentifier = null, string [] parameters = null)
		{
			var arguments = GetDefaultCommandLineArgs ("publish", target, runtimeIdentifier, parameters);
			return Execute (arguments.ToArray ());
		}

		public bool Run ()
		{
			string binlog = Path.Combine (Path.GetDirectoryName (projectOrSolution), "run.binlog");
			var arguments = new List<string> {
				"run",
				"--project", $"\"{projectOrSolution}\"",
				"--no-build",
				$"/bl:\"{binlog}\""
			};
			return Execute (arguments.ToArray ());
		}

		public IEnumerable<string> LastBuildOutput {
			get {
				if (!string.IsNullOrEmpty (BuildLogFile) && File.Exists (BuildLogFile)) {
					return File.ReadLines (BuildLogFile, Encoding.UTF8);
				}
				return Enumerable.Empty<string> ();
			}
		}

		public bool IsTargetSkipped (string target) => BuildOutput.IsTargetSkipped (LastBuildOutput, target);

		List<string> GetDefaultCommandLineArgs (string verb, string target = null, string runtimeIdentifier = null, string [] parameters = null)
		{
			string testDir = Path.GetDirectoryName (projectOrSolution);
			if (string.IsNullOrEmpty (ProcessLogFile))
				ProcessLogFile = Path.Combine (testDir, "process.log");

			if (string.IsNullOrEmpty (BuildLogFile))
				BuildLogFile = Path.Combine (testDir, "build.log");

			var binlog = string.IsNullOrEmpty (target) ? "msbuild" : target;
			var arguments = new List<string> {
				verb,
				$"\"{projectOrSolution}\"",
				"/noconsolelogger",
				$"/flp1:LogFile=\"{BuildLogFile}\";Encoding=UTF-8;Verbosity={Verbosity}",
				$"/bl:\"{Path.Combine (testDir, $"{binlog}.binlog")}\"",
				"/p:_DisableParallelAot=true",
			};
			if (!string.IsNullOrEmpty (target)) {
				arguments.Add ($"/t:{target}");
			}
			if (project != null) {
				arguments.Add ($"/p:Configuration={project.Configuration}");
			}
			if (Directory.Exists (AndroidSdkPath)) {
				arguments.Add ($"/p:AndroidSdkDirectory=\"{AndroidSdkPath}\"");
			}
			if (Directory.Exists (JavaSdkPath)) {
				arguments.Add ($"/p:JavaSdkDirectory=\"{JavaSdkPath}\"");
			}
			if (parameters != null) {
				foreach (var parameter in parameters) {
					arguments.Add ($"/p:{parameter}");
				}
			}
			if (!string.IsNullOrEmpty (runtimeIdentifier)) {
				// NOTE: that this one has to be -r, /r does not appear to work
				arguments.Add ("-r");
				arguments.Add (runtimeIdentifier);
			}
			return arguments;
		}
	}
}
