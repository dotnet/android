using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;
using Xamarin.ProjectTools;
using Microsoft.Android.Build.Tasks;
using TPL = System.Threading.Tasks;

namespace Xamarin.Android.Build.Tests
{
	public class Aapt2Tests : BaseTest
	{
		void CallAapt2Compile (IBuildEngine engine, string dir, string outputPath, string flatFilePath, List<ITaskItem> output = null, int maxInstances = 0, bool keepInDomain = false)
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
				ToolPath = GetPathToAapt2 (),
				ResourcesToCompile = new ITaskItem [] {
					item,
				},
				ResourceDirectories = new ITaskItem [] {
					new TaskItem (dir),
				},
				FlatArchivesDirectory = outputPath,
				FlatFilesDirectory = flatFilePath,
				DaemonMaxInstanceCount = maxInstances,
				DaemonKeepInDomain = keepInDomain,
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
				ToolPath = GetPathToAapt2 (),
				ResourcesToCompile = files.ToArray (),
				ResourceDirectories = new ITaskItem [] { new TaskItem (resPath) },
				FlatArchivesDirectory = archivePath,
				FlatFilesDirectory = flatFilePath,
				DaemonMaxInstanceCount = maxInstances,
				DaemonKeepInDomain = false,
			};
			Assert.True (task.Execute (), $"task should have succeeded. {string.Join (";", errors.Select (x => x.Message))}");
			var daemon = engine.GetRegisteredTaskObjectAssemblyLocal<Aapt2Daemon> (Aapt2Daemon.RegisterTaskObjectKey, RegisteredTaskObjectLifetime.Build);
			Assert.IsNotNull (daemon, "Should have got a Daemon");
			Assert.AreEqual (expectedMax, daemon.MaxInstances, $"Expected {expectedMax} but was {daemon.MaxInstances}");
			Assert.AreEqual (expectedInstances, daemon.CurrentInstances, $"Expected {expectedInstances} but was {daemon.CurrentInstances}");
			Directory.Delete (Path.Combine (Root, path), recursive: true);
		}

		class Aapt2CompileWithProcessorCount : Aapt2Compile
		{
			protected override int ProcessorCount { get; }

			public Aapt2CompileWithProcessorCount (int processorCount) => ProcessorCount = processorCount;
		}

		[Test]
		[Platform ("Linux,MacOsX")]
		public async TPL.Task Aapt2DaemonDrainsStdoutAndPreservesResponses ()
		{
			using var tool = new Aapt2DaemonTool ("""
				while IFS= read -r command; do
				    [ "$command" = quit ] && exit 0
				    printf '%s\n' "$command" >> "$0.commands"
				    while IFS= read -r argument && [ -n "$argument" ]; do
				        printf '%s\n' "$argument" >> "$0.arguments"
				    done
				    for ((i=0; i<128; i++)); do printf '%01024d\n' 0; done
				    printf '%s\n' 'res/values/strings.xml:1: warning: warn: keep this warning' >&2
				    if [ "$command" = fail ]; then
				        printf '%s\n' 'res/values/strings.xml:2: error: invalid resource' Error >&2
				    fi
				    printf 'Done\n' >&2
				done
				""");
			using var daemon = new Aapt2Daemon (tool.Path, 1, 1, TestContext.Out.WriteLine);
			var failed = daemon.QueueCommand (["fail", "argument with spaces", "Ümläüt"], "", CancellationToken.None);
			var succeeded = daemon.QueueCommand (["succeed"], "", CancellationToken.None);
			var jobs = await WaitForDaemonJobsAsync (daemon, [failed, succeeded]);

			Assert.IsFalse (jobs [0].Succeeded);
			Assert.IsTrue (jobs [1].Succeeded);
			CollectionAssert.AreEqual (new [] { "fail", "succeed" }, File.ReadAllLines (tool.Path + ".commands"));
			CollectionAssert.AreEqual (new [] { "argument with spaces", "Ümläüt" }, File.ReadAllLines (tool.Path + ".arguments"));
			Assert.AreEqual (2, jobs [0].Output.Count);
			Assert.IsFalse (jobs [0].Output [0].StdError);
			Assert.IsTrue (jobs [0].Output [1].StdError);
			Assert.IsTrue (jobs [0].Output.All (line => line.Errored && line.JobId == failed));
			Assert.IsTrue (jobs [1].Output.All (line => !line.Errored && line.JobId == succeeded));
			Assert.AreEqual (1, File.ReadAllLines (tool.Path + ".pids").Length, "Completed commands must reuse the daemon.");
			daemon.Dispose ();
			tool.AssertProcessesExited ();
		}

		[TestCase (false)]
		[TestCase (true)]
		[Platform ("Linux,MacOsX")]
		public async TPL.Task Aapt2DaemonCancellationRestartsOnlyTheActiveProcess (bool blockInput)
		{
			using var tool = new Aapt2DaemonTool ("""
				while IFS= read -r command; do
				    [ "$command" = quit ] && exit 0
				    if [ "$command" = block-input ]; then
				        printf '%s\n' "$command" >> "$0.commands"
				        sleep 60 &
				        printf '%s\n' "$!" >> "$0.children"
				        : > "$0.active"
				        wait
				    fi
				    while IFS= read -r argument && [ -n "$argument" ]; do :; done
				    printf '%s\n' "$command" >> "$0.commands"
				    if [ "$command" = block ]; then
				        : > "$0.active"
				        IFS= read -r ignored
				    else
				        printf 'Done\n' >&2
				    fi
				done
				""");
			using var daemon = new Aapt2Daemon (tool.Path, 1, 1, TestContext.Out.WriteLine);
			using var cancellation = new CancellationTokenSource ();
			string command = blockInput ? "block-input" : "block";
			var active = daemon.QueueCommand (blockInput ? [command, new string ('x', 1024 * 1024)] : [command], "", cancellation.Token);
			var canceled = daemon.QueueCommand (["must-not-run"], "", cancellation.Token);
			var next = daemon.QueueCommand (["succeed"], "", CancellationToken.None);
			tool.WaitForMarker (".active");
			cancellation.Cancel ();
			var jobs = await WaitForDaemonJobsAsync (daemon, [active, canceled, next]);

			Assert.IsFalse (jobs [0].Succeeded);
			Assert.IsFalse (jobs [1].Succeeded);
			Assert.IsTrue (jobs [2].Succeeded, string.Join ("\n", jobs [2].Output.Select (line => line.Line)));
			CollectionAssert.AreEqual (new [] { command, "succeed" }, File.ReadAllLines (tool.Path + ".commands"));
			Assert.AreEqual (2, File.ReadAllLines (tool.Path + ".pids").Length);
			daemon.Dispose ();
			tool.AssertProcessesExited ();
		}

		[TestCase ("exit")]
		[TestCase ("stderr")]
		[TestCase ("inherited")]
		[Platform ("Linux,MacOsX")]
		public async TPL.Task Aapt2DaemonRequiresDone (string end)
		{
			var finish = end == "stderr" ? """
				exec 2>&-
				IFS= read -r ignored
				""" : end == "inherited" ? """
				sleep 60 &
				printf '%s\n' "$!" >> "$0.children"
				exit 0
				""" : "exit 0";
			using var tool = new Aapt2DaemonTool ("""
				IFS= read -r command
				while IFS= read -r argument && [ -n "$argument" ]; do :; done
				printf 'diagnostic before EOF\n' >&2
				""" + "\n" + finish);
			using var daemon = new Aapt2Daemon (tool.Path, 1, 1, TestContext.Out.WriteLine);
			var ids = Enumerable.Range (0, 4).Select (_ => daemon.QueueCommand (["compile"], "", CancellationToken.None)).ToArray ();
			var jobs = await WaitForDaemonJobsAsync (daemon, ids, timeoutSeconds: 15);

			Assert.IsTrue (jobs.All (job => !job.Succeeded && job.Output.Count > 0));
			Assert.AreEqual ("diagnostic before EOF", jobs [0].Output [0].Line);
			Assert.IsTrue (jobs [0].Output.All (line => line.Errored));
			daemon.Dispose ();
			tool.AssertProcessesExited ();
		}

		[TestCase (false)]
		[TestCase (true)]
		[Platform ("Linux,MacOsX")]
		public async TPL.Task Aapt2DaemonBoundsMissingOutput (bool exitAfterDone)
		{
			using var tool = new Aapt2DaemonTool ("""
				IFS= read -r command
				while IFS= read -r argument && [ -n "$argument" ]; do :; done
				printf 'Done\n' >&2
				""" + (exitAfterDone ? "\nexit 0\n" : "\nIFS= read -r ignored\n"));
			using var daemon = new Aapt2Daemon (tool.Path, 1, 1, TestContext.Out.WriteLine);
			var outputFile = tool.Path + ".missing";
			var timer = Stopwatch.StartNew ();
			var active = daemon.QueueCommand (["compile"], outputFile, CancellationToken.None);
			var queued = daemon.QueueCommand (["compile"], "", CancellationToken.None);
			var jobs = await WaitForDaemonJobsAsync (daemon, [active, queued], timeoutSeconds: 40);

			Assert.IsTrue (jobs.All (job => !job.Succeeded && job.Output.Count > 0));
			StringAssert.Contains (outputFile, string.Join ("\n", jobs [0].Output.Select (line => line.Line)));
			if (exitAfterDone)
				Assert.Less (timer.Elapsed, TimeSpan.FromSeconds (10), "Daemon exit must interrupt the output-file wait.");
			else
				Assert.GreaterOrEqual (timer.Elapsed, TimeSpan.FromSeconds (29), "Exercise the actual output-file deadline.");
			daemon.Dispose ();
			tool.AssertProcessesExited ();
		}

		[Test]
		[Platform ("Linux,MacOsX")]
		public async TPL.Task Aapt2DaemonDisposeCompletesActiveAndQueuedJobs ()
		{
			using var tool = new Aapt2DaemonTool ("""
				IFS= read -r command
				while IFS= read -r argument && [ -n "$argument" ]; do :; done
				printf '%s\n' "$$" >> "$0.active"
				IFS= read -r ignored
				""");
			using var daemon = new Aapt2Daemon (tool.Path, 2, 2, TestContext.Out.WriteLine);
			var ids = Enumerable.Range (0, 4).Select (_ => daemon.QueueCommand (["block"], "", CancellationToken.None)).ToArray ();
			Assert.IsTrue (SpinWait.SpinUntil (() => File.Exists (tool.Path + ".active") &&
				File.ReadAllLines (tool.Path + ".active").Length == 2, TimeSpan.FromSeconds (5)), "Both workers must be active.");
			var timer = Stopwatch.StartNew ();
			daemon.Dispose ();
			var jobs = await WaitForDaemonJobsAsync (daemon, ids);

			Assert.Less (timer.Elapsed, TimeSpan.FromSeconds (10));
			Assert.IsTrue (jobs.All (job => !job.Succeeded && job.Output.Count > 0));
			Assert.IsFalse (daemon.JobsRunning);
			Assert.IsFalse (daemon.JobsInQueue);
			var rejected = daemon.QueueCommand (["compile"], "", CancellationToken.None);
			Assert.IsFalse (daemon.WaitForJobsToComplete ([rejected]) [0].Succeeded);
			tool.AssertProcessesExited ();
		}

		[TestCase (false)]
		[TestCase (true)]
		[Platform ("Linux,MacOsX")]
		public async TPL.Task Aapt2DaemonBoundsGracefulShutdown (bool ignoreQuit)
		{
			using var tool = new Aapt2DaemonTool ("""
				while IFS= read -r command; do
				    while IFS= read -r argument && [ -n "$argument" ]; do :; done
				    if [ "$command" = quit ]; then
				        : > "$0.quit"
				""" + (ignoreQuit ? "\n        IFS= read -r ignored\n" : "\n") + """
				        exit 0
				    fi
				    printf 'Done\n' >&2
				done
				""");
			using var daemon = new Aapt2Daemon (tool.Path, 1, 1, TestContext.Out.WriteLine);
			var id = daemon.QueueCommand (["compile"], "", CancellationToken.None);
			Assert.IsTrue ((await WaitForDaemonJobsAsync (daemon, [id])) [0].Succeeded);
			var timer = Stopwatch.StartNew ();
			daemon.Stop ();
			tool.WaitForMarker (".quit");
			tool.AssertProcessesExited (timeoutMilliseconds: 10000);
			daemon.Dispose ();

			Assert.Less (timer.Elapsed, TimeSpan.FromSeconds (12));
			if (ignoreQuit)
				Assert.GreaterOrEqual (timer.Elapsed, TimeSpan.FromSeconds (4), "Allow the graceful deadline before killing the owned process.");
		}

		[TestCase (false)]
		[TestCase (true)]
		public async TPL.Task Aapt2DaemonStartupFailureCanBeReplaced (bool registerInDomain)
		{
			var engine = new MockBuildEngine (TestContext.Out);
			var missingTool = Path.Combine (Root, Guid.NewGuid ().ToString ("N"), "missing-aapt2");
			using var failed = Aapt2Daemon.GetInstance (engine, TestContext.Out.WriteLine, missingTool, 2, 2, registerInDomain);
			var ids = Enumerable.Range (0, 4).Select (_ => failed.QueueCommand (["version"], "", CancellationToken.None)).ToArray ();
			var jobs = await WaitForDaemonJobsAsync (failed, ids);
			Assert.IsTrue (jobs.All (job => !job.Succeeded && job.Output.Count > 0));
			var aapt2 = Path.Combine (GetPathToAapt2 (), IsWindows ? "aapt2.exe" : "aapt2");
			using var replacement = Aapt2Daemon.GetInstance (engine, TestContext.Out.WriteLine, aapt2, 1, 1, registerInDomain);
			Assert.AreNotSame (failed, replacement);
			Assert.AreSame (replacement, Aapt2Daemon.GetInstance (engine, TestContext.Out.WriteLine, aapt2, 1, 1, registerInDomain));
			var version = replacement.QueueCommand (["version"], "", CancellationToken.None);
			Assert.IsTrue ((await WaitForDaemonJobsAsync (replacement, [version])) [0].Succeeded);
		}

		[Test]
		[NonParallelizable]
		[Platform ("Linux,MacOsX")]
		public async TPL.Task Aapt2DaemonDisposesReadersWithoutFinalization ()
		{
			var aapt2 = Path.Combine (GetPathToAapt2 (), "aapt2");
			var descriptors = OperatingSystem.IsMacOS () ? "/dev/fd" : "/proc/self/fd";
			for (int i = 0; i < 10; i++)
				await RunDaemonAsync ();
			GC.Collect ();
			GC.WaitForPendingFinalizers ();
			Assert.IsTrue (GC.TryStartNoGCRegion (64 * 1024 * 1024), "Measure explicit cleanup without reader finalization.");
			try {
				int collections = GC.CollectionCount (0);
				int before = Directory.GetFileSystemEntries (descriptors).Length;
				Assert.Greater (before, 2, "Count actual open file descriptors.");
				for (int i = 0; i < 100; i++)
					await RunDaemonAsync ();
				int after = Directory.GetFileSystemEntries (descriptors).Length;

				TestContext.Out.WriteLine ($"Runtime: {typeof (object).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute> ()?.InformationalVersion}");
				TestContext.Out.WriteLine ($"100 daemon start/stop cycles without GC: file descriptors {before} -> {after} (delta {after - before}).");
				Assert.AreEqual (collections, GC.CollectionCount (0), "No collection may mask undisposed readers.");
				Assert.LessOrEqual (after, before + 4, "Readers must close promptly, not retain two pipe handles per daemon until finalization.");
			} finally {
				GC.EndNoGCRegion ();
			}

			async TPL.Task RunDaemonAsync ()
			{
				using var daemon = new Aapt2Daemon (aapt2, 1, 1, TextWriter.Null.WriteLine);
				var id = daemon.QueueCommand (["version"], "", CancellationToken.None);
				var jobs = await WaitForDaemonJobsAsync (daemon, [id]);
				Assert.IsTrue (jobs [0].Succeeded, string.Join ("\n", jobs [0].Output.Select (line => line.Line)));
				daemon.Stop ();
			}
		}

		static TPL.Task<Aapt2Daemon.Job []> WaitForDaemonJobsAsync (Aapt2Daemon daemon, long [] ids, int timeoutSeconds = 10) =>
			TPL.Task.Run (() => daemon.WaitForJobsToComplete (ids)).WaitAsync (TimeSpan.FromSeconds (timeoutSeconds));

		sealed class Aapt2DaemonTool : IDisposable
		{
			readonly string directory = System.IO.Path.Combine (System.IO.Path.GetTempPath (), "aapt2-daemon-" + Guid.NewGuid ().ToString ("N"));

			public string Path => System.IO.Path.Combine (directory, "aapt2");

			public Aapt2DaemonTool (string body)
			{
				Directory.CreateDirectory (directory);
				File.WriteAllText (Path, "#!/bin/bash\nprintf '%s\\n' \"$$\" >> \"$0.pids\"\nprintf 'Ready\\n'\n" + body + "\n", Files.UTF8withoutBOM);
				if (!OperatingSystem.IsWindows ())
					File.SetUnixFileMode (Path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
			}

			public void WaitForMarker (string suffix) =>
				Assert.IsTrue (SpinWait.SpinUntil (() => File.Exists (Path + suffix), TimeSpan.FromSeconds (5)), $"The daemon must reach {suffix}.");

			public void AssertProcessesExited (int timeoutMilliseconds = 5000)
			{
				FileAssert.Exists (Path + ".pids");
				foreach (var pid in File.ReadAllLines (Path + ".pids")) {
					try {
						using var process = Process.GetProcessById (int.Parse (pid));
						Assert.IsTrue (process.WaitForExit (timeoutMilliseconds), "The daemon must terminate its owned process.");
					} catch (ArgumentException) {
						// The owned process has already been reaped.
					}
				}
			}

			public void Dispose ()
			{
				foreach (var suffix in new [] { ".pids", ".children" }) {
					if (!File.Exists (Path + suffix))
						continue;
					foreach (var pid in File.ReadAllLines (Path + suffix)) {
						try {
							using var process = Process.GetProcessById (int.Parse (pid));
							if (!process.HasExited)
								process.Kill (entireProcessTree: true);
							Assert.IsTrue (process.WaitForExit (5000), "Fixture-owned processes must stop before cleanup.");
						} catch (ArgumentException) {
							// The fixture process has already been reaped.
						}
					}
				}
				Directory.Delete (directory, recursive: true);
			}
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
			var platform = AndroidSdkResolver.GetMaxInstalledPlatform ();
			var outputFile = Path.Combine (path, "resources.apk");
			var referencePath = CreateFauxReferencesDirectory (Path.Combine ("temp", TestName, "references"), new [] {
				new ApiInfo { Id = "28", Level = 28, Name = "Pie", FrameworkVersion = "v9.0", Stable = true },
			});
			var manifestFile = Path.Combine (path, "AndroidManifest.xml");
			var task = new Aapt2Link {
				BuildEngine = engine,
				ToolPath = GetPathToAapt2 (),
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
				JavaPlatformJarPath = AndroidSdkResolver.GetAndroidJarPath (platform),
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
			using (var apk = ZipHelper.OpenZip (outputFile)) {
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
				ToolPath = GetPathToAapt2 (),
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
			using (var apk = ZipHelper.OpenZip (flatArchive)) {
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
				ToolPath = GetPathToAapt2 (),
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
				ToolPath = GetPathToAapt2 (),
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
				ToolPath = GetPathToAapt2 (),
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
			var platform = AndroidSdkResolver.GetMaxInstalledPlatform ();
			var task = new Aapt2Link {
				BuildEngine = engine,
				ToolPath = GetPathToAapt2 (),
				ResourceDirectories = new ITaskItem [] { new TaskItem (resPath) },
				ManifestFiles = new ITaskItem [] { new TaskItem (Path.Combine (path, "AndroidManifest.xml")) },
				CompiledResourceFlatArchive = new TaskItem (Path.Combine (path, "compiled.flata")),
				OutputFile = outputFile,
				AssemblyIdentityMapFile = Path.Combine (path, "foo.map"),
				JavaPlatformJarPath = AndroidSdkResolver.GetAndroidJarPath (platform),
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
			var platform = AndroidSdkResolver.GetMaxInstalledPlatform ();
			string emitids = Path.Combine (path, "emitids.txt");
			string Rtxt = Path.Combine (path, "R.txt");
			var task = new Aapt2Link {
				BuildEngine = engine,
				ToolPath = GetPathToAapt2 (),
				ResourceDirectories = new ITaskItem [] { new TaskItem (resPath) },
				ManifestFiles = new ITaskItem [] { new TaskItem (Path.Combine (path, "AndroidManifest.xml")) },
				CompiledResourceFlatArchive = new TaskItem (Path.Combine (archivePath, "compiled.flata")),
				OutputFile = outputFile,
				AssemblyIdentityMapFile = Path.Combine (path, "foo.map"),
				JavaPlatformJarPath = AndroidSdkResolver.GetAndroidJarPath (platform),
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
				ToolPath = GetPathToAapt2 (),
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
