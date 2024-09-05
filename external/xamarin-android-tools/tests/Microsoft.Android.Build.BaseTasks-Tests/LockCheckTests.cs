using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Android.Build.BaseTasks.Tests.Utilities;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;

namespace Microsoft.Android.Build.BaseTasks.Tests;

[TestFixture]
public class LockCheckTests
{
	string tempFile;
	Stream tempStream;
	List<BuildErrorEventArgs> errors;
	List<BuildWarningEventArgs> warnings;
	List<BuildMessageEventArgs> messages;
	MockBuildEngine engine;

	[SetUp]
	public void Setup ()
	{
		tempFile = Path.GetTempFileName ();
		tempStream = File.Create (tempFile);
		errors = new List<BuildErrorEventArgs> ();
		warnings = new List<BuildWarningEventArgs> ();
		messages = new List<BuildMessageEventArgs> ();
		engine = new MockBuildEngine (TestContext.Out, errors, warnings, messages);
	}

	[TearDown]
	public void TearDown ()
	{
		tempStream.Dispose ();
		if (File.Exists (tempFile))
			File.Delete (tempFile);
	}

	class MyTask : AndroidTask
	{
		public string Path { get; set; }

		public override string TaskPrefix => "MYT";

		public override bool RunTask ()
		{
			using var stream = File.Create (Path);
			return false;
		}
	}

	void AssertFileLocked (string actual)
	{
		Assert.IsNotEmpty (actual);
		StringAssert.StartsWith ("The file is locked by:", actual);
		StringAssert.IsMatch (@"\d+", actual, "Should contain a PID!");
	}

	[Test]
	public void LockCheck_FileLocked ()
	{
		string actual = LockCheck.GetLockedFileMessage (tempFile);
		if (OperatingSystem.IsWindows ()) {
			AssertFileLocked (actual);
		} else {
			Assert.IsEmpty (actual);
		}
	}

	[Test]
	public void LockCheck_AndroidTask ()
	{
		if (!OperatingSystem.IsWindows ())
			Assert.Ignore ("Test only valid on Windows");

		var task = new MyTask {
			BuildEngine = engine,
			Path = tempFile,
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
