using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Android.Build.BaseTasks.Tests.Utilities;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Android.Build.BaseTasks.Tests
{
	[TestClass]
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

		[TestMethod]
		[DataRow (true)]
		[DataRow (false)]
		public void MultithreadedTaskPreservesResult (bool result)
		{
			Assert.AreEqual (result, new TransformTask { Result = result }.Execute ());
		}

		[TestMethod]
		public void MultithreadedTaskPreservesUnhandledExceptionDiagnostic ()
		{
			var errors = new List<BuildErrorEventArgs> ();
			var task = new TransformTask {
				BuildEngine = new MockBuildEngine (TextWriter.Null, errors: errors),
				Action = () => throw new ArgumentException ("Invalid transform input."),
			};
			Assert.IsFalse (task.Execute ());
			Assert.AreEqual ("XATEST7004", errors [0].Code);
			StringAssert.Contains (errors [0].Message, "Invalid transform input.");
		}

		[TestMethod]
		[DataRow (false, false, "XATEST7024")]
		[DataRow (true, false, "XATEST7019")]
		[DataRow (false, true, "XATEST7024")]
		[DataRow (true, true, "XATEST7019")]
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
					StringAssert.StartsWith (errors [0].Message, "The file is locked by:");
					StringAssert.Matches (errors [0].Message, new System.Text.RegularExpressions.Regex (@"\d+"));
					StringAssert.EndsWith (errors [0].Message, error.ToString ());
				} else {
					Assert.AreEqual (error.ToString (), errors [0].Message);
				}
			} finally {
				File.Delete (path);
			}
		}
	}
}
