using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Build.Framework;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class AdbTaskTests
{
	[TestCase (0, true)]
	[TestCase (7, false)]
	public void LegacyTaskCallsToolBoundaryOnceAndPreservesFailure (int exitCode, bool expected)
	{
		var executable = Environment.ProcessPath ?? throw new InvalidOperationException ();
		var task = new DispatchProbe {
			BuildEngine = new MockBuildEngine (TestContext.Out), Command = "version",
			ToolPath = Path.GetDirectoryName (executable), ToolExe = Path.GetFileName (executable),
			ExitCodeToReturn = exitCode,
		};
		Assert.AreEqual (expected, task.Execute ());
		Assert.AreEqual (1, task.Invocations);
		Assert.IsTrue (task.ToolInvoked);
		Assert.AreEqual ("captured output", task.Output);
	}

	sealed class DispatchProbe : Adb
	{
		public int ExitCodeToReturn { get; set; }
		public int Invocations { get; private set; }
		public bool ToolInvoked { get; private set; }

		public override bool RunTask ()
		{
			if (++Invocations > 1)
				throw new InvalidOperationException ("Recursive dispatch must not reach a second task invocation.");
			return base.RunTask ();
		}

		protected override int ExecuteTool (string pathToTool, string responseFileCommands, string commandLineCommands)
		{
			ToolInvoked = true;
			LogEventsFromTextOutput ("captured output", MessageImportance.Normal);
			return ExitCodeToReturn;
		}
	}
}
