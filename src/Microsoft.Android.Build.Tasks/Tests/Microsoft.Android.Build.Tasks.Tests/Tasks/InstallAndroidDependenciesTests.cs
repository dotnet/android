using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Tar;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using NUnit.Framework;
using Task = System.Threading.Tasks.Task;
using TaskItem = Microsoft.Build.Utilities.TaskItem;
using Xamarin.Installer.Build.Tasks;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class InstallAndroidDependenciesTests
{
	[Test]
	public void InstallAndroidDependenciesPassesPackagesToAndroidCli ()
	{
		var root = Path.Combine (Path.GetTempPath (), Path.GetRandomFileName ());
		Directory.CreateDirectory (root);
		try {
			// A fake Android CLI that writes its arguments to args.txt.
			string cli;
			if (OperatingSystem.IsWindows ()) {
				cli = Path.Combine (root, "android.cmd");
				File.WriteAllText (cli, "@echo %* > \"%~dp0args.txt\"\r\n");
			} else {
				cli = Path.Combine (root, "android");
				File.WriteAllText (cli, "#!/bin/sh\necho \"$@\" > \"$(dirname \"$0\")/args.txt\"\n");
				File.SetUnixFileMode (cli, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
			}

			var errors = new List<BuildErrorEventArgs> ();
			var task = new InstallAndroidDependencies {
				BuildEngine = new MockBuildEngine (TestContext.Out, errors, [], []),
				// Relative paths resolve against the project directory, not the process working directory.
				TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment (root, new Dictionary<string, string> ()),
				AndroidSdkPath = "sdk",
				AcceptAndroidSDKLicenses = true,
				InstallJavaDependencies = false,
				AndroidCliPath = Path.GetFileName (cli),
				JavaDependencies = [],
				Dependencies = [
					new TaskItem ("build-tools/37.0.0", new Dictionary<string, string> { { "Version", "37.0.0" } }),
					new TaskItem ("platform-tools", new Dictionary<string, string> { { "Version", "37.0.3" } }),
					new TaskItem ("ndk-bundle", new Dictionary<string, string> { { "Version", "27.2.12479018" } }),
					new TaskItem ("platforms/android-37.0"),
				],
			};
			Assert.IsTrue (task.Execute (), string.Join (Environment.NewLine, errors.Select (e => e.Message)));

			var args = File.ReadAllText (Path.Combine (root, "args.txt")).Trim ();
			StringAssert.Contains ($"--no-metrics --sdk={Path.Combine (root, "sdk")} sdk install", args);
			StringAssert.EndsWith ("build-tools/37.0.0 platform-tools@37.0.3 ndk/27.2.12479018 platforms/android-37.0", args);
		} finally {
			Directory.Delete (root, recursive: true);
		}
	}

	[TestCase (0)]
	[TestCase (5000)]
	public void AndroidCliFailureIncludesOutputTail (int paddingLength)
	{
		var root = Directory.CreateTempSubdirectory ("android-cli-").FullName;
		try {
			var padding = new string ('x', paddingLength);
			var script = $"echo discarded-prefix{padding}\necho stdout-tail\necho stderr-tail >&2\nexit 7\n";
			var task = CreateCliTask (root, script);
			Assert.IsFalse (task.Execute ());
			var engine = (MockBuildEngine) task.BuildEngine;
			var error = engine.Errors.Single (e => e.Code == "XA5308");
			StringAssert.Contains ("failed with exit code 7", error.Message);
			StringAssert.Contains ("stdout-tail", error.Message);
			if (paddingLength == 0)
				StringAssert.Contains ("stderr-tail", error.Message);
			else
				StringAssert.DoesNotContain ("discarded-prefix", error.Message);
		} finally {
			Directory.Delete (root, recursive: true);
		}
	}

	[Test]
	[Platform (Exclude = "Win")]
	public void CancelRunningAndroidCli ()
	{
		var root = Directory.CreateTempSubdirectory ("android-cli-").FullName;
		var task = CreateCliTask (root, "echo $$ > child.pid\nexec sleep 60\n");
		var execution = Task.Run (() => task.Execute ());
		try {
			var pidFile = Path.Combine (root, "child.pid");
			Assert.IsTrue (SpinWait.SpinUntil (() => File.Exists (pidFile) && new FileInfo (pidFile).Length > 0, TimeSpan.FromSeconds (10)),
				"The CLI did not signal readiness.");
			using var child = Process.GetProcessById (int.Parse (File.ReadAllText (pidFile).Trim (), CultureInfo.InvariantCulture));
			task.Cancel ();
			Assert.IsTrue (execution.Wait (TimeSpan.FromSeconds (10)), "Cancellation did not complete.");
			Assert.IsFalse (execution.Result);
			Assert.IsTrue (child.HasExited, "The CLI must exit before Execute () returns.");
			var engine = (MockBuildEngine) task.BuildEngine;
			Assert.IsTrue (engine.Errors.Any (e => e.Message.Contains ("timed out or was cancelled", StringComparison.Ordinal)));
		} finally {
			task.Cancel ();
			execution.GetAwaiter ().GetResult ();
			Directory.Delete (root, recursive: true);
		}
	}

	static InstallAndroidDependencies CreateCliTask (string root, string script)
	{
		var cli = Path.Combine (root, OperatingSystem.IsWindows () ? "android.cmd" : "android");
		File.WriteAllText (cli, OperatingSystem.IsWindows () ? "@echo off\r\n" + script.Replace ("\n", "\r\n") : "#!/bin/sh\n" + script);
		if (!OperatingSystem.IsWindows ())
			File.SetUnixFileMode (cli, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
		return new InstallAndroidDependencies {
			BuildEngine = new MockBuildEngine (TestContext.Out, [], [], []),
			TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment (root, new Dictionary<string, string> ()),
			AndroidSdkPath = "sdk",
			AcceptAndroidSDKLicenses = true,
			InstallJavaDependencies = false,
			AndroidCliPath = cli,
			JavaDependencies = [],
			Dependencies = [new TaskItem ("platform-tools")],
		};
	}

	[TestCase (false)]
	[TestCase (true)]
	[Platform (Exclude = "Win")]
	[UnsupportedOSPlatform ("windows")]
	public async Task InstallTarJdkPreservesExecutableAndSymlink (bool trailingSeparator)
	{
		var root = Directory.CreateTempSubdirectory ("jdk-extract-").FullName;
		try {
			var archivePath = Path.Combine (root, "jdk.tar.gz");
			var home = OperatingSystem.IsMacOS () ? "jdk/Contents/Home" : "jdk";
			var mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
				UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
			using (var file = File.Create (archivePath))
			using (var gzip = new GZipStream (file, CompressionMode.Compress))
			using (var writer = new TarWriter (gzip))
			using (var contents = new MemoryStream (Encoding.UTF8.GetBytes ("#!/bin/sh\nexit 0\n"))) {
				writer.WriteEntry (new PaxTarEntry (TarEntryType.RegularFile, $"{home}/bin/java") {
					DataStream = contents,
					Mode = mode,
				});
				writer.WriteEntry (new PaxTarEntry (TarEntryType.SymbolicLink, $"{home}/bin/java-link") { LinkName = "java" });
			}
			var destination = Path.Combine (root, "installed");
			Directory.CreateDirectory (destination);
			File.WriteAllText (Path.Combine (destination, "old.txt"), "old installation");
			var installer = new JavaDependencyInstaller {
				JdkPath = new AbsolutePath (destination + (trailingSeparator ? Path.DirectorySeparatorChar.ToString () : "")),
			};
			await installer.InstallJdk (new Archive { DownloadedFilePath = archivePath }, CancellationToken.None);
			Assert.IsFalse (File.Exists (Path.Combine (destination, "old.txt")));
			var java = Path.Combine (destination, "bin", "java");
			Assert.AreEqual (mode, File.GetUnixFileMode (java));
			var link = Path.Combine (destination, "bin", "java-link");
			Assert.AreEqual ("java", new FileInfo (link).LinkTarget);
			Assert.AreEqual (File.ReadAllText (java), File.ReadAllText (link));
			CollectionAssert.AreEqual (new [] { destination }, Directory.GetDirectories (root), "Extraction staging must be removed.");
		} finally {
			Directory.Delete (root, recursive: true);
		}
	}

	[Test]
	public async Task UnknownJdkVersionIsNotValid ()
	{
		// aka.ms sends an unknown link to an HTML page on bing.com with status 200.
		using (var client = new HttpClient (new HtmlHandler ())) {
			var archive = await new JavaDependencyInstaller ().GetFirstValidJdkArchiveWithVersion (client, "21.0.99", CancellationToken.None);
			Assert.IsNull (archive);
		}
	}

	sealed class HtmlHandler : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync (HttpRequestMessage request, CancellationToken cancellationToken) =>
			Task.FromResult (new HttpResponseMessage (HttpStatusCode.OK) {
				Content = new StringContent ("<html></html>", Encoding.UTF8, "text/html"),
			});
	}
}
