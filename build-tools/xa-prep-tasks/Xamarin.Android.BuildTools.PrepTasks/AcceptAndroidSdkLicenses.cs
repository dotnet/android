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

		public override bool Execute ()
		{
			var licdir = Path.Combine (Path.Combine (AndroidSdkDirectory, "licenses"));
			Directory.CreateDirectory (licdir);

			var psi = new ProcessStartInfo (Path.Combine (AndroidSdkDirectory, "tools", "bin", "sdkmanager"), "--licenses") { UseShellExecute = false, RedirectStandardInput = true };
			var proc = Process.Start (psi);
			for (int i = 0; i < 10; i++)
				proc.StandardInput.WriteLine ('y');
			proc.WaitForExit ();

			return true;
		}
	}
}
