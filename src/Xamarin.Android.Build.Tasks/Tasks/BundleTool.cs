#nullable enable
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;

namespace Xamarin.Android.Tasks
{
	public abstract class BundleTool : JavaToolTask
	{
		[Required]
		public string JarPath { get; set; } = "";

		protected override string GenerateCommandLineCommands ()
		{
			return GetCommandLineBuilder ().ToString ();
		}

		internal virtual CommandLineBuilder GetCommandLineBuilder ()
		{
			var cmd = new CommandLineBuilder ();

			var javaOptions = GetJavaOptions (JavaOptions, JdkVersion);
			if (!javaOptions.IsNullOrEmpty ()) {
				cmd.AppendSwitch (javaOptions);
			}
			cmd.AppendSwitchIfNotNull ("-Xmx", JavaMaximumHeapSize);
			cmd.AppendSwitchIfNotNull ("-jar ", JarPath);

			return cmd;
		}
	}
}
