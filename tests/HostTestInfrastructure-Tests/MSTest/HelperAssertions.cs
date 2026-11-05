using System;
using System.Collections;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Xamarin.Android.Build.Tests;

static partial class EnvironmentHelperAssertions
{
	internal static partial void AreEqual (object expected, object actual, string message)
	{
		// The parser compares Int32 lengths/version constants with UInt32/UInt16 fields.
		if (expected is int && (actual is uint || actual is ushort)) {
			Assert.AreEqual (Convert.ToInt64 (expected), Convert.ToInt64 (actual), message);
		} else {
			Assert.AreEqual (expected, actual, message);
		}
	}

	internal static partial void Greater (IComparable actual, IComparable expected) => Assert.IsTrue (actual.CompareTo (expected) > 0);
	internal static partial void GreaterOrEqual (IComparable actual, IComparable expected) => Assert.IsTrue (actual.CompareTo (expected) >= 0);
	internal static partial void Less (IComparable actual, IComparable expected) => Assert.IsTrue (actual.CompareTo (expected) < 0);
	internal static partial void IsNotEmpty (IEnumerable actual, string message) => Assert.IsTrue (actual.Cast<object> ().Any (), message);
	internal static partial void IsNotEmpty (string actual, string message) => Assert.IsFalse (string.IsNullOrEmpty (actual), message);
}

static partial class NativeToolTestHelper
{
	private static partial void AssertDirectoryExists (string path, string message) => Assert.IsTrue (Directory.Exists (path), message);
	private static partial void AssertFileExists (string path, string message) => Assert.IsTrue (File.Exists (path), message);
	private static partial void AssertExitCode (int exitCode, string message) => Assert.AreEqual (0, exitCode, message);
}
