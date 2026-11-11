using System;
using Microsoft.Build.Framework;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	[TestClass]
	[DoNotParallelize]
	public class CopyResourceTests
	{
		string tempFile;
		List<BuildErrorEventArgs> errors;
		List<BuildMessageEventArgs> messages;
		MockBuildEngine engine;

		[TestInitialize]
		public void Setup ()
		{
			tempFile = Path.GetTempFileName ();
			engine = new MockBuildEngine (Console.Out,
				errors: errors = new List<BuildErrorEventArgs> (),
				messages: messages = new List<BuildMessageEventArgs> ());
		}

		[TestCleanup]
		public void TearDown ()
		{
			File.Delete (tempFile);
		}

		// If we remove one of these, this test should fail
		static object [] [] EmbeddedResources = new object [] [] {
			new object[] { "MonoRuntimeProvider.Bundled.java" },
		};

		[TestMethod]
		[DynamicData (nameof (EmbeddedResources))]
		public void FilesThatAreExpected (string resourceName)
		{
			var task = new CopyResource {
				BuildEngine = engine,
				ResourceName = resourceName,
				OutputPath = tempFile,
			};
			Assert.IsTrue (task.Execute (), "task should succeed!");
			Assert.IsTrue (File.Exists (tempFile));
			Assert.AreNotEqual (0, new FileInfo (tempFile).Length, "file should be non-empty!");
		}

		[TestMethod]
		[DataRow ("thisdoesnotexist")]
		[DataRow ("ApplicationRegistration.java")]
		public void FileThatDoesNotExist (string resourceName)
		{
			var task = new CopyResource {
				BuildEngine = engine,
				ResourceName = resourceName,
				OutputPath = tempFile,
			};
			Assert.IsFalse (task.Execute (), "task should fail!");
			Assert.AreEqual (1, errors.Count);
			var error = errors [0];
			Assert.AreEqual ("XA0116", error.Code);
			Assert.Contains (resourceName, error.Message);
		}
	}
}
