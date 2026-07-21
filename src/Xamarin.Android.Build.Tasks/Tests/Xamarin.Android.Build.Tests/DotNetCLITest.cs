using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[Parallelizable (ParallelScope.Children)]
	public class DotNetCLITest : BaseTest
	{
		sealed class ScriptDotNetCLI : DotNetCLI
		{
			readonly string scriptPath;
			readonly string scriptArguments;

			public ScriptDotNetCLI (string projectDirectory, string scriptPath, string scriptArguments)
				: base (Path.Combine (projectDirectory, "dummy.csproj"))
			{
				this.scriptPath = scriptPath;
				this.scriptArguments = scriptArguments;
				ProjectDirectory = projectDirectory;
			}

			public bool Run ()
			{
				return Execute ("build");
			}

			protected override Process ExecuteProcess (string [] args, string workingDirectory = null)
			{
				var process = new Process ();
				process.StartInfo.FileName = scriptPath;
				process.StartInfo.Arguments = scriptArguments;
				process.StartInfo.CreateNoWindow = true;
				process.StartInfo.UseShellExecute = false;
				process.StartInfo.RedirectStandardOutput = true;
				process.StartInfo.RedirectStandardError = true;
				process.StartInfo.WorkingDirectory = ProjectDirectory;
				process.Start ();
				return process;
			}
		}

		[Test]
		public void Execute_DrainsAsyncProcessOutput ()
		{
			var projectDirectory = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (projectDirectory);

			var scriptPath = Path.Combine (projectDirectory, IsWindows ? "emit-output.cmd" : "emit-output.sh");
			CreateOutputScript (scriptPath);
			var cli = new ScriptDotNetCLI (
				projectDirectory: projectDirectory,
				scriptPath: IsWindows ? scriptPath : "sh",
				scriptArguments: IsWindows ? "" : $"\"{scriptPath}\"");

			Assert.IsTrue (cli.Run (), "Script process should succeed.");

			var processLog = File.ReadAllText (cli.ProcessLogFile);
			StringAssert.Contains ("\"Done\":\"yes\"}}", processLog, "Expected complete JSON tail in process log.");
			StringAssert.Contains ("STDERR_DONE", processLog, "Expected stderr tail marker in process log.");
		}

		void CreateOutputScript (string scriptPath)
		{
			var script = new StringBuilder ();
			if (IsWindows) {
				script.AppendLine ("@echo off");
				script.AppendLine ("echo {\"TargetResults\":{");
				script.AppendLine ("for /L %%i in (1,1,25000) do @echo \"\"L%%i\"\":\"\"V%%i\"\",");
				script.AppendLine ("echo \"Done\":\"yes\"}}");
				script.AppendLine ("echo STDERR_DONE 1>&2");
			} else {
				script.AppendLine ("#!/bin/sh");
				script.AppendLine ("echo '{\"TargetResults\":{'");
				script.AppendLine ("i=1");
				script.AppendLine ("while [ \"$i\" -le 25000 ]; do");
				script.AppendLine ("  echo \"\\\"L$i\\\":\\\"V$i\\\",\"");
				script.AppendLine ("  i=$((i+1))");
				script.AppendLine ("done");
				script.AppendLine ("echo '\"Done\":\"yes\"}}'");
				script.AppendLine ("echo STDERR_DONE 1>&2");
			}

			File.WriteAllText (scriptPath, script.ToString ());
			if (!IsWindows) {
				RunProcess ("chmod", $"u+x \"{scriptPath}\"");
			}
		}
	}
}
