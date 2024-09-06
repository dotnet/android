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

		public bool ReferenceLibraryOutputs { get; set; } = false;

		public string AndroidSdkDirectory { get; set; } = string.Empty;

		public string JavaSdkDirectory { get; set; } = string.Empty;

		[Output]
		public ITaskItem[] AppOutputs { get; set; } = Array.Empty<ITaskItem>();

		[Output]
		public ITaskItem[] LibraryOutputs { get; set; } = Array.Empty<ITaskItem>();


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

			bool didTaskSucceed = base.RunTask ();
			if (didTaskSucceed)
				CollectOutputs ();
			return didTaskSucceed;
		}

		void CollectOutputs ()
		{
			string outDir = string.IsNullOrEmpty (OutputPath) ?  Path.Combine (ToolPath, ModuleName, "build") : OutputPath;
			string outputsDir = Path.Combine (outDir, "outputs");
			if (Directory.Exists (outputsDir)) {
				AppOutputs = Directory.EnumerateFiles (outputsDir, $"*{ModuleName}*.apk", SearchOption.AllDirectories).Select (apk => new TaskItem (apk)).ToArray ();
				LibraryOutputs = Directory.EnumerateFiles (outputsDir, $"*{ModuleName}*.aar", SearchOption.AllDirectories).Select (aar => new TaskItem (aar)).ToArray ();
			}
			string gradleDirName = Path.GetFileName (Path.GetDirectoryName (GenerateFullPathToTool ()));
			foreach (var apk in AppOutputs) {
				Log.LogMessage (MessageImportance.High, $"{gradleDirName} -> {apk}");
			}
			foreach (var lib in LibraryOutputs) {
				Log.LogMessage (MessageImportance.High, $"{gradleDirName} -> {lib}");
				if (ReferenceLibraryOutputs) {
					Log.LogMessage (Properties.Resources.ResourceManager.GetString ("XAGRDLRefLibraryOutputs", Properties.Resources.Culture), lib);
				}
			}
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
