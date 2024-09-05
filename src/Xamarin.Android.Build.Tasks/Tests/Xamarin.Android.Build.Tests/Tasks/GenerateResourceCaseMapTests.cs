using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests {
	[TestFixture]
	[Parallelizable (ParallelScope.Children)]
	public class GenerateResourceCaseMapTests : BaseTest {

		public void CreateResourceDirectory (string path)
		{
			Directory.CreateDirectory (Path.Combine (Root, path));
			Directory.CreateDirectory (Path.Combine (Root, path, "res", "drawable"));
			Directory.CreateDirectory (Path.Combine (Root, path, "res", "values"));
			var icon_binary_mdpi = XamarinAndroidCommonProject.GetResourceContents ("Xamarin.ProjectTools.Resources.Base.Icon.png");
			File.WriteAllBytes (Path.Combine (Root, path, "res", "drawable", "IMALLCAPS.png"), icon_binary_mdpi);
		}

		[Test]
		public void CaseMapAllCapsWorks ()
		{
			var path = Path.Combine ("temp", TestName + " Some Space");
			CreateResourceDirectory (path);
			var task = new GenerateResourceCaseMap () {
				BuildEngine = new MockBuildEngine (TestContext.Out)
			};
			task.ProjectDir = Path.Combine (Root, path);
			task.ResourceDirectory = Path.Combine (Root, path, "res") + Path.DirectorySeparatorChar;
			task.Resources = new TaskItem [] {
				new TaskItem (Path.Combine (Root, path, "res", "values", "strings.xml"), new Dictionary<string, string> () {
					{ "LogicalName", "values\\strings.xml" },
				}),
				new TaskItem (Path.Combine (Root, path, "res", "drawable", "IMALLCAPS.png")),
			};
			task.OutputFile = new TaskItem (Path.Combine (Root, path, "case_map.txt"));

			Assert.IsTrue (task.Execute (), "Task should have run successfully.");
			FileAssert.Exists (task.OutputFile.ItemSpec, $"'{task.OutputFile}' should have been created.");
			var content1 = File.ReadAllText (task.OutputFile.ItemSpec);
			StringAssert.Contains ($"drawable{Path.DirectorySeparatorChar}IMALLCAPS;IMALLCAPS", content1, "File should contain 'IMALLCAPS'");
		}
	}
}
