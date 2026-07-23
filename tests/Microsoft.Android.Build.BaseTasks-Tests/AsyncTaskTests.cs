using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Android.Build.BaseTasks.Tests.Utilities;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using NUnit.Framework;

namespace Microsoft.Android.Build.BaseTasks.Tests
{
	[TestFixture]
	public class AsyncTaskTests
	{
		List<BuildErrorEventArgs> errors;
		List<BuildWarningEventArgs> warnings;
		List<BuildMessageEventArgs> messages;
		MockBuildEngine engine;

		[SetUp]
		public void TestSetup ()
		{
			errors = new List<BuildErrorEventArgs> ();
			warnings = new List<BuildWarningEventArgs> ();
			messages = new List<BuildMessageEventArgs> ();
			engine = new MockBuildEngine (TestContext.Out, errors, warnings, messages);
		}

		class AsyncTaskTest : AsyncTask
		{
			public override string TaskPrefix => "ATT";
		}

		public class AsyncMessage : AsyncTask
		{
			public override string TaskPrefix => "AM";

			public string Text { get; set; }

			public override bool Execute ()
			{
				LogTelemetry ("Test", new Dictionary<string, string> () { { "Property", "Value" } });
				return base.Execute ();
			}

			public override async Task RunTaskAsync ()
			{
				await Task.Delay (5000);
				LogMessage (Text);
				Complete ();
			}
		}

		class AsyncTaskExceptionTest : AsyncTask
		{
			public override string TaskPrefix => "ATET";

			public string ExceptionMessage { get; set; }

			public override Task RunTaskAsync ()
			{
				throw new System.InvalidOperationException (ExceptionMessage);
			}
		}


		[Test]
		public void RunAsyncTask ()
		{
			var task = new AsyncTaskTest () {
				BuildEngine = engine,
			};

			Assert.IsTrue (task.Execute (), "Empty AsyncTask should have ran successfully.");
		}

		[Test]
		public void RunAsyncTaskOverride ()
		{
			var message = "Hello Async World!";
			var task = new AsyncMessage () {
				BuildEngine = engine,
				Text = message,
			};
			var taskSucceeded = task.Execute ();
			Assert.IsTrue (messages.Any (e => e.Message.Contains (message)),
				$"Task did not contain expected message text: '{message}'.");
		}

		[Test]
		public void RunAsyncTaskExpectedException ()
		{
			var expectedException = "test exception!";
			var task = new AsyncTaskExceptionTest () {
				BuildEngine = engine,
				ExceptionMessage = expectedException,
			};

			Assert.IsFalse (task.Execute (), "Exception AsyncTask should have failed.");
			Assert.IsTrue (errors.Count == 1, "Exception AsyncTask should have produced one error.");
			Assert.IsTrue (errors[0].Message.Contains (expectedException),
				$"Task did not contain expected error text: '{expectedException}'.");
		}

	}
}
