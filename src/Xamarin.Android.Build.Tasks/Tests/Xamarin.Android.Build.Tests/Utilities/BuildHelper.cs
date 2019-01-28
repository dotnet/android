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
			//NOTE: since $(BuildingInsideVisualStudio) is set, Build will not happen by default
			ret.Target = "Build,SignAndroidPackage";
			return ret;
		}

		public static ProjectBuilder CreateDllBuilder (string directory, bool cleanupAfterSuccessfulBuild = false, bool cleanupOnDispose = true)
		{
			return new ProjectBuilder (directory) {
				CleanupAfterSuccessfulBuild = cleanupAfterSuccessfulBuild,
				CleanupOnDispose = cleanupOnDispose,
				Verbosity = LoggerVerbosity.Diagnostic,
				Root = Xamarin.Android.Build.Paths.TestOutputDirectory,
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
			Assert.Fail (message ?? $"String did not contain '{text}'!");
		}

		public static bool ContainsText (this IEnumerable<string> collection, string expected) {
			foreach (var line in collection) {
				if (line.Contains (expected)) {
					return true;
				}
			}
			return false;
		}

		public static bool ContainsOccurances (this IEnumerable<string> collection, string expected, int count)
		{
			int found = 0;
			foreach (var line in collection) {
				if (line.Contains (expected)) {
					found++;
				}
			}
			return found == count;
		}
	}
}

