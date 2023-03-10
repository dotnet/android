using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests {

	[TestFixture]
	[Parallelizable (ParallelScope.Self)]
	public class CheckDuplicateJavaLibrariesTests : BaseTest {
		List<BuildErrorEventArgs> errors;
		List<BuildWarningEventArgs> warnings;
		List<BuildMessageEventArgs> messages;
		MockBuildEngine engine;
		string path;

		[SetUp]
		public void Setup ()
		{
			engine = new MockBuildEngine (TestContext.Out,
				errors: errors = new List<BuildErrorEventArgs> (),
				warnings: warnings = new List<BuildWarningEventArgs> (),
				messages: messages = new List<BuildMessageEventArgs> ());

			path = Path.Combine (Root, "temp", TestName);
			TestOutputDirectories [TestContext.CurrentContext.Test.ID] = path;
		}

		[Test]
		public void XA1014ConflictingJarNames ()
		{
			Directory.CreateDirectory (path);
			string jar1 = Path.Combine (path, "library.jar");
			File.WriteAllText (jar1, "jar1");

			Directory.CreateDirectory (Path.Combine (path, "jar2"));
			string jar2 = Path.Combine (path, "jar2", "library.jar");
			File.WriteAllText (jar2, "jar2");

			var task = new CheckDuplicateJavaLibraries {
				BuildEngine = engine,
				JavaSourceFiles = new ITaskItem [] { new TaskItem (jar1), new TaskItem (jar2) }
			};

			Assert.IsFalse (task.Execute (), "Task should fail!");
			BuildErrorEventArgs error = errors [0];
			Assert.AreEqual ("XA1014", error.Code);
			StringAssert.Contains ("library.jar", error.Message);
		}
	}
}
