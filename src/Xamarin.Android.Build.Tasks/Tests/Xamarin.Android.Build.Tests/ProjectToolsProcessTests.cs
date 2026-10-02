using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Xamarin.Android.Tools;
using Xamarin.Android.Tools.VSWhere;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[NonParallelizable]
	[Platform (Exclude = "Win")]
	public class ProjectToolsProcessTests : HostProcessFixture
	{
		[TestCase (0)]
		[TestCase (7)]
		public void BuilderCapturesBothStreamsOnce (int exitCode)
		{
			string script = CreateScript ("build", LargeOutput + $"\nexit {exitCode}");
			string log = Path.Combine (directory, "build-process.log");
			var arguments = new object [] { StartInfo (script), log, 10000, false };
			bool result = WithDeadline (() => Invoke<bool> (typeof (Builder), "RunBuildProcess", null,
				[typeof (ProcessStartInfo), typeof (string), typeof (int), typeof (bool).MakeByRefType ()], arguments));
			Assert.AreEqual (exitCode == 0, result);
			Assert.IsFalse ((bool) arguments [3]);
			string output = File.ReadAllText (log);
			AssertOutput (output);
			Assert.AreEqual ($"ExitCode: {exitCode}", Lines (output).Last ());
		}

		[TestCase (false)]
		[TestCase (true)]
		public void BuilderRetainsNativeCrashDetection (bool standardError)
		{
			string script = CreateScript ("crash", "echo 'Got a SIGSEGV while executing native code'" + (standardError ? " >&2" : ""));
			var arguments = new object [] { StartInfo (script), Path.Combine (directory, "crash.log"), 10000, false };
			Assert.IsTrue (WithDeadline (() => Invoke<bool> (typeof (Builder), "RunBuildProcess", null,
				[typeof (ProcessStartInfo), typeof (string), typeof (int), typeof (bool).MakeByRefType ()], arguments)));
			Assert.IsTrue ((bool) arguments [3]);
		}

		[Test]
		public void BuilderDrainWaitRemainsBounded ()
		{
			var incomplete = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
			var timer = Stopwatch.StartNew ();
			Assert.IsFalse (Builder.WaitForRedirectedOutput (Task.CompletedTask, incomplete.Task));
			Assert.Less (timer.Elapsed, TimeSpan.FromSeconds (5));
		}

		[TestCase (0)]
		[TestCase (7)]
		public void DotNetCapturesBothStreamsOnce (int exitCode)
		{
			string script = CreateScript ("dotnet", LargeOutput + $"\nexit {exitCode}");
			using var process = Process.Start (StartInfo (script)) ?? throw new InvalidOperationException ("Failed to start fixture.");
			var output = new StringBuilder ();
			bool result = WithDeadline (() => Invoke<bool> (typeof (DotNetCLI), "ReadOutput", null,
				[typeof (Process), typeof (StringBuilder), typeof (int), typeof (int)], process, output, 10000, 2000));
			Assert.AreEqual (exitCode == 0, result);
			AssertOutput (output.ToString ());
			Assert.AreEqual ($"Exit Code: {exitCode}", Lines (output.ToString ()).Last ());
		}

		[TestCase (0)]
		[TestCase (7)]
		public void GradleCapturesBothStreamsOnce (int exitCode)
		{
			var gradle = new GradleCLI {
				GradlePath = CreateScript ("gradle", LargeOutput + $"\nexit {exitCode}"),
				ProjectDirectory = directory,
				ProcessLogFile = Path.Combine (directory, "gradle.log"),
			};
			Assert.AreEqual (exitCode == 0, WithDeadline (() => gradle.Execute ()));
			string output = File.ReadAllText (gradle.ProcessLogFile);
			AssertOutput (output);
			Assert.AreEqual ($"Exit Code: {exitCode}", Lines (output).Last ());
		}

		[TestCase (0)]
		[TestCase (7)]
		public void SevenZipCapturesBothStreamsOnce (int exitCode)
		{
			CreateScript ("7z", LargeOutput + $"\nexit {exitCode}");
			using var output = new StringWriter ();
			var previous = Console.Out;
			try {
				Console.SetOut (output);
				using var archive = new SevenZipHelper (Path.Combine (directory, "archive.7z"));
				Assert.AreEqual (exitCode == 0, WithDeadline (() => archive.ExtractAll (directory)));
			} finally {
				Console.SetOut (previous);
			}
			AssertOutput (output.ToString ());
		}

		[Test]
		public void SevenZipPreservesPathsWithSpaces ()
		{
			string argumentsFile = Path.Combine (directory, "arguments");
			CreateScript ("7z", $"printf '%s\\n' \"$@\" > {Quote (argumentsFile)}");
			string archivePath = Path.Combine (directory, "archive with spaces.7z");
			string destination = Path.Combine (directory, "destination with spaces");
			using var archive = new SevenZipHelper (archivePath);
			Assert.IsTrue (WithDeadline (() => archive.ExtractAll (destination)));
			CollectionAssert.AreEqual (new [] { "x", archivePath, "-o" + destination }, File.ReadAllLines (argumentsFile));
		}

		[TestCase ("builder")]
		[TestCase ("dotnet")]
		[TestCase ("gradle")]
		[TestCase ("sevenzip")]
		[TestCase ("nuget")]
		public void TimeoutTerminatesProcessTree (string helper)
		{
			string childPidFile = Path.Combine (directory, "child.pid");
			processIds.Add (childPidFile);
			string script = CreateScript (helper == "sevenzip" ? "7z" : helper, $$"""
				sleep 120 &
				echo $! > {{Quote (childPidFile)}}
				echo timeout-stdout
				echo timeout-stderr >&2
				wait
				""");
			var timer = Stopwatch.StartNew ();
			WithDeadline (() => {
				switch (helper) {
				case "builder":
					Assert.IsFalse (Invoke<bool> (typeof (Builder), "RunBuildProcess", null,
						[typeof (ProcessStartInfo), typeof (string), typeof (int), typeof (bool).MakeByRefType ()],
						StartInfo (script), Path.Combine (directory, "timeout.log"), 500, false));
					StringAssert.Contains ("Build Timed Out!", File.ReadAllText (Path.Combine (directory, "timeout.log")));
					break;
				case "dotnet":
					using (var process = Process.Start (StartInfo (script)) ?? throw new InvalidOperationException ("Failed to start fixture.")) {
						var output = new StringBuilder ();
						Assert.IsFalse (Invoke<bool> (typeof (DotNetCLI), "ReadOutput", null,
							[typeof (Process), typeof (StringBuilder), typeof (int), typeof (int)], process, output, 500, 2000));
						StringAssert.Contains ("Process timed out", output.ToString ());
					}
					break;
				case "gradle":
					var gradle = new GradleCLI { GradlePath = script, ProjectDirectory = directory };
					Assert.IsFalse (Invoke<bool> (typeof (GradleCLI), "Execute", gradle, [typeof (int), typeof (string [])], 500, new string [0]));
					StringAssert.Contains ("Exit Code: <timed out>", File.ReadAllText (gradle.ProcessLogFile));
					break;
				case "sevenzip":
					using (var archive = new SevenZipHelper ("archive.7z"))
						Assert.IsFalse (Invoke<bool> (typeof (SevenZipHelper), "ExtractAll", archive, [typeof (string), typeof (int)], directory, 500));
					break;
				case "nuget":
					var path = Invoke<string> (typeof (FileSystemUtils), "FindNugetGlobalPackageFolder", null,
						[typeof (ProcessStartInfo), typeof (int)], StartInfo (script), 500);
					Assert.AreEqual (DefaultNugetPath, path);
					break;
				}
				return true;
			});
			Assert.Less (timer.Elapsed, TimeSpan.FromSeconds (10), "Timeout was followed by an unbounded wait.");
			Assert.IsTrue (File.Exists (childPidFile), "Fixture child was not started.");
			AssertProcessExited (childPidFile);
		}

		static string DefaultNugetPath => Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.UserProfile), ".nuget", "packages");

		[TestCase ("builder")]
		[TestCase ("dotnet")]
		public void InheritedOutputDoesNotOutliveLogSink (string helper)
		{
			string childPidFile = Path.Combine (directory, "inherited.pid");
			processIds.Add (childPidFile);
			string script = CreateScript ("inherited", $$"""
				( sleep 6; echo late-stdout; echo late-stderr >&2 ) &
				echo $! > {{Quote (childPidFile)}}
				echo ready-stdout
				echo ready-stderr >&2
				""");
			string log = Path.Combine (directory, "inherited.log");
			var output = new StringBuilder ();
			var timer = Stopwatch.StartNew ();
			Assert.IsTrue (WithDeadline (() => {
				if (helper == "builder")
					return Invoke<bool> (typeof (Builder), "RunBuildProcess", null,
						[typeof (ProcessStartInfo), typeof (string), typeof (int), typeof (bool).MakeByRefType ()],
						StartInfo (script), log, 10000, false);
				using var process = Process.Start (StartInfo (script)) ?? throw new InvalidOperationException ("Failed to start fixture.");
				return Invoke<bool> (typeof (DotNetCLI), "ReadOutput", null,
					[typeof (Process), typeof (StringBuilder), typeof (int), typeof (int)], process, output, 10000, 200);
			}));
			Assert.Less (timer.Elapsed, TimeSpan.FromSeconds (5), "Inherited output defeated the bounded drain.");
			string snapshot = helper == "builder" ? File.ReadAllText (log) : output.ToString ();
			StringAssert.Contains ("ready-stdout", snapshot);
			StringAssert.Contains ("ready-stderr", snapshot);
			AssertProcessExited (childPidFile);
			Assert.AreEqual (snapshot, helper == "builder" ? File.ReadAllText (log) : output.ToString (), "Late callback mutated a finalized sink.");
		}

		[Test]
		public void NugetPreservesPathAndFailureContracts ()
		{
			string path = Path.Combine (directory, "packages:with:colons");
			string script = CreateScript ("nuget", $"printf '%s\\n' {Quote ("global-packages: " + path)}\n" + LargeOutput);
			Assert.AreEqual (path, WithDeadline (() => Invoke<string> (typeof (FileSystemUtils), "FindNugetGlobalPackageFolder", null,
				[typeof (ProcessStartInfo), typeof (int)], StartInfo (script), 10000)));
			script = CreateScript ("nuget-failure", "echo nonzero-diagnostic >&2\nexit 7");
			using var errors = new StringWriter ();
			var previous = Console.Error;
			try {
				Console.SetError (errors);
				Assert.AreEqual (DefaultNugetPath, WithDeadline (() => Invoke<string> (typeof (FileSystemUtils), "FindNugetGlobalPackageFolder", null,
					[typeof (ProcessStartInfo), typeof (int)], StartInfo (script), 10000)));
			} finally {
				Console.SetError (previous);
			}
			StringAssert.Contains ("exited with value 7", errors.ToString ());
			StringAssert.Contains ("nonzero-diagnostic", errors.ToString ());
		}

		[TestCase (0)]
		[TestCase (7)]
		public void JavaVersionDrainsBothStreams (int exitCode)
		{
			CreateScript ("jdk/bin/java", LargeOutput + $"\nexit {exitCode}");
			string version = WithDeadline (AndroidSdkResolver.GetJavaSdkVersionString);
			AssertOutput (version);
			Assert.Less (version.IndexOf ("stdout-tail", StringComparison.Ordinal), version.IndexOf ("stderr-0000", StringComparison.Ordinal),
				"Java version contract is stdout followed by stderr.");
			Assert.AreEqual (version, AndroidSdkResolver.GetJavaSdkVersionString (), "Java version cache changed.");
		}

		[Test]
		public void SdkPathDrainsAllOutputBeforeParsingFirstLine ()
		{
			string script = CreateScript ("paths", $"printf '%s\\n' {Quote (directory)}\n" +
				LargeOutput.Replace ("emit stderr >&2 &", "").Replace ("printf stderr-tail >&2", ""));
			Assert.AreEqual (directory, WithDeadline (() => Invoke<string> (typeof (AndroidSdkResolver), "ReadPath", null,
				[typeof (ProcessStartInfo)], StartInfo (script))));
		}

		[TestCase ("")]
		[TestCase ("missing-sdk")]
		public void SdkPathPreservesMissingPathContract (string output)
		{
			string script = CreateScript ("missing-path", $"printf '%s' {Quote (output)}");
			Assert.IsNull (WithDeadline (() => InvokeRaw (typeof (AndroidSdkResolver), "ReadPath", null,
				[typeof (ProcessStartInfo)], StartInfo (script))));
		}

		[Test]
		public void VsWhereDrainsOutputBeforeWait ()
		{
			string script = CreateScript ("vswhere", LargeOutput.Replace ("emit stderr >&2 &", "").Replace ("printf stderr-tail >&2", ""));
			string output = WithDeadline (() => Invoke<string> (typeof (MSBuildLocator), "Exec", null,
				[typeof (string), typeof (string)], script, ""));
			CollectionAssert.AreEqual (Enumerable.Range (0, OutputLineCount).Select (i => $"stdout-{i:0000}:{Padding}").Append ("stdout-tail"), Lines (output));
		}

		[TestCase ("javac")]
		[TestCase ("jar")]
		public void JarFailurePreservesBothDiagnostics (string tool)
		{
			var builder = new JarContentBuilder {
				BaseDirectory = directory,
				JavaSourceFileName = "Payload.java",
				JavaSourceText = "public class Payload {}",
				JarFileName = "payload.jar",
				JavacFullPath = CreateScript ("javac", tool == "javac" ? LargeOutput + "\nexit 7" : ": > Payload.class"),
				JarFullPath = CreateScript ("jar", LargeOutput + "\nexit 7"),
			};
			var error = Assert.Throws<InvalidOperationException> (() => WithDeadline (builder.Build));
			Assert.IsNotNull (error);
			StringAssert.StartsWith ($"`{(tool == "javac" ? "Javac" : "Jar")}` command line tool did not successfully finish:", error.Message);
			AssertOutput (error.Message.Substring (error.Message.IndexOf (":", StringComparison.Ordinal) + 2));
		}

		[Test]
		public void JarLaunchesOnceAndPreservesArchiveContent ()
		{
			var jdk = Environment.GetEnvironmentVariable ("HOST_PROCESS_TEST_JDK") ?? previousJdk ??
				JdkInfo.GetKnownSystemJdkInfos ().FirstOrDefault ()?.HomePath;
			if (string.IsNullOrEmpty (jdk) || !File.Exists (Path.Combine (jdk, "bin", "javac")) || !File.Exists (Path.Combine (jdk, "bin", "jar"))) {
				Assert.Ignore ("A JDK is required; set HOST_PROCESS_TEST_JDK to select it.");
				return;
			}
			string invocations = Path.Combine (directory, "jar-invocations");
			for (int i = 0; i < 1024; i++)
				File.WriteAllBytes (Path.Combine (directory, $"Extra{i:0000}{new string ('x', 64)}.class"), []);
			var builder = new JarContentBuilder {
				BaseDirectory = directory,
				JavaSourceFileName = "Payload.java",
				JavaSourceText = "public class Payload { public int value() { return 42; } }",
				JarFileName = "payload.jar",
				JavacFullPath = CreateScript ("javac", LargeOutput + $"\nexec {Quote (Path.Combine (jdk, "bin", "javac"))} \"$@\""),
				JarFullPath = CreateScript ("jar", $"echo jar >> {Quote (invocations)}\n" + LargeOutput + $"\nexec {Quote (Path.Combine (jdk, "bin", "jar"))} \"$@\""),
			};
			byte [] bytes = WithDeadline (builder.Build);
			CollectionAssert.AreEqual (new [] { "jar" }, File.ReadAllLines (invocations));
			using var stream = new MemoryStream (bytes);
			using var archive = new ZipArchive (stream, ZipArchiveMode.Read);
			Assert.AreEqual (1025, archive.Entries.Count (entry => entry.FullName.EndsWith (".class", StringComparison.Ordinal)));
			var payload = archive.GetEntry ("Payload.class");
			Assert.IsNotNull (payload);
			using var content = payload.Open ();
			byte [] magic = new byte [4];
			Assert.AreEqual (magic.Length, content.Read (magic, 0, magic.Length));
			CollectionAssert.AreEqual (new byte [] { 0xca, 0xfe, 0xba, 0xbe }, magic);
			Assert.IsNotNull (archive.GetEntry ("META-INF/MANIFEST.MF"));
		}
	}
}
