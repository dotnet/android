using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture, NonParallelizable]
	public class FastDeployCommandTests
	{
		[Test]
		public void NonzeroOverrideCommandFailsDeployment (
			[Values (
				"'-c' 'find ",
				"rm -f files/.__override__/stale.dll",
				"rm -f files/.__override__/arm64-v8a/Foo.dll",
				"mkdir -p files/.__override__",
				"cp -p",
				"if test -f")] string command,
			[Values ("", "error: closed")] string output)
		{
			using var device = new CommandDevice ();
			device.FailCommand (command, output);

			Assert.IsFalse (device.Run (), command);
			Assert.AreEqual ("XA0129", device.Errors.Single ().Code);
			Assert.IsFalse (device.HasManifest, "Failed deployment must not save a successful local manifest.");
			Assert.IsFalse (device.Commands.Any (c => c.Contains ("printf %s")), "Failed deployment must not mark remote manifests.");
			Assert.That (device.Commands.Last (), Does.Contain (command), "Deployment must stop at the failed command.");
		}

		[TestCase ("", "XA0129")]
		[TestCase ("error: closed", "XA0129")]
		[TestCase ("run-as: package not debuggable", "XA0134")]
		public void EmbeddedDeploymentReportsOverrideRemovalFailure (string output, string code)
		{
			using var device = new CommandDevice ();
			device.FailCommand ("rm -rf files/.__override__", output);

			Assert.IsFalse (device.Run (embedded: true));
			Assert.AreEqual (code, device.Errors.Single ().Code);
		}

		[TestCase ("cp: write failed", "XA0129")]
		[TestCase ("run-as: package not debuggable", "XA0134")]
		public void LegacyShellFailureTextStillFailsDeployment (string output, string code)
		{
			using var device = new CommandDevice ();
			device.FailCommand ("cp -p", output, exitCode: 0);

			Assert.IsFalse (device.Run ());
			Assert.AreEqual (code, device.Errors.Single ().Code);
			Assert.IsFalse (device.HasManifest);
		}

		[Test]
		public void FailedPackageQueryStopsDeployment (
			[Values (false, true)] bool readlink,
			[Values ("", "error: closed")] string output)
		{
			using var device = new CommandDevice ();
			if (readlink) {
				device.WriteResult ("package", 1, "Permission denied");
			}
			string command = readlink ? "readlink -f ." : "pwd";
			device.FailCommand (command, output);

			Assert.IsFalse (device.Run ());
			Assert.AreEqual ("XA0129", device.Errors.Single ().Code);
			Assert.That (device.Commands.Last (), Does.Contain (command));
		}

		[TestCase (false, "")]
		[TestCase (false, "error: closed")]
		[TestCase (true, "")]
		[TestCase (true, "error: closed")]
		public void FailedSymlinkCommandFallsBackToCopy (bool incremental, string output)
		{
			using var device = new CommandDevice ();
			if (incremental) {
				Assert.IsTrue (device.Run (symlink: true));
				device.AddFile ("Bar.dll");
			}
			device.FailCommand ("ln -sf", output);

			Assert.IsTrue (device.Run (symlink: true), "A successful copy fallback must recover a failed symlink update.");
			Assert.IsEmpty (device.Errors);
			Assert.IsTrue (device.HasManifest);
			Assert.That (device.Commands, Has.Some.Contain ("cp -p"));
			var failedCommand = device.Commands.Last (c => c.Contains ("ln -sf"));
			Assert.AreEqual (!incremental, failedCommand.Contains ("for f in"), "Exercise both glob and incremental batch updates.");
		}

		[TestCase ("")]
		[TestCase ("error: closed")]
		public void FailedFallbackClearStopsDeployment (string output)
		{
			using var device = new CommandDevice ();
			device.FailCommand ("ln -sf", "error: closed");
			device.FailCommand ("rm -rf files/.__override__", output, second: true);

			Assert.IsFalse (device.Run (symlink: true));
			Assert.AreEqual ("XA0129", device.Errors.Single ().Code);
			Assert.IsFalse (device.HasManifest);
			Assert.That (device.Commands.Last (), Does.Contain ("rm -rf files/.__override__"));
		}

		[TestCase (0, "")]
		[TestCase (1, "find: 'files/.__override__': No such file or directory")]
		public void MissingOrEmptyOverrideDirectoryStillDeploys (int exitCode, string output)
		{
			using var device = new CommandDevice ();
			device.WriteResult ("override", exitCode, output);

			Assert.IsTrue (device.Run ());
			Assert.IsEmpty (device.Errors);
			Assert.IsTrue (device.HasManifest);
			Assert.That (device.Commands, Has.Some.Contain ("cp -p"));
		}

		[Test]
		public void PermissionDeniedPackageQueryStillUsesReadlinkFallback ()
		{
			using var device = new CommandDevice ();
			device.WriteResult ("package", 1, "Permission denied");

			Assert.IsTrue (device.Run ());
			Assert.IsEmpty (device.Errors);
			Assert.That (device.Commands, Has.Some.Contain ("readlink -f ."));
		}

		[TestCase ("run-as: unknown package: com.example.app")]
		[TestCase ("run-as: package not debuggable: com.example.app")]
		public void RecoverablePackageQueryStillInstalls (string output)
		{
			using var device = new CommandDevice ();
			device.WriteResult ("package", 1, output);
			device.FailCommand ("rm -rf files/.__override__", output);

			Assert.IsTrue (device.Run (embedded: true, install: true));
			Assert.IsEmpty (device.Errors);
			Assert.That (device.Commands, Has.Some.Contain (" install "));
			Assert.IsFalse (device.Commands.Any (c => c.Contains ("rm -rf files/.__override__")));
		}

		[TestCase ("rm")]
		[TestCase ("ln")]
		public void GlobScriptStopsOnIntermediateCommandFailure (string command)
		{
			using var device = new CommandDevice ();
			Assert.IsTrue (device.Run (symlink: true));

			Assert.AreNotEqual (0, device.RunGlobScript (command),
				"A later successful loop iteration must not mask a failed removal or symlink.");
		}

		sealed class CommandDevice : IDisposable
		{
			const string StagingRoot = "/data/local/tmp/fastdeploy/com.example.app/0";
			const string Script = """
				#!/bin/bash
				dir="$(dirname "$0")"
				printf '%s\n' "$*" >> "$dir/adb.log"
				for fault in fault second-fault; do
					if [ -f "$dir/$fault" ] && [[ "$*" == *"$(cat "$dir/$fault")"* ]]; then
						cat "$dir/$fault.out" >&2
						exit "$(cat "$dir/$fault.exit")"
					fi
				done
				case "$*" in
					"devices -l")
						echo "List of devices attached"
						echo "emulator-5554 device product:test model:test device:test"
						;;
					*"pwd"*"pidof"*)
						cat "$dir/package.out"
						exit "$(cat "$dir/package.exit")"
						;;
					*"readlink -f ."*)
						echo "/data/user/0/com.example.app"
						;;
					*"shell find "*)
						cat "$dir/staging.out"
						;;
					*"run-as"*"find "*)
						cat "$dir/override.out"
						exit "$(cat "$dir/override.exit")"
						;;
					*"remote="*"override="*)
						hash="$(cat "$dir/hash" 2>/dev/null)"
						echo "remote=$hash"
						echo "override=$hash"
						;;
					*"printf %s '"*)
						if [[ "$*" =~ printf\ %s\ \'([0-9a-fA-F]+)\' ]]; then
							printf '%s' "${BASH_REMATCH[1]}" > "$dir/hash"
						fi
						;;
				esac
				exit 0
				""";

			readonly string directory = Path.Combine (Path.GetTempPath (), "fastdeploy-commands-" + Guid.NewGuid ().ToString ("N"));
			readonly List<ITaskItem> files = [];
			public List<BuildErrorEventArgs> Errors { get; } = [];
			public IReadOnlyList<string> Commands => File.ReadAllLines (Path.Combine (directory, "adb.log"));
			public bool HasManifest => Directory.GetFiles (Path.Combine (directory, "obj"), "manifest.json", SearchOption.AllDirectories).Length > 0;

			public CommandDevice ()
			{
				if (OperatingSystem.IsWindows ()) {
					Assert.Ignore ("The fake adb is a bash script.");
					return;
				}
				Directory.CreateDirectory (Path.Combine (directory, "obj"));
				var adb = Path.Combine (directory, "adb");
				File.WriteAllText (adb, Script);
				File.SetUnixFileMode (adb, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
				WriteResult ("package", 0, "/data/user/0/com.example.app");
				WriteResult ("override", 0, "files/.__override__/stale.dll|1|1700000000");
				AddFile ("Foo.dll");
			}

			public void AddFile (string name)
			{
				string path = Path.Combine (directory, name);
				File.WriteAllText (path, "foo");
				var item = new TaskItem (path);
				item.SetMetadata ("TargetPath", "arm64-v8a/" + name);
				files.Add (item);
				File.AppendAllText (Path.Combine (directory, "staging.out"), $"{StagingRoot}/arm64-v8a/{name}|3|1700000000\n");
			}

			public void FailCommand (string command, string output, bool second = false, int exitCode = 1)
			{
				string fault = second ? "second-fault" : "fault";
				File.WriteAllText (Path.Combine (directory, fault), command);
				WriteResult (fault, exitCode, output);
			}

			public void WriteResult (string name, int exitCode, string output)
			{
				File.WriteAllText (Path.Combine (directory, name + ".exit"), exitCode.ToString ());
				File.WriteAllText (Path.Combine (directory, name + ".out"), output);
			}

			public bool Run (bool symlink = false, bool embedded = false, bool install = false)
			{
				string apk = Path.Combine (directory, "app.apk");
				if (install) {
					File.WriteAllText (apk, "");
				}
				var task = new FastDeploy {
					BuildEngine = new MockBuildEngine (TestContext.Out, Errors),
					AdbToolPath = directory,
					AppFileTransferMode = symlink ? "Symlink" : "Copy",
					EmbedAssembliesIntoApk = embedded,
					FastDevFiles = files.ToArray (),
					IntermediateOutputPath = Path.Combine (directory, "obj"),
					PackageName = "com.example.app",
					PackageFile = install ? apk : null,
					PrimaryCpuAbi = "arm64-v8a",
					UploadFlagFile = Path.Combine (directory, "obj", "upload.flag"),
				};
				return task.Execute ();
			}

			public int RunGlobScript (string failingCommand)
			{
				string staging = Path.Combine (directory, "staging");
				string target = Path.Combine (directory, "override");
				Directory.CreateDirectory (staging);
				Directory.CreateDirectory (target);
				foreach (string name in new [] { "Bar.dll", "Foo.dll", "Zoo.dll" }) {
					File.WriteAllText (Path.Combine (staging, name), "foo");
					File.WriteAllText (Path.Combine (target, name), "old");
				}
				string executable = Path.Combine (directory, failingCommand);
				File.WriteAllText (executable, $"""
					#!/bin/sh
					case "$*" in *Foo.dll*) exit 137;; esac
					exec /bin/{failingCommand} "$@"
					""");
				if (!OperatingSystem.IsWindows ()) {
					File.SetUnixFileMode (executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
				}

				string invocation = Commands.Single (c => c.Contains ("for f in"));
				const string shellPrefix = "'sh' '-c' ";
				string script = invocation.Substring (invocation.IndexOf (shellPrefix, StringComparison.Ordinal) + shellPrefix.Length)
					.Replace (StagingRoot + "/arm64-v8a", staging)
					.Replace ("files/.__override__/arm64-v8a", target);
				var startInfo = new ProcessStartInfo ("/bin/sh") { UseShellExecute = false };
				startInfo.ArgumentList.Add ("-c");
				startInfo.ArgumentList.Add ("/bin/sh -c " + script);
				startInfo.Environment ["PATH"] = directory + Path.PathSeparator + Environment.GetEnvironmentVariable ("PATH");
				using var process = Process.Start (startInfo);
				Assert.IsNotNull (process);
				process.WaitForExit ();
				return process.ExitCode;
			}

			public void Dispose ()
			{
				Directory.Delete (directory, recursive: true);
			}
		}
	}
}
