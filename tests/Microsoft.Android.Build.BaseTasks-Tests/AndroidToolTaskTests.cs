using System.IO;
using System.Collections.Generic;
using Microsoft.Android.Build.BaseTasks.Tests.Utilities;
using Microsoft.Android.Build.Tasks;
using NUnit.Framework;
using Microsoft.Build.Framework;
using NUnit.Framework.Internal;
using System.Linq;

namespace Microsoft.Android.Build.BaseTasks.Tests
{
	[TestFixture]
	public class AndroidToolTaskTests
	{
		List<BuildErrorEventArgs> errors;
		List<BuildWarningEventArgs> warnings;
		List<BuildMessageEventArgs> messages;
		MockBuildEngine engine;

		public class MyAndroidTask : AndroidTask
		{
			public override string TaskPrefix {get;} = "MAT";
			public string Key { get; set; }
			public string Value { get; set; }
			public bool ProjectSpecific { get; set; } = false;
			public override bool RunTask ()
			{
				var key = ProjectSpecific ? ProjectSpecificTaskObjectKey (Key) : (Key, (object)string.Empty);
				BuildEngine4.RegisterTaskObjectAssemblyLocal (key, Value, RegisteredTaskObjectLifetime.Build);
				return true;
			}
		}

		public class MyOtherAndroidTask : AndroidTask
		{
			public override string TaskPrefix {get;} = "MOAT";
			public string Key { get; set; }
			public bool ProjectSpecific { get; set; } = false;

			[Output]
			public string Value { get; set; }
			public override bool RunTask ()
			{
				var key = ProjectSpecific ? ProjectSpecificTaskObjectKey (Key) : (Key, (object)string.Empty);
				Value = BuildEngine4.GetRegisteredTaskObjectAssemblyLocal<string> (key, RegisteredTaskObjectLifetime.Build);
				return true;
			}
		}

		public class DotnetToolOutputTestTask : AndroidToolTask
		{
			public override string TaskPrefix {get;} = "DTOT";
			protected override string ToolName => "dotnet";
			protected override string GenerateFullPathToTool () => ToolExe;
			public string CommandLineArgs { get; set; } = "--info";
			protected override string GenerateCommandLineCommands () => CommandLineArgs;
		}

		[SetUp]
		public void TestSetup()
		{
			errors = new List<BuildErrorEventArgs> ();
			warnings = new List<BuildWarningEventArgs> ();
			messages = new List<BuildMessageEventArgs> ();
			engine = new MockBuildEngine (TestContext.Out, errors, warnings, messages);
		}

		[Test]
		[TestCase (true, true, true)]
		[TestCase (false, false, true)]
		[TestCase (true, false, false)]
		[TestCase (false, true, false)]
		public void TestRegisterTaskObjectCanRetrieveCorrectItem (bool projectSpecificA, bool projectSpecificB, bool expectedResult)
		{
			var task = new MyAndroidTask () {
				BuildEngine = engine,
				Key = "Foo",
				Value = "Foo",
				ProjectSpecific = projectSpecificA,
			};
			task.Execute ();
			var task2 = new MyOtherAndroidTask () {
				BuildEngine = engine,
				Key = "Foo",
				ProjectSpecific = projectSpecificB,
			};
			task2.Execute ();
			Assert.AreEqual (expectedResult, string.Compare (task2.Value, task.Value, ignoreCase: true) == 0);
		}

		[Test]
		[TestCase (true, true, false)]
		[TestCase (false, false, true)]
		[TestCase (true, false, false)]
		[TestCase (false, true, false)]
		public void TestRegisterTaskObjectFailsWhenDirectoryChanges (bool projectSpecificA, bool projectSpecificB, bool expectedResult)
		{
			MyAndroidTask task;
			var currentDir = Directory.GetCurrentDirectory ();
			Directory.SetCurrentDirectory (Path.Combine (currentDir, ".."));
			try {
				task = new MyAndroidTask () {
					BuildEngine = engine,
					Key = "Foo",
					Value = "Foo",
					ProjectSpecific = projectSpecificA,
				};
			} finally {
				Directory.SetCurrentDirectory (currentDir);
			}
			task.Execute ();
			var task2 = new MyOtherAndroidTask () {
				BuildEngine = engine,
				Key = "Foo",
				ProjectSpecific = projectSpecificB,
			};
			task2.Execute ();
			Assert.AreEqual (expectedResult, string.Compare (task2.Value, task.Value, ignoreCase: true) == 0);
		}

		[Test]
		[TestCase ("invalidcommand", false, "You intended to execute a .NET program, but dotnet-invalidcommand does not exist.")]
		[TestCase ("--info", true, "")]
		public void FailedAndroidToolTaskShouldLogOutputAsError (string args, bool expectedResult, string expectedErrorText)
		{
			var task = new DotnetToolOutputTestTask () {
				BuildEngine = engine,
				CommandLineArgs = args,
			};
			var taskSucceeded = task.Execute ();
			Assert.AreEqual (expectedResult, taskSucceeded, "Task execution did not return expected value.");

			if (taskSucceeded) {
				Assert.IsEmpty (errors, "Successful task should not have any errors.");
			} else {
				Assert.IsNotEmpty (errors, "Task expected to fail should have errors.");
				Assert.AreEqual ("MSB6006", errors [0].Code,
					$"Expected error code MSB6006 but got {errors [0].Code}");
				Assert.AreEqual ("XADTOT0000", errors [1].Code,
					$"Expected error code XADTOT0000 but got {errors [1].Code}");
				Assert.IsTrue (errors.Any (e => e.Message.Contains (expectedErrorText)),
					"Task expected to fail should contain expected error text.");
			}
		}
	}
}
