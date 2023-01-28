using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.IO;
using System.Text;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks {

	public class AndroidAdb : AndroidToolTask
	{
		public override string TaskPrefix => "AADB";

		public string AdbTarget { get; set; }
		public string Command { get; set; }
		public string Arguments { get; set; }

		public bool IgnoreErrors { get; set; } = false;

		[Output]
		public bool Result { get; set; } = true;

		protected override string ToolName => OS.IsWindows ? "adb.exe" : "adb";

		protected override string GenerateFullPathToTool ()
		{
			return Path.Combine (ToolPath, ToolExe);
		}

		//adb $(AdbTarget) uninstall -k &quot;$(_AndroidPackage)&quot;
		//adb $(AdbTarget) uninstall $(_AndroidPackage)
		//adb $(AdbTarget) install -r &quot;$(ApkFileSigned)&quot;
		//adb $(AdbTarget) shell cmd package uninstall -k $(_AndroidPackage)
		protected override string GenerateCommandLineCommands ()
		{
			var sb = new StringBuilder ();
			if (!string.IsNullOrEmpty (AdbTarget))
				sb.Append ($" {AdbTarget} ");
			sb.AppendFormat ("{0} {1}", Command, Arguments);
			return sb.ToString ();
		}

		protected override bool HandleTaskExecutionErrors ()
		{
			if (!Result)
				return true;
			return base.HandleTaskExecutionErrors ();
		}

		protected override void LogEventsFromTextOutput(string singleLine, MessageImportance messageImportance)
		{
			if (singleLine.Contains ("adb shell cmd package uninstall"))
				Result = false;
			base.LogEventsFromTextOutput (singleLine, messageImportance);
		}
	}
}
