using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class AndroidDotnetToolTests : BaseTest
	{
		MockBuildEngine engine;
		List<BuildErrorEventArgs> errors;
		List<BuildWarningEventArgs> warnings;
		List<BuildMessageEventArgs> messages;

		[SetUp]
		public void Setup ()
		{
			engine = new MockBuildEngine (TestContext.Out,
				errors: errors = new List<BuildErrorEventArgs> (),
				warnings: warnings = new List<BuildWarningEventArgs> (),
				messages: messages = new List<BuildMessageEventArgs> ());
		}

		[Test]
		public void ShouldUseFullToolPath ()
		{
			var dotnetDir = TestEnvironment.DotNetPreviewDirectory;
			var dotnetPath = Path.Combine (dotnetDir, (TestEnvironment.IsWindows ? "dotnet.exe" : "dotnet"));
			var classParseTask = new ClassParseTestTask {
				BuildEngine = engine,
				NetCoreRoot = dotnetDir,
				ToolPath = TestEnvironment.AndroidMSBuildDirectory,
				ToolExe = "class-parse.dll",
			};

			Assert.True (classParseTask.Execute (), "Task should have succeeded.");
			Assert.IsTrue (messages.Any (m => m.Message.StartsWith (dotnetPath)), "Task did not use expected tool path.");
		}

		[Test]
		public void ClassParseWritesReferenceArguments ()
		{
			var outputDirectory = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (outputDirectory);
			TestOutputDirectories [TestContext.CurrentContext.Test.ID] = outputDirectory;
			var output = Path.Combine (outputDirectory, "api.xml");
			var referenceOutput = Path.Combine (outputDirectory, "reference-api.xml");
			var sourceJar = Path.Combine (outputDirectory, "source.jar");
			var referenceJar = Path.Combine (outputDirectory, "reference.jar");
			var task = new ClassParseArgumentsTestTask {
				BuildEngine = engine,
				OutputFile = output,
				ReferenceOutputFile = referenceOutput,
				ReferenceJars = [new TaskItem (referenceJar)],
				SourceJars = [new TaskItem (sourceJar)],
			};

			task.GenerateArguments ();

			var responseFile = File.ReadAllLines (Path.Combine (outputDirectory, "class-parse.rsp"));
			Assert.That (responseFile, Does.Contain ($"--reference-output=\"{referenceOutput}\""));
			Assert.That (responseFile, Does.Contain ($"--reference=\"{referenceJar}\""));
			Assert.That (responseFile, Does.Contain ($"\"{sourceJar}\""));
		}
	}

	public class ClassParseTestTask : AndroidDotnetToolTask
	{
		public override string TaskPrefix => "TEST";
		protected override string GenerateCommandLineCommands ()
		{
			return GetCommandLineBuilder ().ToString ();
		}
	}

	public class ClassParseArgumentsTestTask : ClassParse
	{
		public string GenerateArguments ()
		{
			return GenerateCommandLineCommands ();
		}
	}
}
