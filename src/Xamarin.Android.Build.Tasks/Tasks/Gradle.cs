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

		public string BuildDirInitScriptPath { get; set; } = string.Empty;

		public string AndroidSdkDirectory { get; set; } = string.Empty;

		public string JavaSdkDirectory { get; set; } = string.Empty;


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
			// See src/Xamarin.Android.Build.Tasks/Resources/net.android.init.gradle.kts
			if (!string.IsNullOrEmpty (OutputPath) && File.Exists (BuildDirInitScriptPath)) {
				cmd.AppendSwitchIfNotNull ("-P", $"netAndroidBuildDirOverride={OutputPath}");
				cmd.AppendSwitchIfNotNull ("--init-script ", BuildDirInitScriptPath);
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

			return base.RunTask ();;
		}

	}
}
