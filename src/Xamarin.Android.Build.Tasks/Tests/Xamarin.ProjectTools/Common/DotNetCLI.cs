using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Logging.StructuredLogger;

namespace Xamarin.ProjectTools
{
	public class DotNetCLI
	{
		public string BuildLogFile { get; set; } = "msbuild.binlog";
		public string ProcessLogFile { get; set; } = "process.log";
		public string AndroidSdkPath { get; set; } = AndroidSdkResolver.GetAndroidSdkPath ();
		public string AndroidNdkPath { get; set; } = AndroidSdkResolver.GetAndroidNdkPath ();
		public string JavaSdkPath { get; set; } = AndroidSdkResolver.GetJavaSdkPath ();

		public string ProjectDirectory { get; private set; }

		readonly XASdkProject project;
		readonly string projectOrSolution;
		string buildLogFullPath;

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
			string processLogFile = null;
			if (!string.IsNullOrEmpty (ProcessLogFile))
				processLogFile = Path.Combine (XABuildPaths.TestOutputDirectory, ProcessLogFile);

			var procOutput = new StringBuilder ();
			bool succeeded;

			using (var p = new Process ()) {
				p.StartInfo.FileName = Path.Combine (AndroidSdkResolver.GetDotNetPreviewPath (), "dotnet");
				p.StartInfo.Arguments = string.Join (" ", args);
				p.StartInfo.CreateNoWindow = true;
				p.StartInfo.UseShellExecute = false;
				p.StartInfo.RedirectStandardOutput = true;
				p.StartInfo.RedirectStandardError = true;
				// Ensure any variable alteration from DotNetXamarinProject.Construct is cleared.
				if (!Builder.UseDotNet && !TestEnvironment.IsWindows) {
					p.StartInfo.EnvironmentVariables ["MSBUILD_EXE_PATH"] = null;
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

			File.WriteAllText (processLogFile, procOutput.ToString ());
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

		public bool Build (string target = null, string [] parameters = null)
		{
			var arguments = GetDefaultCommandLineArgs ("build", target, parameters);
			return Execute (arguments.ToArray ());
		}

		public bool Pack (string target = null, string [] parameters = null)
		{
			var arguments = GetDefaultCommandLineArgs ("pack", target, parameters);
			return Execute (arguments.ToArray ());
		}

		public bool Publish (string target = null, string [] parameters = null)
		{
			var arguments = GetDefaultCommandLineArgs ("publish", target, parameters);
			return Execute (arguments.ToArray ());
		}

		public bool Run ()
		{
			var arguments = new List<string> {
				"run",
				"--project", $"\"{projectOrSolution}\"",
				
			};
			if (!string.IsNullOrEmpty (BuildLogFile)) {
				buildLogFullPath = Path.Combine (Path.GetDirectoryName (projectOrSolution), BuildLogFile);
				arguments.Add ($"/bl:\"{buildLogFullPath}\"");
			} else {
				buildLogFullPath = null;
			}
			return Execute (arguments.ToArray ());
		}

		public IEnumerable<string> LastBuildOutput {
			get {
				foreach (var node in Log?.FindChildrenRecursive<TreeNode> ()) {
					File.AppendAllText (@"C:\src\xamarin-android\bin\TestDebug\temp\DotNetBuildXamarinFormsFalse\foo.log", node.ToString ());
					yield return node.ToString ();
				}
			}
		}

		public Microsoft.Build.Logging.StructuredLogger.Build Log =>
			!string.IsNullOrEmpty (buildLogFullPath) && File.Exists (buildLogFullPath) ?
				BinaryLog.ReadBuild (buildLogFullPath) : null;

		public bool IsTargetSkipped (string target) => BuildOutput.IsTargetSkipped (LastBuildOutput, target);

		List<string> GetDefaultCommandLineArgs (string verb, string target = null, string [] parameters = null)
		{
			string testDir = Path.GetDirectoryName (projectOrSolution);

			var arguments = new List<string> {
				verb,
				$"\"{projectOrSolution}\"",
				"/noconsolelogger",
				$"/v:quiet",
			};
			if (!string.IsNullOrEmpty (BuildLogFile)) {
				buildLogFullPath = Path.Combine (testDir, BuildLogFile);
				arguments.Add ($"/bl:\"{buildLogFullPath}\"");
			} else {
				buildLogFullPath = null;
			}
			if (!string.IsNullOrEmpty (target)) {
				arguments.Add ($"/t:{target}");
			}
			if (project != null) {
				arguments.Add ($"/p:Configuration={project.Configuration}");
			}
			if (Directory.Exists (AndroidSdkPath)) {
				arguments.Add ($"/p:AndroidSdkDirectory=\"{AndroidSdkPath}\"");
			}
			if (Directory.Exists (AndroidNdkPath)) {
				arguments.Add ($"/p:AndroidNdkDirectory=\"{AndroidNdkPath}\"");
			}
			if (Directory.Exists (JavaSdkPath)) {
				arguments.Add ($"/p:JavaSdkDirectory=\"{JavaSdkPath}\"");
			}
			if (parameters != null) {
				foreach (var parameter in parameters) {
					arguments.Add ($"/p:{parameter}");
				}
			}
			return arguments;
		}
	}
}
