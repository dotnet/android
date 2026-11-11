using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestClass]
	[DoNotParallelize]
	public class AndroidDotnetToolTests : BaseTest
	{
		MockBuildEngine engine;
		List<BuildErrorEventArgs> errors;
		List<BuildWarningEventArgs> warnings;
		List<BuildMessageEventArgs> messages;

		[TestInitialize]
		public void Setup ()
		{
			engine = new MockBuildEngine (Console.Out,
				errors: errors = new List<BuildErrorEventArgs> (),
				warnings: warnings = new List<BuildWarningEventArgs> (),
				messages: messages = new List<BuildMessageEventArgs> ());
		}

		[TestMethod]
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

			Assert.IsTrue (classParseTask.Execute (), "Task should have succeeded.");
			Assert.IsTrue (messages.Any (m => m.Message.StartsWith (dotnetPath)), "Task did not use expected tool path.");
		}

		[TestMethod]
		public void ClassParseWritesReferenceArguments ()
		{
			var outputDirectory = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (outputDirectory);
			TestOutputDirectories [TestId] = outputDirectory;
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
			Assert.Contains ($"--reference-output=\"{referenceOutput}\"", responseFile);
			Assert.Contains ($"--reference=\"{referenceJar}\"", responseFile);
			Assert.Contains ($"\"{sourceJar}\"", responseFile);
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
