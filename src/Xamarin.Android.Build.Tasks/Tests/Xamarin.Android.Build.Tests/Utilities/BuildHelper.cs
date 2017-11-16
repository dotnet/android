using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Xamarin.ProjectTools;
using NUnit.Framework;

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

	public static class StringAssertEx {
		public static void DoesNotContain (string text, IEnumerable<string> collection, string message = null)
		{
			foreach (var line in collection) {
				if (line.Contains (text)) {
					Assert.Fail (message);
					return;
				}
			}
			Assert.Pass ();
		}

		public static void Contains (string text, IEnumerable<string> collection, string message = null)
		{
			foreach (var line in collection) {
				if (line.Contains (text))
				{
					Assert.Pass ();
					return;
				}
			}
			Assert.Fail (message);
		}

		public static bool ContainsText (this IEnumerable<string> collection, string expected) {
			foreach (var line in collection) {
				if (line.Contains (expected)) {
					return true;
				}
			}
			return false;
		}
	}
}

