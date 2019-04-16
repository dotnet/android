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
using System.Security.Cryptography;

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

		string GetHash (string hashInput)
		{
			using (var sha1 = SHA1.Create ())
			{
				var hash = sha1.ComputeHash (Encoding.UTF8.GetBytes (hashInput));
				var hashResult = new StringBuilder (hash.Length * 2);

				foreach (byte b in hash)
				{
					hashResult.Append (b.ToString ("x2"));
				}
				return hashResult.ToString ();
			}
		}

		void CallAapt2Compile (IBuildEngine engine, string dir, string outputPath, List<ITaskItem> output = null)
		{
			var errors = new List<BuildErrorEventArgs> ();
			var task = new Aapt2Compile {
				BuildEngine = engine,
				ToolPath = GetPathToAapt2 (),
				ResourceDirectories = new ITaskItem [] {
					new TaskItem(dir, new Dictionary<string, string> {
						{ "Hash", output != null ? GetHash (dir) : "compiled" }
					}),
				},
				FlatArchivesDirectory = outputPath,
			};
			Assert.True (task.Execute (), "task should have succeeded.");
			output?.AddRange (task.CompiledResourceFlatArchives);
		}

		[Test]
		public void Aapt2Link ()
		{
			var path = Path.Combine (Root, "temp", "Aapt2Link");
			Directory.CreateDirectory (path);
			var resPath = Path.Combine (path, "res");
			var archivePath = Path.Combine (path, "flata");
			Directory.CreateDirectory (resPath);
			Directory.CreateDirectory (archivePath);
			Directory.CreateDirectory (Path.Combine (resPath, "values"));
			Directory.CreateDirectory (Path.Combine (resPath, "layout"));
			File.WriteAllText (Path.Combine (resPath, "values", "strings.xml"), @"<?xml version='1.0' ?><resources><string name='foo'>foo</string></resources>");
			File.WriteAllText (Path.Combine (resPath, "layout", "main.xml"), @"<?xml version='1.0' ?><LinearLayout xmlns:android='http://schemas.android.com/apk/res/android' />");
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
			var archives = new List<ITaskItem>();
			CallAapt2Compile (engine, resPath, archivePath);
			CallAapt2Compile (engine, Path.Combine (libPath, "0", "res"), archivePath, archives);
			CallAapt2Compile (engine, Path.Combine (libPath, "1", "res"), archivePath, archives);
			var outputFile = Path.Combine (path, "resources.apk");
			var task = new Aapt2Link {
				BuildEngine = engine,
				ToolPath = GetPathToAapt2 (),
				ResourceDirectories = new ITaskItem [] { new TaskItem (resPath) },
				ManifestFiles = new ITaskItem [] { new TaskItem (Path.Combine (path, "AndroidManifest.xml")) },
				AdditionalResourceArchives = archives.ToArray (),
				CompiledResourceFlatArchive = new TaskItem (Path.Combine (archivePath, "compiled.flata")),
				OutputFile = outputFile,
				AssemblyIdentityMapFile = Path.Combine (path, "foo.map"),
			};
			Assert.True (task.Execute (), "task should have succeeded.");
			Assert.True (File.Exists (outputFile), $"{outputFile} should have been created.");
			using (var apk = ZipHelper.OpenZip (outputFile)) {
				Assert.AreEqual (3, apk.EntryCount, $"{outputFile} should have 3 entries.");
			}
			Directory.Delete (Path.Combine (Root, path), recursive: true);
		}

		[Test]
		public void Aapt2Compile ()
		{
			var path = Path.Combine (Root, "temp", "Aapt2Compile");
			Directory.CreateDirectory (path);
			var resPath = Path.Combine (path, "res");
			var archivePath = Path.Combine(path, "flata");
			Directory.CreateDirectory(resPath);
			Directory.CreateDirectory(archivePath);
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
				FlatArchivesDirectory = archivePath,
			};
			Assert.True (task.Execute (), "task should have succeeded.");
			var flatArchive = Path.Combine (archivePath, "compiled.flata");
			Assert.True (File.Exists (flatArchive), $"{flatArchive} should have been created.");
			using (var apk = ZipHelper.OpenZip (flatArchive)) {
				Assert.AreEqual (2, apk.EntryCount, $"{flatArchive} should have 2 entries.");
			}
			Directory.Delete (Path.Combine (Root, path), recursive: true);
		}

		[Test]
		public void Aapt2CompileFixesUpErrors ()
		{
			var path = Path.Combine (Root, "temp", "Aapt2CompileFixesUpErrors");
			Directory.CreateDirectory (path);
			var resPath = Path.Combine (path, "res");
			var archivePath = Path.Combine(path, "flata");
			Directory.CreateDirectory(resPath);
			Directory.CreateDirectory(archivePath);
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
			var current = Directory.GetCurrentDirectory ();
			try {
				Directory.SetCurrentDirectory (path);
				var task = new Aapt2Compile {
					BuildEngine = engine,
					ToolPath = GetPathToAapt2 (),
					ResourceDirectories = new ITaskItem [] { new TaskItem (resPath) },
		    			FlatArchivesDirectory = archivePath,
					ResourceNameCaseMap = $"Layout{directorySeperator}Main.xml|layout{directorySeperator}main.axml;Values{directorySeperator}Strings.xml|values{directorySeperator}strings.xml",
				};
				Assert.False (task.Execute (), "task should not have succeeded.");
			} finally {
				Directory.SetCurrentDirectory (current);
			}
			Assert.AreEqual (1, errors.Count, "One Error should have been raised.");
			Assert.AreEqual ($"Resources{directorySeperator}Values{directorySeperator}Strings.xml", errors[0].File, $"`values{directorySeperator}strings.xml` should have been replaced with `Resources{directorySeperator}Values{directorySeperator}Strings.xml`");
			Directory.Delete (Path.Combine (Root, path), recursive: true);
		}

		[Test]
		public void Aapt2Disabled ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.SetProperty ("AndroidUseAapt2", "False");
			using (var b = CreateApkBuilder ("temp/Aapt2Disabled")) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				Assert.IsTrue (StringAssertEx.ContainsText (b.LastBuildOutput, "Task \"Aapt2Link\" skipped"), "Aapt2Link task should be skipped!");
				Assert.IsTrue (b.Output.IsTargetSkipped ("_CreateAapt2VersionCache"), "_CreateAapt2VersionCache target should be skipped!");
			}
		}

		[Test]
		public void Aapt2AndroidResgenExtraArgsAreInvalid ()
		{
			var path = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (path);
			var resPath = Path.Combine (path, "res");
			var archivePath = Path.Combine(path, "flata");
			Directory.CreateDirectory(resPath);
			Directory.CreateDirectory(archivePath);
			Directory.CreateDirectory (Path.Combine (resPath, "values"));
			Directory.CreateDirectory (Path.Combine (resPath, "layout"));
			File.WriteAllText (Path.Combine (resPath, "values", "strings.xml"), @"<?xml version='1.0' ?><resources><string name='foo'>foo</string></resources>");
			File.WriteAllText (Path.Combine (resPath, "layout", "main.xml"), @"<?xml version='1.0' ?><LinearLayout xmlns:android='http://schemas.android.com/apk/res/android' />");
			File.WriteAllText (Path.Combine (path, "AndroidManifest.xml"), @"<?xml version='1.0' ?><manifest xmlns:android='http://schemas.android.com/apk/res/android' package='Foo.Foo' />");
			File.WriteAllText (Path.Combine (path, "foo.map"), @"a\nb");
			var errors = new List<BuildErrorEventArgs> ();
			IBuildEngine engine = new MockBuildEngine (TestContext.Out, errors);
			var archives = new List<ITaskItem>();
			CallAapt2Compile (engine, resPath, archivePath);
			var outputFile = Path.Combine (path, "resources.apk");
			var task = new Aapt2Link {
				BuildEngine = engine,
				ToolPath = GetPathToAapt2 (),
				ResourceDirectories = new ITaskItem [] { new TaskItem (resPath) },
				ManifestFiles = new ITaskItem [] { new TaskItem (Path.Combine (path, "AndroidManifest.xml")) },
				CompiledResourceFlatArchive = new TaskItem (Path.Combine (path, "compiled.flata")),
				OutputFile = outputFile,
				AssemblyIdentityMapFile = Path.Combine (path, "foo.map"),
				ExtraArgs = "--no-crunch "
			};
			Assert.False (task.Execute (), "task should have failed.");
			Assert.AreEqual (1, errors.Count, "One error should have been raised.");
			Directory.Delete (Path.Combine (Root, path), recursive: true);
		}
	}
}
