using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	public abstract class BundleToolAdbTask : BundleTool
	{
		/// <summary>
		/// This is used to detect the attached device and generate an APK set specifically for it
		/// </summary>
		[Required]
		public string AdbToolPath { get; set; }

		public string AdbToolExe { get; set; }

		public string AdbTarget { get; set; }

		public string AdbToolName => OS.IsWindows ? "adb.exe" : "adb";

		protected void AppendAdbOptions (CommandLineBuilder cmd)
		{
			var adb = string.IsNullOrEmpty (AdbToolExe) ? AdbToolName : AdbToolExe;
			cmd.AppendSwitchIfNotNull ("--adb ", Path.Combine (AdbToolPath, adb));

			var adbTarget = AdbTarget;
			if (!string.IsNullOrEmpty (adbTarget)) {
				// Normally of the form "-s emulator-5554"
				int index = adbTarget.IndexOf (' ');
				if (index != -1) {
					adbTarget = adbTarget.Substring (index + 1, adbTarget.Length - index - 1);
				}
				cmd.AppendSwitchIfNotNull ("--device-id ", adbTarget);
			}
		}
	}
}
