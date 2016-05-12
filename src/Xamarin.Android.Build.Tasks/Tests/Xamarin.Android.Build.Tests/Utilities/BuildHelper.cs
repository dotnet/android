using System;
using Microsoft.Build.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	public static class BuildHelper
	{
		// In general here I don't make use of IDisposable pattern for ProjectBuilder because
		// in case it failed I rather want to preserve the files to investigate.

		public static ProjectBuilder CreateApkBuilder (string directory, bool cleanupAfterSuccessfulBuild = false, bool cleanupOnDispose = true)
		{
			var ret = CreateDllBuilder (directory, cleanupAfterSuccessfulBuild, cleanupOnDispose);
			ret.Target = "SignAndroidPackage";
			return ret;
		}

		public static ProjectBuilder CreateDllBuilder (string directory, bool cleanupAfterSuccessfulBuild = false, bool cleanupOnDispose = true)
		{
			return new ProjectBuilder (directory) {
				CleanupAfterSuccessfulBuild = cleanupAfterSuccessfulBuild,
				CleanupOnDispose = cleanupOnDispose,
				Verbosity = LoggerVerbosity.Diagnostic
			};
		}
	}
}

