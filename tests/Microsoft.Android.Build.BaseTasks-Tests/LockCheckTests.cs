using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Android.Build.BaseTasks.Tests.Utilities;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Android.Build.BaseTasks.Tests;

[TestClass]
public class LockCheckTests
{
	string tempFile;
	Stream tempStream;
	List<BuildErrorEventArgs> errors;
	List<BuildWarningEventArgs> warnings;
	List<BuildMessageEventArgs> messages;
	MockBuildEngine engine;

	[TestInitialize]
	public void Setup ()
	{
		tempFile = Path.GetTempFileName ();
		tempStream = File.Create (tempFile);
		errors = new List<BuildErrorEventArgs> ();
		warnings = new List<BuildWarningEventArgs> ();
		messages = new List<BuildMessageEventArgs> ();
		engine = new MockBuildEngine (System.Console.Out, errors, warnings, messages);
	}

	[TestCleanup]
	public void TearDown ()
	{
		tempStream.Dispose ();
		if (File.Exists (tempFile))
			File.Delete (tempFile);
	}

	class MyTask : AndroidTask
	{
		public override string TaskPrefix => "MYT";

		public Action Action { get; set; }

		public override bool RunTask ()
		{
			Action ();
			return false;
		}
	}

	void AssertFileLocked (string actual)
	{
		Assert.IsNotEmpty (actual);
		StringAssert.StartsWith (actual, "The file is locked by:");
		StringAssert.Matches (actual, new System.Text.RegularExpressions.Regex (@"\d+"), "Should contain a PID!");
	}

	[TestMethod]
	public void LockCheck_FileLocked ()
	{
		string actual = LockCheck.GetLockedFileMessage (tempFile);
		if (OperatingSystem.IsWindows ()) {
			AssertFileLocked (actual);
		} else {
			Assert.IsEmpty (actual);
		}
	}

	[TestMethod]
	public void LockCheck_AndroidTask_FileCreate () =>
		LockCheck_AndroidTask (() => File.Create (tempFile));

	[TestMethod]
	public void LockCheck_AndroidTask_FileDelete () =>
		LockCheck_AndroidTask (() => File.Delete (tempFile));

	[TestMethod]
	public void LockCheck_AndroidTask_UnauthorizedAccessException () =>
		LockCheck_AndroidTask (() => throw new UnauthorizedAccessException ($"Access to the path '{tempFile}' is denied."));

	void LockCheck_AndroidTask (Action action)
	{
		if (!OperatingSystem.IsWindows ())
			Assert.Inconclusive ("Test only valid on Windows");

		var task = new MyTask {
			BuildEngine = engine,
			Action = action,
		};
		task.Execute ();

		// error XAMYT7024: The file is locked by: "testhost (22040)"
		// System.IO.IOException: The process cannot access the file 'D:\temp\tmphkqpda.tmp' because it is being used by another process.
		//    at Microsoft.Win32.SafeHandles.SafeFileHandle.CreateFile (String fullPath, FileMode mode, FileAccess access, FileShare share, FileOptions options)
		// ... rest of stacktrace
		Assert.AreEqual (1, errors.Count);
		AssertFileLocked (errors [0].Message);
	}
}
