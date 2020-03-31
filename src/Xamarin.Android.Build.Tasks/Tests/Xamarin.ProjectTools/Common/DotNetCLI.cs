using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Xamarin.ProjectTools
{
	public class DotNetCLI
	{
		public string BuildLogFile { get; set; }
		public string ProcessLogFile { get; set; }
		public string Verbosity { get; set; } = "diag";
		public string AndroidSdkPath { get; set; } = AndroidSdkResolver.GetAndroidSdkPath ();
		public string AndroidNdkPath { get; set; } = AndroidSdkResolver.GetAndroidNdkPath ();

		const string Executable = "dotnet";
		readonly XASdkProject project;
		readonly string projectOrSolution;

		public DotNetCLI (XASdkProject project, string projectOrSolution)
		{
			this.project = project;
			this.projectOrSolution = projectOrSolution;
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
				p.StartInfo.FileName = Executable;
				p.StartInfo.Arguments = string.Join (" ", args);
				p.StartInfo.CreateNoWindow = true;
				p.StartInfo.UseShellExecute = false;
				p.StartInfo.RedirectStandardOutput = true;
				p.StartInfo.RedirectStandardError = true;

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

		public bool Build (string target = null)
		{
			var arguments = GetDefaultCommandLineArgs ("build", target);
			return Execute (arguments.ToArray ());
		}

		public bool Publish (bool restore = true)
		{
			var arguments = GetDefaultCommandLineArgs ("publish");
			arguments.Add ("/p:SelfContained=True");
			return Execute (arguments.ToArray ());
		}

		List<string> GetDefaultCommandLineArgs (string verb, string target = null)
		{
			string testDir = Path.GetDirectoryName (projectOrSolution);
			if (string.IsNullOrEmpty (ProcessLogFile))
				ProcessLogFile = Path.Combine (testDir, "process.log");

			if (string.IsNullOrEmpty (BuildLogFile))
				BuildLogFile = Path.Combine (testDir, "build.log");

			var arguments = new List<string> {
				verb,
				$"\"{projectOrSolution}\"",
				$"/p:Configuration={project.Configuration}",
				"/noconsolelogger",
				$"/flp1:LogFile=\"{BuildLogFile}\";Encoding=UTF-8;Verbosity={Verbosity}",
				$"/bl:\"{Path.Combine (testDir, "msbuild.binlog")}\""
			};
			if (!string.IsNullOrEmpty (target)) {
				arguments.Add ($"/t:{target}");
			}
			if (Directory.Exists (AndroidSdkPath)) {
				arguments.Add ($"/p:AndroidSdkDirectory=\"{AndroidSdkPath}\"");
			}
			if (Directory.Exists (AndroidNdkPath)) {
				arguments.Add ($"/p:AndroidNdkDirectory=\"{AndroidNdkPath}\"");
			}
			return arguments;
		}
	}
}
