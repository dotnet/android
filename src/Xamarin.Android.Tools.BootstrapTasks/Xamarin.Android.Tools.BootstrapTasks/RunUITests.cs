using Microsoft.Build.Framework;
using System.IO;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	public class RunUITests : Adb
	{
		public                  string              AdbTarget                   { get; set; }
		public                  string              AdbOptions                  { get; set; }

		[Required]
		public                  string              Activity                    { get; set; }

		[Required]
		public                  string              LogcatFilename              { get; set; }

		protected   override    bool                LogTaskMessages {
			get { return false; }
		}

		bool getLogcat;
		TextWriter logcatWriter;


		public override bool Execute ()
		{
			Log.LogMessage (MessageImportance.Low, $"Task {nameof (RunUITests)}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (AdbTarget)}: {AdbTarget}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (AdbOptions)}: {AdbOptions}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (Activity)}: {Activity}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (LogcatFilename)}: {LogcatFilename}");

			base.Execute ();

			Log.LogMessage(MessageImportance.Low, $"  going to wait for 15 seconds");
			System.Threading.Thread.Sleep (15000);

			using (logcatWriter = File.Exists (LogcatFilename) ? File.AppendText (LogcatFilename) : File.CreateText (LogcatFilename)) {
				getLogcat = true;
				base.Execute ();
			}

			return !Log.HasLoggedErrors;
		}

		protected override string GenerateCommandLineCommands ()
		{
			return getLogcat ? $"{AdbTarget} {AdbOptions} logcat -v threadtime -d" : $"{AdbTarget} {AdbOptions} shell am start -n \"{Activity}\"";
		}

		protected override void LogEventsFromTextOutput (string singleLine, MessageImportance messageImportance)
		{
			if (getLogcat) {
				logcatWriter.WriteLine (singleLine);
				return;
			}

			base.LogEventsFromTextOutput (singleLine, messageImportance);
		}
	}
}
