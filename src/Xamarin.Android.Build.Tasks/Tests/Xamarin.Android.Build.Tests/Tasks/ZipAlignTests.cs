using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests.Tasks
{
	[TestFixture]
	[Category ("Node-2")]
	public class ZipAlignTests : BaseTest
	{
		string path;
		List<BuildErrorEventArgs> errors;
		List<BuildMessageEventArgs> messages;
		MockBuildEngine engine;

		[SetUp]
		public void Setup ()
		{
			path = Path.Combine (Root, "temp", TestName);
			engine = new MockBuildEngine (TestContext.Out,
				errors: errors = new List<BuildErrorEventArgs> (),
				messages: messages = new List<BuildMessageEventArgs> ());
			Directory.CreateDirectory (path);
		}

		[TearDown]
		public void TearDown ()
		{
			if (Directory.Exists (path))
				Directory.Delete (path, recursive: true);
		}

		[Test]
		public void CheckAZAErrorCodeIsRaised ()
		{

			var exe = IsWindows ? "zipalign.exe" : "zipalign";
			var source = Path.Combine (path, "error.zip");
			var zipAlign = new AndroidZipAlign () {
				BuildEngine = engine,
				Source = new TaskItem (source),
				DestinationDirectory = new TaskItem (path),
				ToolPath = GetPathToLatestBuildTools (exe)
			};
			File.WriteAllText (source, "Invalid Zip File");
			Assert.False (zipAlign.Execute (), "Execute should *not* succeed!");
			Assert.AreEqual (1, errors.Count, "1 error should have been raised.");
			Assert.AreEqual ("ANDZA0000", errors [0].Code, $"Expected error code ANDZA0000 but got {errors [0].Code}");
		}
	}
}
