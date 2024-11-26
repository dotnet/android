using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tasks;
using TaskItem = Microsoft.Build.Utilities.TaskItem;

namespace Xamarin.Android.Build.Tests
{

	[TestFixture]
	public class CheckForInvalidResourceFileNamesTests : BaseTest {

#pragma warning disable 414
		static object [] InvalidResourceFileNamesChecks () => new object [] {
			new object[] {
				"myresource.xml",
				true,
				0,
			},
			new object[] {
				"_myresource.xml",
				true,
				0,
			},
			new object[] {
				"Myresource.xml",
				true,
				0,
			},
			new object[] {
				"@myresource.xml",
				false,
				2,
			},
			new object[] {
				"5myresource.xml",
				false,
				1,
			},
			new object[] {
				".myresource.xml",
				false,
				1,
			},
			new object[] {
				"-myresource.xml",
				false,
				2,
			},
			new object[] {
				"import.xml",
				false,
				1,
			},
			new object[] {
				"class.png",
				false,
				1,
			},
			new object[] {
				"class1.png",
				true,
				0,
			},
		};
#pragma warning restore 414
		[Test]
		[TestCaseSource(nameof(InvalidResourceFileNamesChecks))]
		public void InvalidResourceFileNames (string file, bool shouldPass, int expectedErrorCount)
		{
			var errors = new List<BuildErrorEventArgs> ();
			var task = new CheckForInvalidResourceFileNames {
				BuildEngine = new MockBuildEngine (TestContext.Out, errors),
				Resources = new TaskItem [] {
					new TaskItem (file),
				},
			};
			Assert.AreEqual (shouldPass, task.Execute (), $"task.Execute() should have {(shouldPass ? "succeeded" : "failed")}.");
			Assert.AreEqual (expectedErrorCount, errors.Count, "There should be one error.");
		}
	}
}
