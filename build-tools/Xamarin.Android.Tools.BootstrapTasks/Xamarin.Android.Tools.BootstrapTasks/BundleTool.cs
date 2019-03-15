using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.IO;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	public class BundleTool : ToolTask
	{
		[Required]
		public string JarPath { get; set; }

		[Required]
		public string Arguments { get; set; }

		public string AdbToolPath { get; set; }

		public string AdbToolExe { get; set; }

		public string KeyStore { get; set; }

		public string KeyAlias { get; set; }

		public string KeyPass { get; set; }

		public string StorePass { get; set; }

		protected override string ToolName {
			get { return OS.IsWindows ? "java.exe" : "java"; }
		}

		protected override string GenerateFullPathToTool ()
		{
			return Path.Combine (ToolPath, ToolExe);
		}

		protected override string GenerateCommandLineCommands ()
		{
			var cmd = new CommandLineBuilder ();
			cmd.AppendSwitchIfNotNull ("-jar ", JarPath);
			cmd.AppendTextUnquoted (" " + Arguments);
			if (!string.IsNullOrEmpty (AdbToolPath)) {
				var adb = !string.IsNullOrEmpty (AdbToolExe) ? AdbToolExe : "adb";
				if (OS.IsWindows && !adb.EndsWith (".exe", StringComparison.OrdinalIgnoreCase)) {
					adb += ".exe";
				}
				cmd.AppendSwitchIfNotNull ("--adb ", Path.Combine (AdbToolPath, adb));
			}
			cmd.AppendSwitchIfNotNull ("--ks ", KeyStore);
			cmd.AppendSwitchIfNotNull ("--ks-key-alias ", KeyAlias);
			if (!string.IsNullOrEmpty (KeyPass))
				cmd.AppendSwitchIfNotNull ("--key-pass ", $"pass:{KeyPass}");
			if (!string.IsNullOrEmpty (StorePass))
				cmd.AppendSwitchIfNotNull ("--ks-pass ", $"pass:{StorePass}");
			return cmd.ToString ();
		}
	}
}
