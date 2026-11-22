using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class BuilderTests
	{
		sealed class TestBuilder : Builder
		{
			public bool Build (string project, string target, params string [] parameters) =>
				BuildInternal (project, target, parameters);
		}

		static string GetBuildErrors (params string [] buildOutput)
		{
			var message = new StringBuilder ();
			Builder.AppendBuildErrors (message, buildOutput);
			return message.ToString ();
		}

		[TestCase (true, "build.log")]
		[TestCase (false, "install.log")]
		public void BuildLogDoesNotContainErrorsFromPreviousBuild (bool throwOnFailure, string logFile)
		{
			var directory = Path.Combine (Path.GetTempPath (), "BuilderTests-" + Guid.NewGuid ().ToString ("N"));
			try {
				var project = new DotNetStandard {
					ProjectName = "BuilderLog",
					Sdk = "Microsoft.NET.Sdk",
					TargetFramework = "netstandard2.0",
				};
				project.Populate (directory);
				var projectPath = Path.Combine (directory, project.ProjectFilePath);
				using var builder = new TestBuilder {
					AutomaticNuGetRestore = false,
					BuildLogFile = logFile,
					ThrowOnBuildFailure = true,
				};

				var previous = Assert.Throws<FailedBuildException> (() => builder.Build (projectPath, "MissingTarget"));
				StringAssert.Contains ("error MSB4057:", previous.Message);

				builder.ThrowOnBuildFailure = throwOnFailure;
				// This invalid property is rejected before MSBuild initializes its file logger.
				if (throwOnFailure) {
					var current = Assert.Throws<FailedBuildException> (() => builder.Build (projectPath, "Build", "=value"));
					StringAssert.Contains ("error MSB1006:", current.Message);
					StringAssert.DoesNotContain ("error MSB4057:", current.Message);
				} else {
					Assert.IsFalse (builder.Build (projectPath, "Build", "=value"));
				}

				var output = string.Join ("\n", builder.LastBuildOutput);
				StringAssert.Contains ("error MSB1006:", output);
				StringAssert.DoesNotContain ("error MSB4057:", output);
			} finally {
				if (Directory.Exists (directory))
					Directory.Delete (directory, recursive: true);
			}
		}

		[Test]
		public void AppendBuildErrorsReportsDistinctErrors ()
		{
			var message = GetBuildErrors (
				"  1>/tmp/App.csproj(10,5): error XA1234: Something failed [/tmp/App.csproj]",
				"/tmp/App.csproj(10,5): warning XA5678: Not an error [/tmp/App.csproj]",
				"/tmp/App.csproj(10,5): error XA1234: Something failed [/tmp/App.csproj]",
				"MSBUILD : error MSB1008: Only one project can be specified.",
				"clang: fatal error: linker command failed"
			);

			var lines = message.Split ('\n').Select (l => l.Trim ()).Where (l => l.Length > 0).ToArray ();
			CollectionAssert.AreEqual (new [] {
				"Build errors:",
				"/tmp/App.csproj(10,5): error XA1234: Something failed [/tmp/App.csproj]",
				"MSBUILD : error MSB1008: Only one project can be specified.",
				"clang: fatal error: linker command failed",
			}, lines);
		}

		[Test]
		public void AppendBuildErrorsCapsReportedErrors ()
		{
			var output = Enumerable.Range (1, 25).Select (i => $"Foo.cs(1,1): error CS{i:0000}: Error {i}").ToArray ();

			var message = GetBuildErrors (output);

			StringAssert.Contains ("error CS0020: Error 20", message);
			StringAssert.DoesNotContain ("error CS0021: Error 21", message);
			StringAssert.Contains ("... 5 more error(s) omitted.", message);
		}
	}
}
