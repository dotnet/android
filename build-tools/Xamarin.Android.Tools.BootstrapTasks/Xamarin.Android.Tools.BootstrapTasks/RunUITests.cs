using Microsoft.Build.Framework;
using System.Collections.Generic;
using System.IO;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	public class RunUITests : Adb
	{
		const                   int                 StateRunTests               = 0;
		const                   int                 StateGetLogcat              = 1;

		[Required]
		public                  string              Activity                    { get; set; }

		[Required]
		public                  string              LogcatFilename              { get; set; }

		protected virtual void AfterCommand (int commandIndex, CommandInfo info)
		{
			if (commandIndex != StateRunTests)
				return;

			Log.LogMessage (MessageImportance.Low, $"  going to wait for 15 seconds");
			System.Threading.Thread.Sleep (15000);
		}

		protected override List <CommandInfo> GenerateCommandArguments ()
		{
			return new List <CommandInfo> {
				new CommandInfo {
					ArgumentsString = $"{AdbTarget} {AdbOptions} shell am start -n \"{Activity}\"",
				},

				new CommandInfo {
					ArgumentsString = $"{AdbTarget} {AdbOptions} logcat -v threadtime -d",
					MergeStdoutAndStderr = false,
					StdoutFilePath = LogcatFilename,
					StdoutAppend = File.Exists (LogcatFilename)
				},
			};
		}
	}
}
