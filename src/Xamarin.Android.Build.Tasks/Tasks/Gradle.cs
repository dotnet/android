#nullable enable
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
		public string Command { get; set; } = "";

		public string Arguments { get; set; } = "";

		public string ModuleName { get; set; } = "";

		public string OutputPath { get; set; } = "";

		public string BuildDirInitScriptPath { get; set; } = "";

		public string AndroidSdkDirectory { get; set; } = "";

		public string JavaSdkDirectory { get; set; } = "";


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
			if (!ModuleName.IsNullOrEmpty ()) {
				commandArg = $"{ModuleName}:{commandArg}";
			}
			cmd.AppendSwitch (commandArg);

			// If an output path is specified, use init script to set the gradle projects build directory to that path
			// See src/Xamarin.Android.Build.Tasks/Resources/net.android.init.gradle.kts
			if (!OutputPath.IsNullOrEmpty () && File.Exists (BuildDirInitScriptPath)) {
				cmd.AppendSwitchIfNotNull ("-P", $"netAndroidBuildDirOverride={OutputPath}");
				cmd.AppendSwitchIfNotNull ("--init-script ", BuildDirInitScriptPath);
			}

			if (!Arguments.IsNullOrEmpty ())
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
			if (ToolPath.IsNullOrEmpty () || !File.Exists (GenerateFullPathToTool ())) {
				Log.LogCodedError ("XAGRDL1000", Properties.Resources.XAGRDL1000, ToolPath ?? string.Empty);
				return false;
			}

			return base.RunTask ();;
		}

	}
}
