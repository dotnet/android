using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Build.Utilities;
using Xamarin.Android.Tools.Aidl;

namespace Xamarin.Android.Tasks
{
	public class ImportJavaDoc : ToolTask
	{
		public string [] JavaDocs { get; set; }
		
		public string [] References { get; set; }

		public string [] Transforms { get; set; }

		[Required]
		public string TargetAssembly { get; set; }

		[Required]
		public string OutputDocDirectory { get; set; }

		protected override string ToolName {
			get { return "javadoc-to-mdoc"; }
		}

		protected override string GenerateFullPathToTool ()
		{
			return MonoDroidSdk.JavaDocToMDocExe;
		}

		protected override string GenerateCommandLineCommands ()
		{
			if (!Directory.Exists (OutputDocDirectory))
				Directory.CreateDirectory (OutputDocDirectory);

			var cmd = new CommandLineBuilder ();
			cmd.AppendSwitch (Path.GetFullPath (TargetAssembly));
			//foreach (var r in References)
			//	cmd.AppendSwitch (r);
			cmd.AppendSwitch ("-v=2");
			cmd.AppendSwitchIfNotNull ("--out=", OutputDocDirectory);
			foreach (var j in JavaDocs)
				cmd.AppendSwitchIfNotNull ("--doc-dir=", Path.GetDirectoryName (j));
			foreach (var t in Transforms) {
				if (t.EndsWith ("Metadata.xml", StringComparison.InvariantCultureIgnoreCase))
					cmd.AppendSwitchIfNotNull ("--metadata=", t);
				else
					cmd.AppendSwitchIfNotNull ("--enum-map=", t);
			}
			return cmd.ToString ();
		}
	}
}
