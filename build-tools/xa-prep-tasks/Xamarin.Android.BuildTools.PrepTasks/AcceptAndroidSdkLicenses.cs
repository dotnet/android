using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.BuildTools.PrepTasks
{
	public class AcceptAndroidSdkLicenses : Task
	{
		[Required]
		public string AndroidSdkDirectory { get; set; }

		public string JavaSdkDirectory { get; set; }

		public override bool Execute ()
		{
			Log.LogMessage (MessageImportance.Low, $"Task {nameof (AcceptAndroidSdkLicenses)}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (AndroidSdkDirectory)}: {AndroidSdkDirectory}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (JavaSdkDirectory)}: {JavaSdkDirectory}");

			var licdir = Path.Combine (Path.Combine (AndroidSdkDirectory, "licenses"));
			Directory.CreateDirectory (licdir);

			if (!string.IsNullOrEmpty (JavaSdkDirectory)) {
				Environment.SetEnvironmentVariable ("JAVA_HOME", JavaSdkDirectory);
			}

			string _;
			var path = Which.GetProgramLocation ("sdkmanager", out _, new [] { Path.Combine (AndroidSdkDirectory, "tools", "bin") });
			var psi = new ProcessStartInfo (path, "--licenses") { UseShellExecute = false, RedirectStandardInput = true };
			var proc = Process.Start (psi);
			for (int i = 0; i < 10; i++)
				proc.StandardInput.WriteLine ('y');
			proc.WaitForExit ();

			return true;
		}
	}
}
