// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

namespace Xamarin.Android.Tools.Tests
{
	[TestFixture]
	public class StartProcessTests
	{
		[TestCase (true, false)]
		[TestCase (false, true)]
		[TestCase (true, true)]
		public async Task RedirectedStreamsWithoutWritersAreDrained (bool redirectOutput, bool redirectError)
		{
			var psi = CreateShellStartInfo (
				"for /L %i in (1,1,8192) do @(echo output data & echo error data 1>&2)",
				"i=0; while [ \"$i\" -lt 8192 ]; do echo output data; echo error data >&2; i=$((i+1)); done");
			psi.RedirectStandardOutput = redirectOutput;
			psi.RedirectStandardError = redirectError;
			using var output = new StringWriter ();
			using var error = new StringWriter ();
			using var cancellation = new CancellationTokenSource (TimeSpan.FromSeconds (5));

			var task = ProcessUtils.StartProcess (psi, redirectOutput ? null : output, redirectError ? null : error, cancellation.Token);
			await AssertCompletes (task);
			Assert.AreEqual (0, await task);
			if (!redirectOutput)
				StringAssert.Contains ("output data", output.ToString ());
			if (!redirectError)
				StringAssert.Contains ("error data", error.ToString ());
		}

		[TestCase (false)]
		[TestCase (true)]
		public async Task PreservesCallerOwnedWriters (bool sharedWriter)
		{
			var psi = CreateShellStartInfo ("echo tool output & echo tool error 1>&2", "echo tool output; echo tool error >&2");
			using var output = new StringWriter ();
			using var error = new StringWriter ();
			var errorDestination = sharedWriter ? output : error;
			using var cancellation = new CancellationTokenSource (TimeSpan.FromSeconds (5));

			var task = ProcessUtils.StartProcess (psi, output, errorDestination, cancellation.Token);
			await AssertCompletes (task);
			Assert.AreEqual (0, await task);
			StringAssert.Contains ("tool output", output.ToString ());
			StringAssert.Contains ("tool error", errorDestination.ToString ());
			output.Write ("still open");
			errorDestination.Write ("still open");
		}

		[Test]
		public async Task RedirectedStreamsWithoutWritersPreserveCancellation ()
		{
			var psi = new ProcessStartInfo (ShellExecutable) {
				RedirectStandardInput = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
			};
			using var cancellation = new CancellationTokenSource ();
			var task = ProcessUtils.StartProcess (psi, null, null, cancellation.Token,
				process => cancellation.Cancel ());

			await AssertCompletes (task);
			Assert.ThrowsAsync<OperationCanceledException> (async () => await task);
			Assert.IsTrue (task.IsCanceled);
		}

		[Test]
		public async Task RedirectedStreamsWithoutWritersPreserveExitCode ()
		{
			var psi = CreateShellStartInfo ("echo tool error 1>&2 & exit /b 7", "echo tool error >&2; exit 7");
			psi.RedirectStandardOutput = true;
			psi.RedirectStandardError = true;
			using var cancellation = new CancellationTokenSource (TimeSpan.FromSeconds (5));

			var task = ProcessUtils.StartProcess (psi, null, null, cancellation.Token);
			await AssertCompletes (task);
			Assert.AreEqual (7, await task);
		}

		static string ShellExecutable => OS.IsWindows ? Environment.GetEnvironmentVariable ("ComSpec") ?? "cmd.exe" : "/bin/sh";

		static ProcessStartInfo CreateShellStartInfo (string windowsCommand, string unixCommand)
		{
			return OS.IsWindows
				? ProcessUtils.CreateProcessStartInfo (ShellExecutable, "/d", "/c", windowsCommand)
				: ProcessUtils.CreateProcessStartInfo (ShellExecutable, "-c", unixCommand);
		}

		static async Task AssertCompletes (Task task)
		{
			Assert.AreSame (task, await Task.WhenAny (task, Task.Delay (TimeSpan.FromSeconds (10))), "Process task did not complete");
		}
	}
}
