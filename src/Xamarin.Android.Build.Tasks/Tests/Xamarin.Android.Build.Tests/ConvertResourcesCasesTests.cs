using System;
using System.Collections.Generic;
using NUnit.Framework;
using Xamarin.ProjectTools;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using System.Text;
using Xamarin.Android.Tasks;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Build.Tests {

	[TestFixture]
	[Parallelizable (ParallelScope.Self)]
	public class ConvertResourcesCasesTests  : BaseTest {
		[Test]
		public void CheckClassIsReplacedWithMd5 ()
		{
			var path = Path.Combine (Root, "temp", "CheckClassIsReplacedWithMd5");
			Directory.CreateDirectory (path);
			var resPath = Path.Combine (path, "res");
			Directory.CreateDirectory (Path.Combine (resPath, "layout"));
			File.WriteAllText (Path.Combine (resPath, "layout", "main.xml"), @"<?xml version='1.0' ?>
<LinearLayout xmlns:android='http://schemas.android.com/apk/res/android'>
<ClassLibrary1.CustomView xmlns:android='http://schemas.android.com/apk/res/android' />
<classlibrary1.CustomView xmlns:android='http://schemas.android.com/apk/res/android' />
</LinearLayout>
");
			var errors = new List<BuildErrorEventArgs> ();
			IBuildEngine engine = new MockBuildEngine (TestContext.Out, errors);
			var task = new ConvertResourcesCases {
				BuildEngine = engine
			};
			task.ResourceDirectories = new ITaskItem [] {
				new TaskItem (resPath),
			};
			task.AcwMapFile = Path.Combine (path, "acwmap.txt");
			File.WriteAllLines (task.AcwMapFile, new string [] {
				"ClassLibrary1.CustomView;md5d6f7135293df7527c983d45d07471c5e.CustomTextView",
				"classlibrary1.CustomView;md5d6f7135293df7527c983d45d07471c5e.CustomTextView",
			});
			Assert.IsTrue (task.Execute (), "Task should have executed successfully");
			var output = File.ReadAllText (Path.Combine (resPath, "layout", "main.xml"));
			StringAssert.Contains ("md5d6f7135293df7527c983d45d07471c5e.CustomTextView", output, "md5d6f7135293df7527c983d45d07471c5e.CustomTextView should exist in the main.xml");
			StringAssert.DoesNotContain ("ClassLibrary1.CustomView", output, "ClassLibrary1.CustomView should have been replaced.");
			StringAssert.DoesNotContain ("classlibrary1.CustomView", output, "classlibrary1.CustomView should have been replaced.");
			Directory.Delete (path, recursive: true);
		}

		[Test]
		public void CheckClassIsNotReplacedWithMd5 ()
		{
			var path = Path.Combine (Root, "temp", "CheckClassIsNotReplacedWithMd5");
			Directory.CreateDirectory (path);
			var resPath = Path.Combine (path, "res");
			Directory.CreateDirectory (Path.Combine (resPath, "layout"));
			File.WriteAllText (Path.Combine (resPath, "layout", "main.xml"), @"<?xml version='1.0' ?>
<LinearLayout xmlns:android='http://schemas.android.com/apk/res/android'>
<ClassLibrary1.CustomView xmlns:android='http://schemas.android.com/apk/res/android' />
<classLibrary1.CustomView xmlns:android='http://schemas.android.com/apk/res/android' />
</LinearLayout>
");
			var errors = new List<BuildErrorEventArgs> ();
			IBuildEngine engine = new MockBuildEngine (TestContext.Out, errors);
			var task = new ConvertResourcesCases {
				BuildEngine = engine
			};
			task.ResourceDirectories = new ITaskItem [] {
				new TaskItem (resPath),
			};
			task.AcwMapFile = Path.Combine (path, "acwmap.txt");
			File.WriteAllLines (task.AcwMapFile, new string [] {
				"ClassLibrary1.CustomView;md5d6f7135293df7527c983d45d07471c5e.CustomTextView",
				"classlibrary1.CustomView;md5d6f7135293df7527c983d45d07471c5e.CustomTextView",
			});
			Assert.IsTrue (task.Execute (), "Task should have executed successfully");
			var output = File.ReadAllText (Path.Combine (resPath, "layout", "main.xml"));
			StringAssert.Contains ("md5d6f7135293df7527c983d45d07471c5e.CustomTextView", output, "md5d6f7135293df7527c983d45d07471c5e.CustomTextView should exist in the main.xml");
			StringAssert.DoesNotContain ("ClassLibrary1.CustomView", output, "ClassLibrary1.CustomView should have been replaced.");
			StringAssert.Contains ("classLibrary1.CustomView", output, "classLibrary1.CustomView should have been replaced.");
			Assert.AreEqual (1, errors.Count, "One Error should have been raised.");
			Assert.AreEqual ("XA1000", errors [0].Code, "XA1000 should have been raised.");
			Directory.Delete (path, recursive: true);
		}
	}
}
