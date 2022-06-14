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
	[Category ("Node-5")]
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
				ToolExe = Builder.UseDotNet ? "class-parse.dll" : "class-parse.exe",
			};

			Assert.True (classParseTask.Execute (), "Task should have succeeded.");
			var expectedTool = Builder.UseDotNet ? dotnetPath : Path.Combine (TestEnvironment.AndroidMSBuildDirectory, "class-parse.exe");
			Assert.IsTrue (messages.Any (m => m.Message.StartsWith (expectedTool)), "Task did not use expected tool path.");
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
}
