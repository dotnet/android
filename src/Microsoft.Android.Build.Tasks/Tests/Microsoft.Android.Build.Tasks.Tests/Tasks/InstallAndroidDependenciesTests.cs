using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
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
			var messages = new List<BuildMessageEventArgs> ();
			var task = new InstallAndroidDependencies {
				BuildEngine = new MockBuildEngine (TestContext.Out, errors, [], messages),
				// Relative paths resolve against the project directory, not the process working directory.
				TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment (root, new Dictionary<string, string> ()),
				AndroidSdkPath = "sdk",
				AcceptAndroidSDKLicenses = true,
				InstallJavaDependencies = false,
				AndroidCliPath = Path.GetFileName (cli),
				JavaDependencies = [],
				Dependencies = [
					new TaskItem ("build-tools/37.0.0", new Dictionary<string, string> { { "Version", "37.0.0" } }),
					new TaskItem ("platform-tools", new Dictionary<string, string> { { "Version", "37.0.1" } }),
					new TaskItem ("ndk-bundle", new Dictionary<string, string> { { "Version", "27.2.12479018" } }),
					new TaskItem ("platforms/android-37.0"),
				],
			};
			Assert.IsTrue (task.Execute (), string.Join (Environment.NewLine, errors.Select (e => e.Message)));

			var args = File.ReadAllText (Path.Combine (root, "args.txt")).Trim ();
			StringAssert.Contains ($"--no-metrics --sdk={Path.Combine (root, "sdk")} sdk install", args);
			StringAssert.EndsWith ("build-tools/37.0.0 platform-tools@37.0.1 ndk/27.2.12479018 platforms/android-37.0", args);
			var installing = messages.Single (m => m.Message.StartsWith ("Installing Android SDK packages", StringComparison.Ordinal));
			Assert.AreEqual (Environment.CurrentManagedThreadId, installing.ThreadId, "Messages must be logged on the thread that called Execute ().");
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
