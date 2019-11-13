using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;

namespace Xamarin.Android.Tasks
{
	public class SetVsMonoAndroidRegistryKey : AndroidTask
	{
		public override string TaskPrefix => "SVM";

		[Required]
		public string InstallationID { get; set; }

		[Required]
		public string VisualStudioVersion { get; set; }

		const string EnvironmentVariable = "XAMARIN_ANDROID_REGKEY";

		public override bool RunTask ()
		{
			string value = $@"SOFTWARE\Xamarin\VisualStudio\{VisualStudioVersion}_{InstallationID}\Android";
			Log.LogDebugMessage ($"Setting %{EnvironmentVariable}%=\"{value}\"");
			Environment.SetEnvironmentVariable (EnvironmentVariable, value, EnvironmentVariableTarget.Process);
			return !Log.HasLoggedErrors;
		}
	}
}
