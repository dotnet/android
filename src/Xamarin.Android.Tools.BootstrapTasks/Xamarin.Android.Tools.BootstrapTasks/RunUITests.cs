using Microsoft.Build.Framework;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	public class RunUITests : Adb
	{
		public                  string              AdbTarget                   { get; set; }
		public                  string              AdbOptions                  { get; set; }

		[Required]
		public                  string              Activity                    { get; set; }

		public override bool Execute ()
		{
			Log.LogMessage (MessageImportance.Low, $"Task {nameof (RunUITests)}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (AdbTarget)}: {AdbTarget}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (AdbOptions)}: {AdbOptions}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (Activity)}: {Activity}");

			base.Execute ();

			Log.LogMessage(MessageImportance.Low, $"  going to wait for 15 seconds");
			System.Threading.Thread.Sleep (15000);

			return !Log.HasLoggedErrors;
		}

		protected override string GenerateCommandLineCommands ()
		{
			return $"{AdbTarget} {AdbOptions} shell am start -n \"{Activity}\"";
		}
	}
}
