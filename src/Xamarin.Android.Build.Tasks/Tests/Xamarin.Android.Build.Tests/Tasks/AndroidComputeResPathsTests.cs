using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests {
	[TestFixture]
	[Category ("Node-2")]
	[Parallelizable (ParallelScope.Self)]
	public class AndroidComputeResPathsTests : BaseTest {

#pragma warning disable 414
		static object [] AndroidComputeResPathsChecks () => new object [] {
			new object[] {
				/* Identity */	                Path.Combine ("Assets", "asset1.txt"),
				/* LogicalName */               Path.Combine ("Assets", "subfolder", "asset1.txt"),
				/* expectedIntermediateFile */  Path.Combine ("subfolder", "asset1.txt"),
				/* expectedResultFile */        Path.Combine ("Assets", "asset1.txt"),
			},
			new object[] {
				/* Identity */	                Path.Combine ("Assets", "asset1.txt"),
				/* LogicalName */               null,
				/* expectedIntermediateFile */  Path.Combine ("asset1.txt"),
				/* expectedResultFile */        Path.Combine ("Assets", "asset1.txt"),
			},
			new object[] {
				/* Identity */	                Path.Combine ("Assets", "subfolder", "asset1.txt"),
				/* LogicalName */               null,
				/* expectedIntermediateFile */  Path.Combine ("subfolder", "asset1.txt"),
				/* expectedResultFile */        Path.Combine ("Assets", "subfolder", "asset1.txt"),
			},
			new object[] {
				/* Identity */	                Path.Combine ("Assets", "asset1.txt"),
				/* LogicalName */               Path.Combine ("%FOO%Assets", "subfolder", "asset1.txt"),
				/* expectedIntermediateFile */  Path.Combine ("subfolder", "asset1.txt"),
				/* expectedResultFile */        Path.Combine ("Assets", "asset1.txt"),
			},
			new object[] {
				/* Identity */	                Path.Combine ("Assets", "asset1.txt"),
				/* LogicalName */               Path.Combine ("%FOO%Assets", "subfolder", "..", "..", "asset1.txt"),
				/* expectedIntermediateFile */  Path.Combine ("asset1.txt"),
				/* expectedResultFile */        Path.Combine ("Assets", "asset1.txt"),
			},
		};
#pragma warning restore 414

		[Test]
		[TestCaseSource (nameof (AndroidComputeResPathsChecks))]
		public void AndroidComputeResPathsTest (string identity, string logicalName, string expectedIntermediateFile, string expectedResultFile)
		{
			var hash = Tools.Files.HashString (identity + logicalName + expectedIntermediateFile + expectedResultFile);
			var projectDir = Path.Combine (Root, "temp", $"{nameof (AndroidComputeResPathsTest)}_{hash}");
			TestOutputDirectories [TestContext.CurrentContext.Test.ID] = projectDir;
			var intermdiateDir = Path.Combine (projectDir, "assets");
			var errors = new List<BuildErrorEventArgs> ();
			IBuildEngine engine = new MockBuildEngine (TestContext.Out, errors);

			var metaData = new Dictionary<string, string> ();
			if (!string.IsNullOrEmpty (logicalName))
				metaData.Add ("LogicalName", logicalName.Replace ("%FOO%", "\\"));

			// The file needs to exist on disk
			var path = Path.Combine (projectDir, identity);
			Directory.CreateDirectory (Path.GetDirectoryName (path));
			File.WriteAllText (path, contents: "");

			var assets = new List<ITaskItem> () {
				new TaskItem (identity, metaData),
			};

			string previousWorking = Environment.CurrentDirectory;
			try {
				Environment.CurrentDirectory = projectDir;
				var task = new AndroidComputeResPaths () {
					BuildEngine = engine,
					IntermediateDir = intermdiateDir,
					ProjectDir = projectDir,
					Prefixes = "Assets",
					ResourceFiles = assets.ToArray (),
				};
				Assert.True (task.Execute (), "task should have succeeded.");
				Assert.AreEqual (1, task.IntermediateFiles.Length, "Expected 1 entry in the IntermediateFiles Output");
				Assert.AreEqual (1, task.ResolvedResourceFiles.Length, "Expected 1 entry in the ResolvedResourceFiles Output");
				var expected = Path.GetFullPath (Path.Combine (intermdiateDir, expectedIntermediateFile));
				StringAssert.AreEqualIgnoringCase (
					expected,
					task.IntermediateFiles [0].ItemSpec,
					$"Expected {expected} in task.IntermediateFiles, but got {task.IntermediateFiles [0].ItemSpec}"
				);
				StringAssert.AreEqualIgnoringCase (
					expectedResultFile,
					task.ResolvedResourceFiles [0].ItemSpec,
					$"Expected {expectedResultFile} in task.ResolvedResourceFiles, but got {task.ResolvedResourceFiles [0].ItemSpec}"
				);
			} finally {
				Environment.CurrentDirectory = previousWorking;
			}
		}
	}
}
