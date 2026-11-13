using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	public class CollectNonEmptyDirectoriesTests : BaseTest
	{
		[Test]
		public void CollectNonEmptyDirectoriesTest ()
		{
			var path = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (path);
			var resPath = Path.Combine (path, "res");
			var archivePath = Path.Combine (path, "flata");
			var flatFilePath = Path.Combine(path, "flat");
			var resizerPath = Path.Combine (path, "resizer");
			Directory.CreateDirectory (resPath);
			Directory.CreateDirectory (resizerPath);
			Directory.CreateDirectory (Path.Combine (path, "stamps"));
			Directory.CreateDirectory (archivePath);
			Directory.CreateDirectory (flatFilePath);
			Directory.CreateDirectory (Path.Combine (resPath, "values"));
			Directory.CreateDirectory (Path.Combine (resPath, "layout"));
			Directory.CreateDirectory (Path.Combine (resizerPath, "drawable"));
			File.WriteAllText (Path.Combine (resPath, "values", "strings.xml"), @"<?xml version='1.0' ?><resources><string name='foo'>foo</string></resources>");
			File.WriteAllText (Path.Combine (resPath, "layout", "main.xml"), @"<?xml version='1.0' ?><LinearLayout xmlns:android='http://schemas.android.com/apk/res/android' />");
			File.WriteAllText (Path.Combine (resizerPath, "drawable", "icon.xml"), @"<?xml version='1.0' ?>
<vector xmlns:android=""http://schemas.android.com/apk/res/android""
   android:height=""64dp""
   android:width=""64dp""
   android:viewportHeight=""600""
   android:viewportWidth=""600"" >
   <group
      android:name=""rotationGroup""
      android:pivotX=""300.0""
      android:pivotY=""300.0""
      android:rotation=""45.0"" >
      <path
         android:name=""vectorPath""
         android:fillColor=""#000000""
         android:pathData=""M300,70 l 0,-70 70,70 0,0 -70,70z"" />
   </group>
</vector>");
			var libPath = Path.Combine (path, "lp");
			Directory.CreateDirectory (libPath);
			Directory.CreateDirectory (Path.Combine (libPath, "0", "res", "values"));
			Directory.CreateDirectory (Path.Combine (libPath, "1", "res", "values"));
			File.WriteAllText (Path.Combine (libPath, "0", "res", "values", "strings.xml"), @"<?xml version='1.0' ?><resources><string name='foo1'>foo1</string></resources>");
			File.WriteAllText (Path.Combine (libPath, "1", "res", "values", "strings.xml"), @"<?xml version='1.0' ?><resources><string name='foo2'>foo2</string></resources>");
			File.WriteAllText (Path.Combine (path, "AndroidManifest.xml"), @"<?xml version='1.0' ?><manifest xmlns:android='http://schemas.android.com/apk/res/android' package='Foo.Foo' />");
			File.WriteAllText (Path.Combine (path, "foo.map"), @"a\nb");
			var errors = new List<BuildErrorEventArgs> ();
			IBuildEngine engine = new MockBuildEngine (TestContext.Out, errors);
			var task = new CollectNonEmptyDirectories {
				BuildEngine = engine,
				Directories = new ITaskItem[] {
					new TaskItem (resPath, new Dictionary<string, string> {
						{ "FilesCache", Path.Combine(path, "files.cache") },
					}),
					new TaskItem (resizerPath),
					new TaskItem (Path.Combine (libPath, "0", "res"), new Dictionary<string, string> {
						{ "AndroidSkipResourceProcessing", "True" },
						{ "StampFile", "0.stamp" },
					}),
					new TaskItem (Path.Combine (libPath, "1", "res")),
				},
				LibraryProjectIntermediatePath = libPath,
				StampDirectory = Path.Combine(path, "stamps"),
			};
			Assert.True (task.Execute (), $"task should have succeeded. {string.Join (";", errors.Select (x => x.Message))}");
			Assert.AreEqual (4, task.Output.Length, "Output should have 4 items in it.");
			Assert.AreEqual (5, task.LibraryResourceFiles.Length, "Output should have 5 items in it.");
			Assert.AreEqual ("layout_main.xml.flat", task.LibraryResourceFiles[0].GetMetadata ("_FlatFile"));
			Assert.AreEqual ("values_strings.arsc.flat", task.LibraryResourceFiles[1].GetMetadata ("_FlatFile"));
			Assert.AreEqual ("0.flata", task.LibraryResourceFiles[3].GetMetadata ("_FlatFile"));
			Assert.AreEqual ("values_strings.arsc.flat", task.LibraryResourceFiles[4].GetMetadata ("_FlatFile"));
			foreach (var item in task.Output) {
				Assert.IsNotNull (item.GetMetadata ("FilesCache"), "FilesCache should have been set");
				var cacheFile = item.GetMetadata ("FilesCache");
				FileAssert.Exists (cacheFile, $"{cacheFile} should have been created.");
			}
		}
	}
}
