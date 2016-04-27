using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Build.Utilities;
using Xamarin.Android.Tools.Aidl;

namespace Xamarin.Android.Tasks
{
	public class MDoc : ToolTask
	{
		public string [] References { get; set; }
		
		[Required]
		public string TargetAssembly { get; set; }

		[Required]
		public string OutputDocDirectory { get; set; }

		public bool RunExport { get; set; }

		protected override string ToolName {
			get { return OS.IsWindows ? "mdoc.exe" : "mdoc"; }
		}

		protected override string GenerateFullPathToTool ()
		{
			return Path.Combine (ToolPath, ToolExe);
		}
		
		protected override string GenerateCommandLineCommands ()
		{
			Log.LogDebugMessage ("MDoc");
			Log.LogDebugMessage ("  RunExport: {0}", RunExport);
			Log.LogDebugMessage ("  TargetAssembly: {0}", TargetAssembly);
			Log.LogDebugMessage ("  References");
			if (References != null)
				foreach (var reference in References)
					Log.LogDebugMessage ("    {0}", reference);
			Log.LogDebugMessage ("  OutputDocDirectory: {0}", OutputDocDirectory);
			if (RunExport) {
				var cmd = new CommandLineBuilder ();
				cmd.AppendSwitch ("--debug");
				cmd.AppendSwitch ("export-msxdoc");
				cmd.AppendSwitchIfNotNull ("-o", Path.ChangeExtension (TargetAssembly, ".xml"));
				cmd.AppendSwitch (OutputDocDirectory);
				return cmd.ToString ();
			} else {
				var refPaths = References.Select (Path.GetDirectoryName).Distinct ();
				var cmd = new CommandLineBuilder ();
				cmd.AppendSwitch ("--debug");
				cmd.AppendSwitch ("update");
				cmd.AppendSwitch ("--delete");
				cmd.AppendSwitchIfNotNull ("-L", Path.GetDirectoryName (TargetAssembly));
				foreach (var rp in refPaths)
					cmd.AppendSwitchIfNotNull ("-L", rp);
				cmd.AppendSwitchIfNotNull ("-o", OutputDocDirectory);
				cmd.AppendSwitch (Path.GetFullPath (TargetAssembly));
				return cmd.ToString ();
			}
		}
	}
}

