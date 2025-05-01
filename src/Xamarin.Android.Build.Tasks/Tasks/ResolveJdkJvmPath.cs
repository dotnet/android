using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.IO;
using System.Linq;
using Xamarin.Android.Tools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// This MSBuild task's job is to find $(JdkJvmPath) used by $(AndroidGenerateJniMarshalMethods)
	/// </summary>
	public class ResolveJdkJvmPath : AndroidTask
	{
		public override string TaskPrefix => "RJJ";

		public string? JavaSdkPath { get; set; }

		[Output]
		public string? JdkJvmPath { get; set; }

		[Required]
		public string MinimumSupportedJavaVersion   { get; set; } = "";

		[Required]
		public string LatestSupportedJavaVersion    { get; set; } = "";

		public override bool RunTask ()
		{
			try {
				JdkJvmPath = GetJvmPath ();
			} catch (Exception e) {
				Log.LogCodedError ("XA5300", $"Unable to find {nameof (JdkJvmPath)}{Environment.NewLine}{e}");
				return false;
			}

			if (string.IsNullOrEmpty (JdkJvmPath)) {
				Log.LogCodedError ("XA5300", $"{nameof (JdkJvmPath)} is blank");
				return false;
			}

			if (!File.Exists (JdkJvmPath)) {
				Log.LogCodedError ("XA5300", $"JdkJvmPath not found at {JdkJvmPath}");
				return false;
			}

			return !Log.HasLoggedErrors;
		}

		string? GetJvmPath ()
		{
			// NOTE: this doesn't need to use GetRegisteredTaskObjectAssemblyLocal()
			// because JavaSdkPath is the key and the value is a string.
			var key = new Tuple<string, string?> (nameof (ResolveJdkJvmPath), JavaSdkPath);
			var cached = BuildEngine4.GetRegisteredTaskObject (key, RegisteredTaskObjectLifetime.AppDomain) as string;
			if (cached != null) {
				Log.LogDebugMessage ($"Using cached value for {nameof (JdkJvmPath)}: {cached}");

				return cached;
			}

			var minVersion  = Version.Parse (MinimumSupportedJavaVersion);
			var maxVersion  = Version.Parse (LatestSupportedJavaVersion);

			JdkInfo? info    = MonoAndroidHelper.GetJdkInfo (this.CreateTaskLogger (), JavaSdkPath, minVersion, maxVersion);

			if (info == null)
				return null;

			var path = info.JdkJvmPath;
			if (string.IsNullOrEmpty (path))
				return null;

			BuildEngine4.RegisterTaskObject (key, path, RegisteredTaskObjectLifetime.AppDomain, allowEarlyCollection: false);

			return path;
		}
	}
}
