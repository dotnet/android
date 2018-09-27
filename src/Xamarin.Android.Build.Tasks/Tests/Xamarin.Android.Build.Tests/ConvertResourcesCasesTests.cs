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
<fragment android:name='classlibrary1.CustomView' />
<fragment class='ClassLibrary1.CustomView' />
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
			task.CustomViewMapFile = Path.Combine (path, "classmap.txt");
			File.WriteAllLines (task.AcwMapFile, new string [] {
				"ClassLibrary1.CustomView;md5d6f7135293df7527c983d45d07471c5e.CustomTextView",
				"classlibrary1.CustomView;md5d6f7135293df7527c983d45d07471c5e.CustomTextView",
			});
			Assert.IsTrue (task.Execute (), "Task should have executed successfully");
			var custom = new ConvertCustomView () {
				BuildEngine = engine,
				CustomViewMapFile = task.CustomViewMapFile,
				AcwMapFile = task.AcwMapFile,
				ResourceDirectories = new ITaskItem [] {
					new TaskItem (resPath),
				},
			};
			Assert.IsTrue (custom.Execute (), "Task should have executed successfully");
			var output = File.ReadAllText (Path.Combine (resPath, "layout", "main.xml"));
			StringAssert.Contains ("md5d6f7135293df7527c983d45d07471c5e.CustomTextView", output, "md5d6f7135293df7527c983d45d07471c5e.CustomTextView should exist in the main.xml");
			StringAssert.DoesNotContain ("ClassLibrary1.CustomView", output, "ClassLibrary1.CustomView should have been replaced.");
			StringAssert.DoesNotContain ("classlibrary1.CustomView", output, "classlibrary1.CustomView should have been replaced.");
			Assert.IsTrue (custom.Execute (), "Task should have executed successfully");
			var secondOutput = File.ReadAllText (Path.Combine(resPath, "layout", "main.xml"));
			StringAssert.AreEqualIgnoringCase (output, secondOutput, "Files should not have changed.");
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
<fragment android:name='classLibrary1.CustomView' />
<fragment class='ClassLibrary1.CustomView' />
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
			task.CustomViewMapFile = Path.Combine (path, "classmap.txt");
			File.WriteAllLines (task.AcwMapFile, new string [] {
				"ClassLibrary1.CustomView;md5d6f7135293df7527c983d45d07471c5e.CustomTextView",
				"classlibrary1.CustomView;md5d6f7135293df7527c983d45d07471c5e.CustomTextView",
			});
			Assert.IsTrue (task.Execute (), "Task should have executed successfully");
			var custom = new ConvertCustomView () {
				BuildEngine = engine,
				CustomViewMapFile = task.CustomViewMapFile,
				AcwMapFile = task.AcwMapFile,
				ResourceDirectories = new ITaskItem [] {
					new TaskItem (resPath),
				},
			};
			Assert.IsFalse (custom.Execute (), "Task should have executed successfully");
			var output = File.ReadAllText (Path.Combine (resPath, "layout", "main.xml"));
			StringAssert.Contains ("md5d6f7135293df7527c983d45d07471c5e.CustomTextView", output, "md5d6f7135293df7527c983d45d07471c5e.CustomTextView should exist in the main.xml");
			StringAssert.DoesNotContain ("ClassLibrary1.CustomView", output, "ClassLibrary1.CustomView should have been replaced.");
			StringAssert.Contains ("classLibrary1.CustomView", output, "classLibrary1.CustomView should have been replaced.");
			Assert.AreEqual (1, errors.Count, "One Error should have been raised.");
			Assert.AreEqual ("XA1002", errors [0].Code, "XA1002 should have been raised.");
			var expected = Path.Combine ("Resources", "layout", "main.xml");
			Assert.AreEqual (expected, errors [0].File, $"Error should have the \"{expected}\" path. But contained \"{errors [0].File}\"");
			Assert.IsFalse (custom.Execute (), "Task should have executed successfully");
			var secondOutput = File.ReadAllText (Path.Combine (resPath, "layout", "main.xml"));
			StringAssert.AreEqualIgnoringCase (output, secondOutput, "Files should not have changed.");
			Directory.Delete (path, recursive: true);
		}
	}
}
