using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Microsoft.Android.Tasks;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Build.Tests
{
	public class Aapt2Tests : BaseTest
	{
		void CallAapt2Compile (IBuildEngine engine, string dir, string outputPath, string flatFilePath, List<ITaskItem> output = null, int maxInstances = 0)
		{
			var errors = new List<BuildErrorEventArgs> ();
			ITaskItem item;
			if (File.Exists (dir)) {
				item = CreateTaskItemForResourceFile (dir);
			} else {
				item = new TaskItem(dir, new Dictionary<string, string> {
					{ "ResourceDirectory", dir },
					{ "Hash", output != null ? Files.HashString (dir) : "compiled" },
					{ "_FlatFile", output != null ? Files.HashString (dir) + ".flata" : "compiled.flata" },
					{ "_ArchiveDirectory", outputPath }
				});
			}
			var task = new Aapt2Compile {
				BuildEngine = engine,
				ToolPath = AndroidSdkTestHelper.GetAapt2Directory (),
				ResourcesToCompile = new ITaskItem [] {
					item,
				},
				ResourceDirectories = new ITaskItem [] {
					new TaskItem (dir),
				},
				FlatArchivesDirectory = outputPath,
				FlatFilesDirectory = flatFilePath,
				DaemonMaxInstanceCount = maxInstances,
			};
			MockBuildEngine mockEngine = (MockBuildEngine)engine;
			Assert.True (task.Execute (), $"task should have succeeded. {string.Join (" ", mockEngine.Errors.Select (x => x.Message))}");
			output?.AddRange (task.CompiledResourceFlatArchives);
		}

		ITaskItem CreateTaskItemForResourceFile (string root, string dir, string file)
		{
			string ext = Path.GetExtension (file);
			if (dir.StartsWith ("values", StringComparison.Ordinal))
				ext = ".arsc";
			return new TaskItem (Path.Combine (root, dir, file), new Dictionary<string, string> { { "_FlatFile", $"{dir}_{Path.GetFileNameWithoutExtension (file)}{ext}.flat" } } );
		}

		ITaskItem CreateTaskItemForResourceFile (string file)
		{
			string ext = Path.GetExtension (file);
			string dir = Path.GetFileName (Path.GetDirectoryName (file));
			if (dir.StartsWith ("values", StringComparison.Ordinal))
				ext = ".arsc";
			return new TaskItem (file, new Dictionary<string, string> { { "_FlatFile", $"{dir}_{Path.GetFileNameWithoutExtension (file)}{ext}.flat" } } );
		}

		[Test]
		[TestCase (6, 6, 3, 2)]
		[TestCase (6, 6, 2, 1)]
		[TestCase (6, 6, 6, 50)]
		[TestCase (1, 1, 1, 10)]
		public void Aapt2DaemonInstances (int maxInstances, int expectedMax, int expectedInstances, int numLayouts)
		{
			var path = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (path);
			var resPath = Path.Combine (path, "res");
			var archivePath = Path.Combine (path, "flata");
			var flatFilePath = Path.Combine(path, "flat");
			Directory.CreateDirectory (resPath);
			Directory.CreateDirectory (archivePath);
			Directory.CreateDirectory (flatFilePath);
			Directory.CreateDirectory (Path.Combine (resPath, "values"));
			Directory.CreateDirectory (Path.Combine (resPath, "layout"));
			File.WriteAllText (Path.Combine (resPath, "values", "strings.xml"), @"<?xml version='1.0' ?><resources><string name='foo'>foo</string></resources>");
			for (int i = 0; i < numLayouts; i++)
				File.WriteAllText (Path.Combine (resPath, "layout", $"main{i}.xml"), @"<?xml version='1.0' ?><LinearLayout xmlns:android='http://schemas.android.com/apk/res/android' />");
			File.WriteAllText (Path.Combine (path, "AndroidManifest.xml"), @"<?xml version='1.0' ?><manifest xmlns:android='http://schemas.android.com/apk/res/android' package='Foo.Foo' />");
			File.WriteAllText (Path.Combine (path, "foo.map"), @"a\nb");
			var errors = new List<BuildErrorEventArgs> ();
			var warnings = new List<BuildWarningEventArgs> ();
			List<ITaskItem> files = new List<ITaskItem> ();
			IBuildEngine4 engine = new MockBuildEngine (System.Console.Out, errors, warnings);
			files.Add (CreateTaskItemForResourceFile (resPath, "values", "strings.xml"));
			for (int i = 0; i < numLayouts; i++)
				files.Add (CreateTaskItemForResourceFile (resPath, "layout", $"main{i}.xml"));
			var task = new Aapt2CompileWithProcessorCount (processorCount: 8) {
				BuildEngine = engine,
				ToolPath = AndroidSdkTestHelper.GetAapt2Directory (),
				ResourcesToCompile = files.ToArray (),
				ResourceDirectories = new ITaskItem [] { new TaskItem (resPath) },
				FlatArchivesDirectory = archivePath,
				FlatFilesDirectory = flatFilePath,
				DaemonMaxInstanceCount = maxInstances,
			};
			Assert.True (task.Execute (), $"task should have succeeded. {string.Join (";", errors.Select (x => x.Message))}");
			var daemon = task.Daemon;
			Assert.AreEqual (expectedMax, daemon.MaxInstances, $"Expected {expectedMax} but was {daemon.MaxInstances}");
			Assert.AreEqual (expectedInstances, daemon.CurrentInstances, $"Expected {expectedInstances} but was {daemon.CurrentInstances}");
			Directory.Delete (Path.Combine (Root, path), recursive: true);
		}

		struct ApiInfo
		{
			public string Id;
			public int Level;
			public string Name;
			public string FrameworkVersion;
			public bool Stable;
		}

		string CreateFauxReferencesDirectory (string path, ApiInfo [] versions)
		{
			string referencesDirectory = Path.Combine (Root, path);
			Directory.CreateDirectory (referencesDirectory);
			Directory.CreateDirectory (Path.Combine (referencesDirectory, "MonoAndroid", "v1.0", "RedistList"));
			File.WriteAllText (Path.Combine (referencesDirectory, "MonoAndroid", "v1.0", "mscorlib.dll"), "");
			File.WriteAllText (Path.Combine (referencesDirectory, "MonoAndroid", "v1.0", "RedistList", "FrameworkList.xml"),
				$"<FileList Redist=\"MonoAndroid\" Name=\".NET for Android Base Class Libraries\"></FileList>");
			foreach (var v in versions) {
				Directory.CreateDirectory (Path.Combine (referencesDirectory, "MonoAndroid", v.FrameworkVersion));
				Directory.CreateDirectory (Path.Combine (referencesDirectory, "MonoAndroid", v.FrameworkVersion, "RedistList"));
				File.WriteAllText (Path.Combine (referencesDirectory, "MonoAndroid", v.FrameworkVersion, "MonoAndroid.dll"), "");
				File.WriteAllText (Path.Combine (referencesDirectory, "MonoAndroid", v.FrameworkVersion, "AndroidApiInfo.xml"),
					$"<AndroidApiInfo>\n<Id>{v.Id}</Id>\n<Level>{v.Level}</Level>\n<Name>{v.Name}</Name>\n<Version>{v.FrameworkVersion}</Version>\n<Stable>{v.Stable}</Stable>\n</AndroidApiInfo>");
				File.WriteAllText (Path.Combine (referencesDirectory, "MonoAndroid", v.FrameworkVersion, "RedistList", "FrameworkList.xml"),
					$"<FileList Redist=\"MonoAndroid\" Name=\".NET for Android {v.FrameworkVersion} Support\" IncludeFramework=\"v1.0\"></FileList>");
			}
			return referencesDirectory;
		}

		class Aapt2CompileWithProcessorCount : Aapt2Compile
		{
			protected override int ProcessorCount { get; }

			public Aapt2CompileWithProcessorCount (int processorCount) => ProcessorCount = processorCount;
		}

		class ApiMetadataBuildEngine : MockBuildEngine, IBuildEngine4
		{
			readonly IBuildEngine4 cache = new MockBuildEngine (TextWriter.Null);
			readonly int taskThread = Environment.CurrentManagedThreadId;
			bool yielded;
			int metadataLookups;

			public int MetadataLookups => metadataLookups;
			public bool MetadataAccessBeforeYield { get; private set; } = true;
			public bool Yielded => yielded;
			public AndroidVersions Versions { get; private set; }

			public ApiMetadataBuildEngine (IList<BuildErrorEventArgs> errors, IList<BuildWarningEventArgs> warnings)
				: base (TestContext.Out, errors, warnings)
			{
			}

			static bool IsApiMetadataKey (object key) =>
				key is ValueTuple<string, object> localKey &&
				localKey.Item2 is ValueTuple<string, string, string> metadataKey &&
				metadataKey.Item2 == nameof (AndroidVersions);

			void CheckMetadataThread ()
			{
				if (yielded || Environment.CurrentManagedThreadId != taskThread)
					MetadataAccessBeforeYield = false;
			}

			object IBuildEngine4.GetRegisteredTaskObject (object key, RegisteredTaskObjectLifetime lifetime)
			{
				if (IsApiMetadataKey (key)) {
					Assert.AreEqual (RegisteredTaskObjectLifetime.Build, lifetime);
					System.Threading.Interlocked.Increment (ref metadataLookups);
					CheckMetadataThread ();
				}
				return cache.GetRegisteredTaskObject (key, lifetime);
			}

			void IBuildEngine4.RegisterTaskObject (object key, object value, RegisteredTaskObjectLifetime lifetime, bool allowEarlyCollection)
			{
				if (value is AndroidVersions versions) {
					Assert.AreEqual (RegisteredTaskObjectLifetime.Build, lifetime);
					Versions = versions;
					CheckMetadataThread ();
				}
				cache.RegisterTaskObject (key, value, lifetime, allowEarlyCollection);
			}

			object IBuildEngine4.UnregisterTaskObject (object key, RegisteredTaskObjectLifetime lifetime) =>
				cache.UnregisterTaskObject (key, lifetime);

			void IBuildEngine3.Yield () => yielded = true;

			void IBuildEngine3.Reacquire () { }
		}

		[Test]
		public void Aapt2Link ([Values (true, false)] bool compilePerFile)
		{
			var path = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (path);
			var resPath = Path.Combine (path, "res");
			var archivePath = Path.Combine (path, "flata");
			var flatFilePath = Path.Combine(path, "flat");
			Directory.CreateDirectory (resPath);
			Directory.CreateDirectory (archivePath);
			Directory.CreateDirectory (flatFilePath);
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
			var warnings = new List<BuildWarningEventArgs> ();
			var engine = new ApiMetadataBuildEngine (errors, warnings);
			var archives = new List<ITaskItem>();
			if (compilePerFile) {
				foreach (var file in Directory.EnumerateFiles (resPath, "*.*", SearchOption.AllDirectories)) {
					CallAapt2Compile (engine, file, archivePath, flatFilePath);
				}
			} else {
				CallAapt2Compile (engine, resPath, archivePath, flatFilePath);
			}
			CallAapt2Compile (engine, Path.Combine (libPath, "0", "res"), archivePath, flatFilePath, archives);
			CallAapt2Compile (engine, Path.Combine (libPath, "1", "res"), archivePath, flatFilePath, archives);
			List<ITaskItem> items = new List<ITaskItem> ();
			if (compilePerFile) {
				// collect all the flat archives
				foreach (var file in Directory.EnumerateFiles (flatFilePath, "*.flat", SearchOption.AllDirectories)) {
					items.Add (new TaskItem (file));
				}
			}
			var outputFile = Path.Combine (path, "resources.apk");
			var referencePath = CreateFauxReferencesDirectory (Path.Combine ("temp", TestName, "references"), new [] {
				new ApiInfo { Id = "28", Level = 28, Name = "Pie", FrameworkVersion = "v9.0", Stable = true },
			});
			var manifestFile = Path.Combine (path, "AndroidManifest.xml");
			var task = new Aapt2Link {
				BuildEngine = engine,
				ToolPath = AndroidSdkTestHelper.GetAapt2Directory (),
				YieldDuringToolExecution = true,
				ReferenceAssemblyPaths = [Path.GetRelativePath (Directory.GetCurrentDirectory (), Path.Combine (referencePath, "MonoAndroid"))],
				CreatePackagePerAbi = true,
				SupportedAbis = ["arm64-v8a", "x86_64"],
				AndroidManifestFile = new TaskItem (manifestFile),
				ResourceDirectories = new ITaskItem [] { new TaskItem (resPath) },
				ManifestFiles = new ITaskItem [] { new TaskItem (manifestFile) },
				AdditionalResourceArchives = !compilePerFile ? archives.ToArray () : null,
				CompiledResourceFlatArchive = !compilePerFile ? new TaskItem (Path.Combine (archivePath, "compiled.flata")) : null,
				CompiledResourceFlatFiles = compilePerFile ? items.ToArray () : null,
				OutputFile = outputFile,
				AssemblyIdentityMapFile = Path.Combine (path, "foo.map"),
				JavaPlatformJarPath = AndroidSdkTestHelper.GetAndroidJarPath (),
			};
			Assert.True (task.Execute (), $"task should have succeeded. {string.Join (";", errors.Select (x => x.Message))}");
			Assert.AreEqual (1, engine.MetadataLookups, "All ABI links must share one API metadata lookup.");
			Assert.IsTrue (engine.MetadataAccessBeforeYield, "Resolve API metadata on the calling thread before yielding.");
			Assert.IsTrue (engine.Yielded);
			Assert.IsNotNull (engine.Versions);
			CollectionAssert.Contains (engine.Versions.FrameworkDirectories, Path.Combine (referencePath, "MonoAndroid"));
			FileAssert.Exists (outputFile + "-arm64-v8a");
			FileAssert.Exists (outputFile + "-x86_64");
			Assert.AreEqual (0, errors.Count, "There should be no errors.");
			Assert.LessOrEqual (0, warnings.Count, "There should be 0 warnings.");
			Assert.True (File.Exists (outputFile), $"{outputFile} should have been created.");
			using (var apk = ZipFile.OpenRead (outputFile)) {
				Assert.AreEqual (3, apk.Entries.Count, $"{outputFile} should have 3 entries.");
			}
			Directory.Delete (Path.Combine (Root, path), recursive: true);
		}

		[TestCase ("Aapt2Compile")]
		[TestCase ("Aapt2CompileÜmläüt")]
		public void Aapt2Compile (string directoryName)
		{
			var path = Path.Combine (Root, "temp", directoryName);
			Directory.CreateDirectory (path);
			var resPath = Path.Combine (path, "res");
			var archivePath = Path.Combine(path, "flata");
			var flatFilePath = Path.Combine(path, "flat");
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
				ToolPath = AndroidSdkTestHelper.GetAapt2Directory (),
				ResourcesToCompile = new ITaskItem [] {
						new TaskItem (resPath, new Dictionary<string, string> () {
								{ "ResourceDirectory", resPath },
							}
						)
					},
				ResourceDirectories = new ITaskItem [] { new TaskItem (resPath) },
				FlatArchivesDirectory = archivePath,
				FlatFilesDirectory = flatFilePath,
			};
			Assert.True (task.Execute (), "task should have succeeded.");
			var flatArchive = Path.Combine (archivePath, "compiled.flata");
			Assert.True (File.Exists (flatArchive), $"{flatArchive} should have been created.");
			using (var apk = ZipFile.OpenRead (flatArchive)) {
				Assert.AreEqual (2, apk.Entries.Count, $"{flatArchive} should have 2 entries.");
			}
			Assert.AreEqual (0, Directory.GetFiles (path, "*.zip", SearchOption.AllDirectories).Length, "Temporary resource archives should have been deleted.");
			Directory.Delete (Path.Combine (Root, path), recursive: true);
		}

		[Test]
		public void Aapt2CompileUmlautsAndSpaces ()
		{
			var path = Path.Combine (Root, "temp", "Aapt2CompileÜmläüt Files");
			Directory.CreateDirectory (path);
			var resPath = Path.Combine (path, "res");
			var archivePath = Path.Combine(path, "flata");
			var flatFilePath = Path.Combine(path, "flat");
			Directory.CreateDirectory(resPath);
			Directory.CreateDirectory(archivePath);
			Directory.CreateDirectory(flatFilePath);
			Directory.CreateDirectory (Path.Combine (resPath, "values"));
			Directory.CreateDirectory (Path.Combine (resPath, "layout"));
			File.WriteAllText (Path.Combine (resPath, "values", "strings.xml"), @"<?xml version='1.0' ?><resources><string name='foo'>foo</string></resources>");
			File.WriteAllText (Path.Combine (resPath, "layout", "main.xml"), @"<?xml version='1.0' ?><LinearLayout xmlns:android='http://schemas.android.com/apk/res/android' />");
			List<ITaskItem> files = new List<ITaskItem> ();
			files.Add (CreateTaskItemForResourceFile (resPath, "values", "strings.xml"));
			files.Add (CreateTaskItemForResourceFile (resPath, "layout", "main.xml"));
			var errors = new List<BuildErrorEventArgs> ();
			IBuildEngine engine = new MockBuildEngine (TestContext.Out, errors);
			var task = new Aapt2Compile {
				BuildEngine = engine,
				ToolPath = AndroidSdkTestHelper.GetAapt2Directory (),
				ResourcesToCompile = files.ToArray (),
				ResourceDirectories = new ITaskItem [] { new TaskItem (resPath) },
				FlatArchivesDirectory = archivePath,
				FlatFilesDirectory = flatFilePath,
			};
			Assert.True (task.Execute (), $"task should have succeeded. {string.Join (";", errors.Select (x => x.Message))}");
			var flatArchive = Path.Combine (archivePath, "compiled.flata");
			Assert.False (File.Exists (flatArchive), $"{flatArchive} should not have been created.");
			foreach (var file in files) {
				string f = Path.Combine (flatFilePath, file.GetMetadata ("_FlatFile"));
				Assert.True (File.Exists (f), $"{f} should have been created.");
			}
			Directory.Delete (Path.Combine (Root, path), recursive: true);
		}

		[Test]
		public void Aapt2CompileFiles ()
		{
			var path = Path.Combine (Root, "temp", "Aapt2CompileFiles");
			Directory.CreateDirectory (path);
			var resPath = Path.Combine (path, "res");
			var archivePath = Path.Combine(path, "flata");
			var flatFilePath = Path.Combine(path, "flat");
			Directory.CreateDirectory(resPath);
			Directory.CreateDirectory(archivePath);
			Directory.CreateDirectory(flatFilePath);
			Directory.CreateDirectory (Path.Combine (resPath, "values"));
			Directory.CreateDirectory (Path.Combine (resPath, "layout"));
			File.WriteAllText (Path.Combine (resPath, "values", "strings.xml"), @"<?xml version='1.0' ?><resources><string name='foo'>foo</string></resources>");
			File.WriteAllText (Path.Combine (resPath, "layout", "main.xml"), @"<?xml version='1.0' ?><LinearLayout xmlns:android='http://schemas.android.com/apk/res/android' />");
			List<ITaskItem> files = new List<ITaskItem> ();
			files.Add (CreateTaskItemForResourceFile (resPath, "values", "strings.xml"));
			files.Add (CreateTaskItemForResourceFile (resPath, "layout", "main.xml"));
			var errors = new List<BuildErrorEventArgs> ();
			IBuildEngine engine = new MockBuildEngine (TestContext.Out, errors);
			var task = new Aapt2Compile {
				BuildEngine = engine,
				ToolPath = AndroidSdkTestHelper.GetAapt2Directory (),
				ResourcesToCompile = files.ToArray (),
				ResourceDirectories = new ITaskItem [] { new TaskItem (resPath) },
				FlatArchivesDirectory = archivePath,
				FlatFilesDirectory = flatFilePath,
			};
			Assert.True (task.Execute (), $"task should have succeeded. {string.Join (";", errors.Select (x => x.Message))}");
			var flatArchive = Path.Combine (archivePath, "compiled.flata");
			Assert.False (File.Exists (flatArchive), $"{flatArchive} should not have been created.");
			foreach (var file in files) {
				string f = Path.Combine (flatFilePath, file.GetMetadata ("_FlatFile"));
				Assert.True (File.Exists (f), $"{f} should have been created.");
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
			var flatFilePath = Path.Combine(path, "flat");
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
			var engine = new MockBuildEngine (TestContext.Out, errors);
			var directorySeperator = Path.DirectorySeparatorChar;
			var task = new Aapt2Compile {
				BuildEngine = engine,
				ResourceCaseMap = new ITaskItem [] {
					new TaskItem ($"values{directorySeperator}strings.xml", new Dictionary<string, string> {
						{ "OriginalName", $"Values{directorySeperator}Strings.xml" },
					}),
				},
				ToolPath = AndroidSdkTestHelper.GetAapt2Directory (),
				ResourceDirectories = new ITaskItem [] {
					new TaskItem (resPath, new Dictionary<string, string> () {
							{ "ResourceDirectory", resPath },
						}
					)
				},
				FlatArchivesDirectory = archivePath,
				FlatFilesDirectory = flatFilePath,
			};
			Assert.False (task.Execute (), "task should not have succeeded.");
			Assert.AreEqual (2, errors.Count, $"Two Error should have been raised. {string.Join (" ", errors.Select (e => e.Message))}");
			Assert.AreEqual ($"Resources{directorySeperator}Values{directorySeperator}Strings.xml", errors[0].File, $"`values{directorySeperator}strings.xml` should have been replaced with `Resources{directorySeperator}Values{directorySeperator}Strings.xml`");
			Assert.AreEqual ($"Resources{directorySeperator}Values{directorySeperator}Strings.xml", errors [1].File, $"`values{directorySeperator}strings.xml` should have been replaced with `Resources{directorySeperator}Values{directorySeperator}Strings.xml`");
			Assert.AreEqual (1, errors [0].LineNumber);
			Assert.AreEqual (0, errors [1].LineNumber);
			Directory.Delete (Path.Combine (Root, path), recursive: true);
		}

		[Test]
		public void Aapt2AndroidResgenExtraArgsAreInvalid ()
		{
			var path = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (path);
			var resPath = Path.Combine (path, "res");
			var archivePath = Path.Combine(path, "flata");
			var flatFilePath = Path.Combine(path, "flat");
			Directory.CreateDirectory(resPath);
			Directory.CreateDirectory(archivePath);
			Directory.CreateDirectory (Path.Combine (resPath, "values"));
			Directory.CreateDirectory (Path.Combine (resPath, "layout"));
			File.WriteAllText (Path.Combine (resPath, "values", "strings.xml"), @"<?xml version='1.0' ?><resources><string name='foo'>foo</string></resources>");
			File.WriteAllText (Path.Combine (resPath, "layout", "main.xml"), @"<?xml version='1.0' ?><LinearLayout xmlns:android='http://schemas.android.com/apk/res/android' />");
			File.WriteAllText (Path.Combine (path, "AndroidManifest.xml"), @"<?xml version='1.0' ?><manifest xmlns:android='http://schemas.android.com/apk/res/android' package='Foo.Foo' />");
			File.WriteAllText (Path.Combine (path, "foo.map"), @"a\nb");
			var errors = new List<BuildErrorEventArgs> ();
			var warnings = new List<BuildWarningEventArgs> ();
			var messages = new List<BuildMessageEventArgs> ();
			IBuildEngine engine = new MockBuildEngine (TestContext.Out, errors, warnings, messages);
			var archives = new List<ITaskItem>();
			CallAapt2Compile (engine, resPath, archivePath, flatFilePath);
			var outputFile = Path.Combine (path, "resources.apk");
			var task = new Aapt2Link {
				BuildEngine = engine,
				ToolPath = AndroidSdkTestHelper.GetAapt2Directory (),
				ResourceDirectories = new ITaskItem [] { new TaskItem (resPath) },
				ManifestFiles = new ITaskItem [] { new TaskItem (Path.Combine (path, "AndroidManifest.xml")) },
				CompiledResourceFlatArchive = new TaskItem (Path.Combine (path, "compiled.flata")),
				OutputFile = outputFile,
				AssemblyIdentityMapFile = Path.Combine (path, "foo.map"),
				JavaPlatformJarPath = AndroidSdkTestHelper.GetAndroidJarPath (),
				ExtraArgs = "--no-crunch "
			};
			Assert.False (task.Execute (), "task should have failed.");
			Assert.AreEqual (1, errors.Count, $"One error should have been raised. {string.Join (" ", errors.Select (e => e.Message))}");
			Directory.Delete (Path.Combine (Root, path), recursive: true);
		}

		[Test]
		public void Aapt2AndroidResgenExtraArgsAreSplit ()
		{
			var path = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (path);
			var resPath = Path.Combine (path, "res");
			var archivePath = Path.Combine(path, "flata");
			var flatFilePath = Path.Combine(path, "flat");
			Directory.CreateDirectory(resPath);
			Directory.CreateDirectory(archivePath);
			Directory.CreateDirectory (Path.Combine (resPath, "values"));
			Directory.CreateDirectory (Path.Combine (resPath, "layout"));
			File.WriteAllText (Path.Combine (resPath, "values", "strings.xml"), @"<?xml version='1.0' ?><resources><string name='foo'>foo</string></resources>");
			File.WriteAllText (Path.Combine (resPath, "layout", "main.xml"), @"<?xml version='1.0' ?><LinearLayout xmlns:android='http://schemas.android.com/apk/res/android' />");
			File.WriteAllText (Path.Combine (path, "AndroidManifest.xml"), @"<?xml version='1.0' ?><manifest xmlns:android='http://schemas.android.com/apk/res/android' package='Foo.Foo' />");
			File.WriteAllText (Path.Combine (path, "foo.map"), @"a\nb");
			var errors = new List<BuildErrorEventArgs> ();
			var warnings = new List<BuildWarningEventArgs> ();
			var messages = new List<BuildMessageEventArgs> ();
			IBuildEngine engine = new MockBuildEngine (TestContext.Out, errors, warnings, messages);
			var archives = new List<ITaskItem>();
			CallAapt2Compile (engine, resPath, archivePath, flatFilePath);
			var outputFile = Path.Combine (path, "resources.apk");
			string emitids = Path.Combine (path, "emitids.txt");
			string Rtxt = Path.Combine (path, "R.txt");
			var task = new Aapt2Link {
				BuildEngine = engine,
				ToolPath = AndroidSdkTestHelper.GetAapt2Directory (),
				ResourceDirectories = new ITaskItem [] { new TaskItem (resPath) },
				ManifestFiles = new ITaskItem [] { new TaskItem (Path.Combine (path, "AndroidManifest.xml")) },
				CompiledResourceFlatArchive = new TaskItem (Path.Combine (archivePath, "compiled.flata")),
				OutputFile = outputFile,
				AssemblyIdentityMapFile = Path.Combine (path, "foo.map"),
				JavaPlatformJarPath = AndroidSdkTestHelper.GetAndroidJarPath (),
				ExtraArgs = $@"--no-version-vectors -v --emit-ids ""{emitids}"" --output-text-symbols '{Rtxt}'"
			};
			Assert.True (task.Execute (), $"task should have succeeded. {string.Join (" ", errors.Select (e => e.Message))}");
			Assert.AreEqual (0, errors.Count, $"No errors should have been raised. {string.Join (" ", errors.Select (e => e.Message))}");
			Assert.True (File.Exists (emitids), $"{emitids} should have been created.");
			Assert.True (File.Exists (Rtxt), $"{Rtxt} should have been created.");
			Directory.Delete (Path.Combine (Root, path), recursive: true);
		}

		[Test]
		[TestCase ("1.0", "", "XA0003")]
		[TestCase ("-1", "", "XA0004")]
		[TestCase ("2100000001", "", "XA0004")]
		[TestCase ("1.0", "{abi}{versionCode:D5}", "XA0003")]
		[TestCase ("-1", "{abi}{versionCode:D5}", "XA0004")]
		[TestCase ("2100000001", "{abi}{versionCode:D5}", "XA0004")]
		public void CheckForInvalidVersionCode (string versionCode, string versionCodePattern, string errorCode)
		{
			var path = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (path);
			var resPath = Path.Combine (path, "res");
			var archivePath = Path.Combine (path, "flata");
			var flatFilePath = Path.Combine(path, "flat");
			Directory.CreateDirectory (resPath);
			Directory.CreateDirectory (archivePath);
			Directory.CreateDirectory (Path.Combine (resPath, "values"));
			Directory.CreateDirectory (Path.Combine (resPath, "layout"));
			File.WriteAllText (Path.Combine (resPath, "values", "strings.xml"), @"<?xml version='1.0' ?><resources><string name='foo'>foo</string></resources>");
			File.WriteAllText (Path.Combine (resPath, "layout", "main.xml"), @"<?xml version='1.0' ?><LinearLayout xmlns:android='http://schemas.android.com/apk/res/android' />");
			File.WriteAllText (Path.Combine (path, "AndroidManifest.xml"), $@"<?xml version='1.0' ?><manifest xmlns:android='http://schemas.android.com/apk/res/android' package='Foo.Foo' android:versionCode='{versionCode}' />");
			File.WriteAllText (Path.Combine (path, "foo.map"), @"a\nb");
			var errors = new List<BuildErrorEventArgs> ();
			IBuildEngine engine = new MockBuildEngine (TestContext.Out, errors);
			var archives = new List<ITaskItem> ();
			CallAapt2Compile (engine, resPath, archivePath, flatFilePath);
			var outputFile = Path.Combine (path, "resources.apk");
			var manifestFile = Path.Combine (path, "AndroidManifest.xml");
			var task = new Aapt2Link {
				BuildEngine = engine,
				ToolPath = AndroidSdkTestHelper.GetAapt2Directory (),
				ResourceDirectories = new ITaskItem [] { new TaskItem (resPath) },
				ManifestFiles = new ITaskItem [] { new TaskItem (manifestFile) },
				CompiledResourceFlatArchive = new TaskItem (Path.Combine (path, "compiled.flata")),
				OutputFile = outputFile,
				AssemblyIdentityMapFile = Path.Combine (path, "foo.map"),
				VersionCodePattern = versionCodePattern,
			};
			Assert.False (task.Execute (), "task should have failed.");
			Assert.AreEqual (1, errors.Count, $"One error should have been raised. {string.Join (" ", errors.Select (e => e.Message))}");
			Assert.AreEqual (errorCode, errors [0].Code, $"Error Code should have been {errorCode}");
			Assert.AreEqual (manifestFile, errors [0].File, $"Error File should have been {manifestFile}");
			Directory.Delete (Path.Combine (Root, path), recursive: true);
		}
	}
}
