using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests {

	[TestFixture]
	[Category ("Node-2")]
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
			using (Stream resourceStream = typeof (XamarinAndroidCommonProject).Assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Base.classes.jar"))
			using (FileStream fileStream = File.Create (jar1))
				resourceStream.CopyTo (fileStream);

			Directory.CreateDirectory (Path.Combine (path, "Hello (World)"));
			string jar2 = Path.Combine (path, "Hello (World)", "library.jar");
			File.WriteAllBytes (jar2, Convert.FromBase64String (@"
UEsDBBQACAgIAMl8lUsAAAAAAAAAAAAAAAAJAAQATUVUQS1JTkYv/soAAAMAUEsHCAAAAAACAAAAA
AAAAFBLAwQUAAgICADJfJVLAAAAAAAAAAAAAAAAFAAAAE1FVEEtSU5GL01BTklGRVNULk1G803My0
xLLS7RDUstKs7Mz7NSMNQz4OVyLkpNLElN0XWqBAlY6BnEG5oaKWj4FyUm56QqOOcXFeQXJZYA1Wv
ycvFyAQBQSwcIbrokAkQAAABFAAAAUEsDBBQACAgIAIJ8lUsAAAAAAAAAAAAAAAASAAAAc2FtcGxl
L0hlbGxvLmNsYXNzO/Vv1z4GBgYTBkEuBhYGXg4GPnYGfnYGAUYGNpvMvMwSO0YGZg3NMEYGFuf8l
FRGBn6fzLxUv9LcpNSikMSkHKAIa3l+UU4KI4OIhqZPVmJZon5OYl66fnBJUWZeujUjA1dwfmlRcq
pbJkgtl0dqTk6+HkgZDwMrAxvQFrCIIiMDT3FibkFOqj6Yz8gggDDKPykrNbmEQZGBGehCEGBiYAR
pBpLsQJ4skGYE0qxa2xkYNwIZjAwcQJINIggkORm4oEqloUqZhZg2oClkB5LcYLN5AFBLBwjQMrpO
0wAAABMBAABQSwECFAAUAAgICADJfJVLAAAAAAIAAAAAAAAACQAEAAAAAAAAAAAAAAAAAAAATUVUQ
S1JTkYv/soAAFBLAQIUABQACAgIAMl8lUtuuiQCRAAAAEUAAAAUAAAAAAAAAAAAAAAAAD0AAABNRV
RBLUlORi9NQU5JRkVTVC5NRlBLAQIUABQACAgIAIJ8lUvQMrpO0wAAABMBAAASAAAAAAAAAAAAAAA
AAMMAAABzYW1wbGUvSGVsbG8uY2xhc3NQSwUGAAAAAAMAAwC9AAAA1gEAAAAA"));

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
