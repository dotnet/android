using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.IO;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Invokes `bundletool` to install an APK set to an attached device
	/// 
	/// Usage: bundletool install-apks --apks=foo.apks
	/// </summary>
	public class InstallApkSet : BundleTool
	{
		[Required]
		public string ApkSet { get; set; }

		[Required]
		public string AdbToolPath { get; set; }

		public string AdbToolExe { get; set; }

		public string AdbToolName => OS.IsWindows ? "adb.exe" : "adb";

		protected override CommandLineBuilder GetCommandLineBuilder ()
		{
			var adb = string.IsNullOrEmpty (AdbToolExe) ? AdbToolName : AdbToolExe;
			var cmd = base.GetCommandLineBuilder ();
			cmd.AppendSwitch ("install-apks");
			cmd.AppendSwitchIfNotNull ("--apks ", ApkSet);
			cmd.AppendSwitchIfNotNull ("--adb ", Path.Combine (AdbToolPath, adb));
			cmd.AppendSwitch ("--allow-downgrade");

			// --modules: List of modules to be installed, or "_ALL_" for all modules.
			// Defaults to modules installed during first install, i.e. not on-demand.
			// Xamarin.Android won't support on-demand modules yet.
			cmd.AppendSwitchIfNotNull ("--modules ", "_ALL_");
			
			return cmd;
		}
	}
}
