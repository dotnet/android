using System;
using System.Linq;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Text;
using System.Collections.Generic;
using Xamarin.Android.Tools;
using Xamarin.Android.Tools.Aidl;

namespace Xamarin.Android.Tasks
{

	public class JavaDoc : JavaToolTask
	{
		public string [] SourceDirectories { get; set; }

		[Output]
		public string [] DestinationDirectories { get; set; }

		protected override string ToolName {
			get { return OS.IsWindows ? "javadoc.exe" : "javadoc"; }
		}

		public override bool Execute ()
		{
			Log.LogDebugMessage ("JavaDoc Task");
			Log.LogDebugTaskItems ("  SourceDirectories: ", SourceDirectories);
			Log.LogDebugTaskItems ("  DestinationDirectories: ", DestinationDirectories);

			foreach (var dir in DestinationDirectories)
				if (!Directory.Exists (dir))
					Directory.CreateDirectory (dir);

			bool retval = true;
			foreach (var pair in SourceDirectories.Zip (DestinationDirectories, (src, dst) => new { Source = src, Destination = dst })) {
				context_src = pair.Source;
				context_dst = pair.Destination;
				retval &= base.Execute ();
			}
			return retval;
		}

		string context_src;
		string context_dst;

		protected override string GenerateCommandLineCommands ()
		{
			var cmd = new CommandLineBuilder ();

			cmd.AppendSwitch ("-d");
			cmd.AppendFileNameIfNotNull (context_dst);
			cmd.AppendSwitch ("-sourcepath");
			cmd.AppendFileNameIfNotNull (context_src);
			cmd.AppendSwitch ("-subpackages");
			cmd.AppendSwitch (".");

			return cmd.ToString ();
		}
	}
	
}
