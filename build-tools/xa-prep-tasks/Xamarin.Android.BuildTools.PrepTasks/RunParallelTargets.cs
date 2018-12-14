using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.BuildTools.PrepTasks.Xamarin.Android.BuildTools.PrepTasks
{
	public class RunParallelTargets : Task
	{
		[Required]
		public ITaskItem        ProjectFile                             {get; set;}

		public string           Configuration                           {get; set;}

		public string           MSBuildBinPath                          {get; set;}

		public string           MSBuildBinaryLogParameterPrefix         {get; set;}

		public ITaskItem[]      Targets                                 {get; set;}

		public override bool Execute ()
		{
			if (Targets?.Length == 0)
				return true;

			var processes = new Process [Targets.Length];
			for (int i = 0; i < Targets.Length; ++i) {
				processes[i]  = CreateProcess (Targets[i].ItemSpec);
			}

			bool success  = true;
			for (int i = 0; i < processes.Length; ++i) {
				processes[i].WaitForExit ();
				if (processes[i].ExitCode != 0) {
					Log.LogError ($"Execution of target {Targets [i].ItemSpec} exited with code {processes [i].ExitCode}.");
					success = false;
				}
			}
			return success;
		}

		Process CreateProcess (string target)
		{
			var binlogOption  = "";
			if (!string.IsNullOrEmpty (MSBuildBinaryLogParameterPrefix)) {
				var date  = DateTime.Now.ToString ("yyyyMMddTHHmmss");
				// -$([System.DateTime]::Now.ToString ("yyyyMMddTHHmmss"))-Target-%(Identity).binlog
				binlogOption = $"{MSBuildBinaryLogParameterPrefix}-{date}-Target-{target}.binlog\"";
			}
			var config      = string.IsNullOrEmpty (Configuration) ? "" : $"/p:Configuration={Configuration}";
			var command     = $"{config} \"{ProjectFile.ItemSpec}\" {binlogOption} /t:{target}";
			var msbuild     = (string.IsNullOrEmpty (MSBuildBinPath) || Path.DirectorySeparatorChar == '/')
				? "msbuild"
				: Path.Combine (MSBuildBinPath, "msbuild");
			var parameters  = new ProcessStartInfo (msbuild, command) {
				CreateNoWindow    = true,
				UseShellExecute   = false,
				WindowStyle       = ProcessWindowStyle.Hidden,
			};
			var process     = Process.Start (parameters);
			return process;
		}
	}
}
