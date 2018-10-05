using Microsoft.Build.Framework;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class CopyResourceTests
	{
		string tempFile;
		List<BuildErrorEventArgs> errors;
		List<BuildMessageEventArgs> messages;
		MockBuildEngine engine;

		[SetUp]
		public void Setup ()
		{
			tempFile = Path.GetTempFileName ();
			engine = new MockBuildEngine (TestContext.Out,
				errors: errors = new List<BuildErrorEventArgs> (),
				messages: messages = new List<BuildMessageEventArgs> ());
		}

		[TearDown]
		public void TearDown ()
		{
			File.Delete (tempFile);
		}

		// If we remove one of these, this test should fail
		static object [] EmbeddedResources = new object [] {
			new object[] { "machine.config" },
			new object[] { "MonoRuntimeProvider.Bundled.java" },
			new object[] { "NotifyTimeZoneChanges.java" },
			new object[] { "Seppuku.java" },
			new object[] { "IncrementalClassLoader.java" },
			new object[] { "MultiDexLoader.java" },
			new object[] { "Placeholder.java" },
	    		new object[] { "ResourcePatcher.java" },
			new object[] { "MonkeyPatcher.java" },
		};

		[Test]
		[TestCaseSource ("EmbeddedResources")]
		public void FilesThatAreExpected (string resourceName)
		{
			var task = new CopyResource {
				BuildEngine = engine,
				ResourceName = resourceName,
				OutputPath = tempFile,
			};
			Assert.IsTrue (task.Execute (), "task should succeed!");
			FileAssert.Exists (tempFile);
			Assert.AreNotEqual (0, new FileInfo (tempFile).Length, "file should be non-empty!");
		}

		[Test]
		public void FileThatDoesNotExist ()
		{
			var resourceName = "thisdoesnotexist";
			var task = new CopyResource {
				BuildEngine = engine,
				ResourceName = resourceName,
				OutputPath = tempFile,
			};
			Assert.IsFalse (task.Execute (), "task should fail!");
			Assert.AreEqual (1, errors.Count);
			var error = errors [0];
			Assert.AreEqual ("XA0116", error.Code);
			StringAssert.Contains (resourceName, error.Message);
		}
	}
}
