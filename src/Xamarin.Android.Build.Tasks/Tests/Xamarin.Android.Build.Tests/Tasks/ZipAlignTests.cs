using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests.Tasks
{
	[TestClass]
	[DoNotParallelize]
	public class ZipAlignTests : BaseTest
	{
		string path;
		List<BuildErrorEventArgs> errors;
		List<BuildMessageEventArgs> messages;
		MockBuildEngine engine;

		[TestInitialize]
		public void Setup ()
		{
			path = Path.Combine (Root, "temp", TestName);
			engine = new MockBuildEngine (Console.Out,
				errors: errors = new List<BuildErrorEventArgs> (),
				messages: messages = new List<BuildMessageEventArgs> ());
			Directory.CreateDirectory (path);
		}

		[TestCleanup]
		public void TearDown ()
		{
			if (Directory.Exists (path))
				Directory.Delete (path, recursive: true);
		}

		[TestMethod]
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
			Assert.IsFalse (zipAlign.Execute (), "Execute should *not* succeed!");
			Assert.AreEqual (1, errors.Count, "1 error should have been raised.");
			Assert.AreEqual ("ANDZA0000", errors [0].Code, $"Expected error code ANDZA0000 but got {errors [0].Code}");
		}
	}
}
