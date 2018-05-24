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
	public class Aapt2Tests : BaseTest {

		string GetPathToAapt2 ()
		{
			var path = Path.Combine (AndroidSdkPath, "build-tools");
			foreach (var dir in Directory.GetDirectories (path, "*", SearchOption.TopDirectoryOnly).OrderByDescending (x => x)) {
				var aapt2 = Path.Combine (dir, IsWindows ? "aapt2.exe" : "aapt");
				if (File.Exists (aapt2))
					return dir;
			}
			return Path.Combine (path, "25.0.2");
		}

		[Test]
		public void Aapt2Compile ()
		{
			var path = Path.Combine (Root, "temp", "Aapt2Compile");
			Directory.CreateDirectory (path);
			var resPath = Path.Combine (path, "res");
			Directory.CreateDirectory (resPath);
			Directory.CreateDirectory (Path.Combine (resPath, "values"));
			Directory.CreateDirectory (Path.Combine (resPath, "layout"));
			File.WriteAllText (Path.Combine (resPath, "values", "strings.xml"), @"<?xml version='1.0' ?><resources><string name='foo'>foo</string></resources>");
			File.WriteAllText (Path.Combine (resPath, "layout", "main.xml"), @"<?xml version='1.0' ?><LinearLayout xmlns:android='http://schemas.android.com/apk/res/android' />");
			var errors = new List<BuildErrorEventArgs> ();
			IBuildEngine engine = new MockBuildEngine (TestContext.Out, errors);
			var task = new Aapt2Compile {
				BuildEngine = engine,
				ToolPath = GetPathToAapt2 (),
				ResourceDirectories = new ITaskItem [] { new TaskItem (resPath) },
			};
			Assert.True (task.Execute (), "task should have succeeded.");
			var flatArchive = Path.Combine (path, "compiled.flata");
			Assert.True (File.Exists (flatArchive), $"{flatArchive} should have been created.");
			using (var apk = ZipHelper.OpenZip (flatArchive)) {
				Assert.AreEqual (2, apk.EntryCount, $"{flatArchive} should have 2 entries.");
			}
			Directory.Delete (Path.Combine (Root, path), recursive: true);
		}

		[Test]
		public void Aapt2CompileFixesUpErrors ()
		{
			var path = Path.Combine (Root, "temp", "Aapt2Compile");
			Directory.CreateDirectory (path);
			var resPath = Path.Combine (path, "res");
			Directory.CreateDirectory (resPath);
			Directory.CreateDirectory (Path.Combine (resPath, "values"));
			Directory.CreateDirectory (Path.Combine (resPath, "layout"));
			File.WriteAllText (Path.Combine (resPath, "values", "strings.xml"), @"<?xml version='1.0' ?><resources><string name='foo'>foo</string</resources>");
			File.WriteAllText (Path.Combine (resPath, "layout", "main.xml"), @"<?xml version='1.0' ?>
<LinearLayout xmlns:android='http://schemas.android.com/apk/res/android'
	android:orientation='vertical'
	android:layout_width='fill_parent'
	android:layout_height='fill_parent'
	>
<Button  
	android:id='@+id/myButton'
	android:layout_width='fill_parent' 
	android:layout_height='wrap_content' 
	android:text='@string/hello'
	/>
</LinearLayout>
");
			var errors = new List<BuildErrorEventArgs> ();
			IBuildEngine engine = new MockBuildEngine (TestContext.Out, errors);
			var directorySeperator = Path.DirectorySeparatorChar;
			var task = new Aapt2Compile {
				BuildEngine = engine,
				ToolPath = GetPathToAapt2 (),
				ResourceDirectories = new ITaskItem [] { new TaskItem (resPath) },
				ResourceNameCaseMap = $"Layout{directorySeperator}Main.xml|layout{directorySeperator}main.axml;Values{directorySeperator}Strings.xml|values{directorySeperator}strings.xml",
			};
			Assert.False (task.Execute (), "task should not have succeeded.");
			Assert.AreEqual (1, errors.Count, "One Error should have been raised.");
			Assert.AreEqual ($"Resources{directorySeperator}Values{directorySeperator}Strings.xml", errors[0].File, $"`values{directorySeperator}strings.xml` should have been replaced with `Resources{directorySeperator}Values{directorySeperator}Strings.xml`");
			Directory.Delete (Path.Combine (Root, path), recursive: true);
		}
	}
}
