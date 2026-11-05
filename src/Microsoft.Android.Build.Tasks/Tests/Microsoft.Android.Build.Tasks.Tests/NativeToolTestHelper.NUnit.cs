#nullable enable
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

static partial class NativeToolTestHelper
{
	private static partial void AssertDirectoryExists (string path, string message) => DirectoryAssert.Exists (path, message);
	private static partial void AssertFileExists (string path, string message) => FileAssert.Exists (path, message);
	private static partial void AssertExitCode (int exitCode, string message) => Assert.AreEqual (0, exitCode, message);
}
