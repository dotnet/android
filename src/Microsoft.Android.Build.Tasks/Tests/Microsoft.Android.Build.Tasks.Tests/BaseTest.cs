using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

public class BaseTest
{
	static readonly char [] InvalidChars = ['{', '}', '(', ')', '$', ':', ';', '"', '\'', ',', '=', '|'];

	public string Root => Path.Combine (TestContext.CurrentContext.WorkDirectory, "Microsoft.Android.Build.Tasks.Tests");

	public string TestName {
		get {
			var result = TestContext.CurrentContext.Test.Name;
			foreach (var c in InvalidChars.Concat (Path.GetInvalidPathChars ()).Concat (Path.GetInvalidFileNameChars ())) {
				result = result.Replace (c, '_');
			}
			return result.Replace ("_", "");
		}
	}

	protected static (int code, string stdOutput, string stdError) RunProcessWithExitCode (string exe, string args, int timeoutInSeconds = 30)
	{
		TestContext.Out.WriteLine ($"{nameof (RunProcessWithExitCode)}: {exe} {args}");
		var info = new ProcessStartInfo (exe, args) {
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
			WindowStyle = ProcessWindowStyle.Hidden,
		};
		using var process = Process.Start (info);
		if (process == null) {
			return (-1, "", $"Failed to start '{exe}'.");
		}
		var output = process.StandardOutput.ReadToEndAsync ();
		var error = process.StandardError.ReadToEndAsync ();
		if (!process.WaitForExit ((int) TimeSpan.FromSeconds (timeoutInSeconds).TotalMilliseconds)) {
			process.Kill (entireProcessTree: true);
			return (-1, "", $"Process timed out after {timeoutInSeconds} seconds.");
		}
		return (process.ExitCode, output.GetAwaiter ().GetResult ().Trim (), error.GetAwaiter ().GetResult ().Trim ());
	}

	[TearDown]
	public void Cleanup ()
	{
		var testDirectory = Path.Combine (Root, "temp", TestName);
		if (Directory.Exists (testDirectory)) {
			Directory.Delete (testDirectory, recursive: true);
		}
	}
}
