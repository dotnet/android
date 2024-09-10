using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Microsoft.Android.Build.Tasks;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	public class Gradle : AndroidToolTask
	{
		public override string TaskPrefix => "GRDL";

		[Required]
		public string Command { get; set; } = string.Empty;

		public string Arguments { get; set; } = string.Empty;

		public string ModuleName { get; set; } = string.Empty;

		public string OutputPath { get; set; } = string.Empty;

		public string IntermediateOutputPath { get; set; } = string.Empty;

		public string AndroidSdkDirectory { get; set; } = string.Empty;

		public string JavaSdkDirectory { get; set; } = string.Empty;

		string InitScriptPath => Path.Combine (IntermediateOutputPath, "net.android.init.gradle.kts");

		protected override string ToolName => OS.IsWindows ? "gradlew.bat" : "gradlew";

		protected override string GetWorkingDirectory ()
		{
			return ToolPath;
		}

		protected override string GenerateFullPathToTool ()
		{
			return Path.Combine (ToolPath, ToolExe);
		}

		protected override string GenerateCommandLineCommands ()
		{
			var cmd = new CommandLineBuilder ();

			var commandArg = Command;
			if (!string.IsNullOrEmpty (ModuleName)) {
				commandArg = $"{ModuleName}:{commandArg}";
			}
			cmd.AppendSwitch (commandArg);

			// If an output path is specified, use init script to set the gradle projects build directory to that path
			if (!string.IsNullOrEmpty (OutputPath)) {
				cmd.AppendSwitchIfNotNull ("-P", $"netAndroidBuildDirOverride={OutputPath}");
				cmd.AppendSwitchIfNotNull ("--init-script ", InitScriptPath);
			}

			if (!string.IsNullOrEmpty (Arguments))
				cmd.AppendSwitch (Arguments);

			// Do not leave gradle daemon running
			cmd.AppendSwitch ("--no-daemon");

			return cmd.ToString ();
		}

		protected override ProcessStartInfo GetProcessStartInfo (string pathToTool, string commandLineCommands, string responseFileSwitch)
		{
			ProcessStartInfo psi = base.GetProcessStartInfo (pathToTool, commandLineCommands, responseFileSwitch);
			if (Directory.Exists (AndroidSdkDirectory))
				psi.Environment["ANDROID_HOME"] = AndroidSdkDirectory;

			if (Directory.Exists (JavaSdkDirectory))
				psi.Environment["JAVA_HOME"] = JavaSdkDirectory;

			return psi;
		}

		public override bool RunTask ()
		{
			if (string.IsNullOrEmpty (ToolPath) || !File.Exists (GenerateFullPathToTool ())) {
				Log.LogCodedError ("XAGRDL1000", Properties.Resources.XAGRDL1000, ToolPath ?? string.Empty);
				return false;
			}

			if (!string.IsNullOrEmpty (OutputPath)) {
				Files.CopyIfStringChanged (init_script_content, InitScriptPath);
			}

			return base.RunTask ();;
		}

		const string init_script_content = @"
gradle.projectsLoaded {
	if (gradle.startParameter.projectProperties.containsKey(""netAndroidBuildDirOverride"")) {
		val customBuildDir = gradle.startParameter.projectProperties[""netAndroidBuildDirOverride""]
		rootProject.allprojects {
			afterEvaluate {
				layout.buildDirectory.set(file(customBuildDir))
			}
		}
	}
}
";

	}
}
