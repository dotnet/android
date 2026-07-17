using NUnit.Framework;
using System;
using System.IO;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class NativeLoggingTests : BaseTest
	{
		[Test]
		public void PrintfLoggingForwardsArgumentsAndSkipsDisabledCategories ()
		{
			if (IsWindows) {
				Assert.Ignore ("The native logging test requires a host C++ compiler.");
			}

			var testDirectory = Path.Combine (XABuildPaths.TopDirectory, "src", "Xamarin.Android.Build.Tasks", "Tests", "Xamarin.Android.Build.Tests", "Resources", "NativeLogging");
			var nativeDirectory = Path.Combine (XABuildPaths.TopDirectory, "src", "native");
			var outputDirectory = Path.Combine (Root, TestName);
			var output = Path.Combine (outputDirectory, "native-logging-tests");
			Directory.CreateDirectory (outputDirectory);

			var compiler = Environment.GetEnvironmentVariable ("CXX");
			if (compiler.IsNullOrEmpty ()) {
				compiler = "clang++";
			}

			var arguments = string.Join (" ", new [] {
				"-std=c++23",
				"-Wall",
				"-Wextra",
				"-Werror",
				$"-I\"{Path.Combine (testDirectory, "include")}\"",
				$"-I\"{Path.Combine (nativeDirectory, "common", "include")}\"",
				$"-I\"{Path.Combine (nativeDirectory, "clr", "include")}\"",
				$"-I\"{Path.Combine (XABuildPaths.TopDirectory, "external", "Java.Interop", "src", "java-interop")}\"",
				$"\"{Path.Combine (testDirectory, "log-functions-tests.cc")}\"",
				$"\"{Path.Combine (nativeDirectory, "clr", "shared", "log_functions.cc")}\"",
				$"-o \"{output}\"",
			});

			var (compileExitCode, compileOutput, compileError) = RunProcessWithExitCode (compiler, arguments);
			Assert.That (compileExitCode, Is.EqualTo (0), $"{compileOutput}{Environment.NewLine}{compileError}");

			var (testExitCode, testOutput, testError) = RunProcessWithExitCode (output, "");
			Assert.That (testExitCode, Is.EqualTo (0), $"{testOutput}{Environment.NewLine}{testError}");
		}
	}
}
