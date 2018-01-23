using System;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.IO;
using System.Diagnostics;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	public class AdjustJavacVersionArguments : Task
	{
		[Required]
		public string JdkVersion { get; set; }

		[Required]
		public string DefaultJdkVersion { get; set; }

		public bool EnableProguard { get; set; }

		public bool EnableMultiDex { get; set; }

		public bool SkipJavacVersionCheck { get; set; }

		[Output]
		public string TargetVersion { get; set; }

		[Output]
		public string SourceVersion { get; set; }

		public override bool Execute ()
		{
			Log.LogDebugMessage ($"{nameof (DefaultJdkVersion)}: {DefaultJdkVersion}");
			Log.LogDebugMessage ("EnableProguard: {0}", EnableProguard);
			Log.LogDebugMessage ("EnableMultiDex: {0}", EnableMultiDex);
			Log.LogDebugMessage ($"{nameof (JdkVersion)}: {JdkVersion}");
			Log.LogDebugMessage ("SkipJavacVersionCheck: {0}", SkipJavacVersionCheck);

			if (JdkVersion.StartsWith ("9", StringComparison.OrdinalIgnoreCase)) {
				TargetVersion = SourceVersion = DefaultJdkVersion;
			}

			if (SkipJavacVersionCheck)
				return true;

			// so far only proguard matters.
			if (!EnableProguard && !EnableMultiDex)
				return true;

			if (JdkVersion.StartsWith ("1.8", StringComparison.OrdinalIgnoreCase)) {
				TargetVersion = SourceVersion = "1.7";
				Log.LogDebugMessage ("Javac TargetVersion adjusted to: {0}", TargetVersion);
				Log.LogDebugMessage ("Javac SourceVersion adjusted to: {0}", SourceVersion);
			}

			return !Log.HasLoggedErrors;
		}
	}
}

