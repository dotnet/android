using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.IO;
using System.Linq;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// This MSBuild task's job is to find $(JdkJvmPath) used by $(AndroidGenerateJniMarshalMethods)
	/// </summary>
	public class ResolveJdkJvmPath : Task
	{
		public string JavaSdkPath { get; set; }

		[Output]
		public string JdkJvmPath { get; set; }

		public override bool Execute ()
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

		string GetJvmPath ()
		{
			var key = new Tuple<string, string> (nameof (ResolveJdkJvmPath), JavaSdkPath);
			var cached = BuildEngine4.GetRegisteredTaskObject (key, RegisteredTaskObjectLifetime.AppDomain) as string;
			if (cached != null) {
				Log.LogDebugMessage ($"Using cached value for {nameof (JdkJvmPath)}: {cached}");

				return cached;
			}

			JdkInfo info = null;
			try {
				info = new JdkInfo (JavaSdkPath);
			} catch {
				info = JdkInfo.GetKnownSystemJdkInfos (this.CreateTaskLogger ()).FirstOrDefault ();
			}

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
