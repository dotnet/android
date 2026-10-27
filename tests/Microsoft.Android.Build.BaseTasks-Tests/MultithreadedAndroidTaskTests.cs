using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Android.Build.BaseTasks.Tests.Utilities;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using NUnit.Framework;

namespace Microsoft.Android.Build.BaseTasks.Tests
{
	[TestFixture]
	public class MultithreadedAndroidTaskTests
	{
		class TransformTask : MultithreadedAndroidTask
		{
			public override string TaskPrefix => "TEST";
			public bool Result { get; set; }
			public Action Action { get; set; }

			public override bool RunTask ()
			{
				Action?.Invoke ();
				return Result;
			}
		}

		[Test]
		[TestCase (true)]
		[TestCase (false)]
		public void MultithreadedTaskPreservesResult (bool result)
		{
			Assert.AreEqual (result, new TransformTask { Result = result }.Execute ());
		}

		[Test]
		public void MultithreadedTaskPreservesUnhandledExceptionDiagnostic ()
		{
			var errors = new List<BuildErrorEventArgs> ();
			var task = new TransformTask {
				BuildEngine = new MockBuildEngine (TextWriter.Null, errors: errors),
				Action = () => throw new ArgumentException ("Invalid transform input."),
			};
			Assert.IsFalse (task.Execute ());
			Assert.AreEqual ("XATEST7004", errors [0].Code);
			StringAssert.Contains ("Invalid transform input.", errors [0].Message);
		}

		[TestCase (false, false, "XATEST7024")]
		[TestCase (true, false, "XATEST7019")]
		[TestCase (false, true, "XATEST7024")]
		[TestCase (true, true, "XATEST7019")]
		public void MultithreadedTaskPreservesFileLockDiagnostics (bool unauthorizedAccess, bool relativePath, string code)
		{
			string path = Path.GetTempFileName ();
			try {
				using var lockedFile = File.Create (path);
				string exceptionPath = relativePath ? Path.GetFileName (path) : path;
				if (relativePath) {
					Assert.AreNotEqual (Directory.GetCurrentDirectory (), Path.GetDirectoryName (path));
					Assert.IsFalse (File.Exists (exceptionPath), "The relative path must not resolve against the process directory.");
				}
				Exception error = unauthorizedAccess ?
					new UnauthorizedAccessException ($"Access to the path '{exceptionPath}' is denied.") :
					new IOException ($"The file '{exceptionPath}' is locked.");
				var errors = new List<BuildErrorEventArgs> ();
				var task = new TransformTask {
					BuildEngine = new MockBuildEngine (TextWriter.Null, errors: errors),
					TaskEnvironment = relativePath ?
						TaskEnvironment.CreateWithProjectDirectoryAndEnvironment (Path.GetDirectoryName (path), new Dictionary<string, string> ()) :
						TaskEnvironment.Fallback,
					Action = () => throw error,
				};
				Assert.IsFalse (task.Execute ());
				Assert.AreEqual (1, errors.Count);
				Assert.AreEqual (code, errors [0].Code);
				if (OperatingSystem.IsWindows ()) {
					StringAssert.StartsWith ("The file is locked by:", errors [0].Message);
					StringAssert.IsMatch (@"\d+", errors [0].Message);
					StringAssert.EndsWith (error.ToString (), errors [0].Message);
				} else {
					Assert.AreEqual (error.ToString (), errors [0].Message);
				}
			} finally {
				File.Delete (path);
			}
		}
	}
}
