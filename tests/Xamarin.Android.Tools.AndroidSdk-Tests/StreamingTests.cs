#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Xamarin.Android.Tools.Tests;

[TestClass]
[DoNotParallelize]
public class StreamingTests
{
	string directory = "";
	TextWriter previousError = Console.Error;
	StringWriter error = new ();

	[TestInitialize]
	public void SetUp ()
	{
		directory = Path.Combine (Path.GetTempPath (), "android-streaming-" + Guid.NewGuid ().ToString ("N"));
		Directory.CreateDirectory (directory);
		previousError = Console.Error;
		error = new StringWriter ();
		Console.SetError (TextWriter.Synchronized (error));
	}

	[TestCleanup]
	public void TearDown ()
	{
		Console.SetError (previousError);
		error.Dispose ();
		Directory.Delete (directory, recursive: true);
	}

	static InstrumentationStatus Status (string kind, string test, string? outcome = null)
	{
		var values = new Dictionary<string, string> { ["event"] = kind, ["test"] = test };
		if (outcome != null)
			values ["outcome"] = outcome;
		return new () { Values = values };
	}

	[TestMethod]
	public void PartialResultsPreserveObservedOutcomesAndAllUnfinishedTests ()
	{
		var state = new StreamingState ();
		foreach (var outcome in new [] { "passed", "failed", "skipped", "unexpected" }) {
			state.Observe (Status ("start", "Tests." + outcome));
			state.Observe (Status ("finish", "Tests." + outcome, outcome));
		}
		state.Observe (Status ("start", "Tests.ActiveOne"));
		state.Observe (Status ("start", "Tests.ActiveTwo"));
		state.Observe (Status ("finish", "Tests.ActiveOne")); // Missing outcome is not a completion.
		state.Observe (Status ("other", "Tests.Ignored", "passed"));
		state.WritePartialResults ();
		Assert.Contains ("passed=1, failed=1, skipped=1, unknown=1", error.ToString ());
		foreach (var outcome in new [] { "passed", "failed", "skipped", "unexpected" })
			Assert.Contains ($"[{outcome}] Tests.{outcome}", error.ToString ());
		Assert.Contains ("[unfinished] Tests.ActiveOne", error.ToString ());
		Assert.Contains ("not necessarily the cause", error.ToString ());
		Assert.IsEmpty (state.ReportedFinal, "Observation must not depend on MTP publication.");
	}

	[TestMethod]
	public void ParserObservesCompleteBlocksImmediatelyButNeverInventsACompletion ()
	{
		var state = new StreamingState ();
		var parser = new StatusStreamParser (state.Observe, new StringBuilder (), verbose: false);
		parser.OnLine ("INSTRUMENTATION_STATUS: event=start");
		parser.OnLine ("INSTRUMENTATION_STATUS: test=Tests.One");
		parser.OnLine ("INSTRUMENTATION_STATUS_CODE: 1");
		Assert.IsTrue (state.HasUnfinishedTests, "Observed before the instrumentation process exits.");
		parser.OnLine ("INSTRUMENTATION_STATUS: event=finish");
		parser.OnLine ("INSTRUMENTATION_STATUS: test=Tests.One");
		parser.OnLine ("INSTRUMENTATION_STATUS: outcome=passed");
		parser.Complete ();
		state.WritePartialResults ();
		Assert.Contains ("passed=0", error.ToString ());
	}

	const string Progress = """
		INSTRUMENTATION_STATUS: event=start
		INSTRUMENTATION_STATUS: test=Tests.One
		INSTRUMENTATION_STATUS: class=Tests
		INSTRUMENTATION_STATUS_CODE: 1
		INSTRUMENTATION_STATUS: event=finish
		INSTRUMENTATION_STATUS: test=Tests.One
		INSTRUMENTATION_STATUS: class=Tests
		INSTRUMENTATION_STATUS: outcome=passed
		INSTRUMENTATION_STATUS_CODE: 0
		INSTRUMENTATION_STATUS: event=start
		INSTRUMENTATION_STATUS: test=Tests.Two
		INSTRUMENTATION_STATUS: class=Tests
		INSTRUMENTATION_STATUS_CODE: 1
		""";

	[TestMethod]
	[DataRow (false, false)]
	[DataRow (false, true)]
	[DataRow (true, false)]
	[DataRow (true, true)]
	public async Task AdapterPreservesNormalTrxAndPrintsPartialResultsWithoutTrx (bool crash, bool reportFailure)
	{
		string transcript = Progress + (crash ? "\nINSTRUMENTATION_RESULT: shortMsg=Process crashed.\nINSTRUMENTATION_CODE: 0" : """

			INSTRUMENTATION_STATUS: event=finish
			INSTRUMENTATION_STATUS: test=Tests.Two
			INSTRUMENTATION_STATUS: class=Tests
			INSTRUMENTATION_STATUS: outcome=passed
			INSTRUMENTATION_STATUS_CODE: 0
			INSTRUMENTATION_RESULT: passed=2
			INSTRUMENTATION_RESULT: failed=0
			INSTRUMENTATION_RESULT: skipped=0
			INSTRUMENTATION_RESULT: resultsPath=/device/results.trx
			INSTRUMENTATION_CODE: -1
			""");
		if (reportFailure)
			Directory.CreateDirectory (Path.Combine (directory, "results.trx"));
		var (exitCode, output) = await RunAndroidTestsAsync (transcript);
		if (reportFailure) {
			Assert.AreNotEqual (0, exitCode, output);
			Assert.IsFalse (File.Exists (Path.Combine (directory, "results.trx")));
		} else {
			Assert.AreEqual (!crash, exitCode == 0, output);
			var trx = XDocument.Load (Path.Combine (directory, "results.trx"));
			XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
			var results = trx.Descendants (ns + "UnitTestResult").ToArray ();
			Assert.HasCount (2, results);
			var preceding = results.Single (e => e.Attribute ("testName")?.Value == "Tests.One");
			var last = results.Single (e => e.Attribute ("testName")?.Value == "Tests.Two");
			Assert.AreEqual ("Passed", preceding.Attribute ("outcome")?.Value);
			Assert.AreEqual (crash ? "Failed" : "Passed", last.Attribute ("outcome")?.Value);
			if (crash) {
				var message = last.Element (ns + "Output")?.Element (ns + "ErrorInfo")?.Element (ns + "Message")?.Value;
				Assert.AreEqual ("This was the last observed active test, not necessarily the cause of the incomplete run.\nThe test process crashed: Process crashed.", message);
			}
		}
		if (crash) {
			Assert.Contains ("Incomplete test run", output);
			Assert.Contains ("[passed] Tests.One", output);
		} else {
			Assert.DoesNotContain ("Incomplete test run", output);
		}
	}

	[TestMethod]
	[DataRow ("", 0)]
	[DataRow ("error: device offline", 1)]
	[DataRow (Progress, 0)]
	public async Task MissingBundleOrTransportFailureIsNotLabeledACrash (string transcript, int adbExitCode)
	{
		var (exitCode, output) = await RunAndroidTestsAsync (transcript, adbExitCode: adbExitCode);
		Assert.AreNotEqual (0, exitCode, output);
		if (adbExitCode == 0 && transcript.Length == 0)
			Assert.Contains ("Instrumentation did not report a resultsPath in the bundle.", output);
		Assert.Contains ("Incomplete test run", output);
		Assert.Contains (transcript == Progress ? "passed=1" : "passed=0", output);
		Assert.DoesNotContain ("process crashed", output);
	}

	[TestMethod]
	public async Task ProcessStartupExceptionStillPrintsPartialSummary ()
	{
		var (exitCode, output) = await RunAndroidTestsAsync ("", executableAdb: false);
		Assert.AreNotEqual (0, exitCode, output);
		Assert.Contains ("An error occurred trying to start process", output);
		Assert.Contains ("Incomplete test run", output);
	}

	[TestMethod]
	[DataRow (false)]
	[DataRow (true)]
	public async Task UnfinishedTestIsSavedAsFailedEvenWhenAdbExitsSuccessfully (bool finalBundle)
	{
		string transcript = """
			INSTRUMENTATION_STATUS: event=start
			INSTRUMENTATION_STATUS: test=Tests.One
			INSTRUMENTATION_STATUS: class=Tests
			INSTRUMENTATION_STATUS_CODE: 1
			""" + (finalBundle ? "\nINSTRUMENTATION_RESULT: resultsPath=/device/results.trx\nINSTRUMENTATION_CODE: -1" : "");
		var (exitCode, output) = await RunAndroidTestsAsync (transcript);
		Assert.AreNotEqual (0, exitCode, output);
		XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
		var result = XDocument.Load (Path.Combine (directory, "results.trx")).Descendants (ns + "UnitTestResult").Single ();
		Assert.AreEqual ("Tests.One", result.Attribute ("testName")?.Value);
		Assert.AreEqual ("Failed", result.Attribute ("outcome")?.Value);
		Assert.AreEqual ("This was the last observed active test, not necessarily the cause of the incomplete run.\nThe test run ended before all tests completed.", result.Element (ns + "Output")?.Element (ns + "ErrorInfo")?.Element (ns + "Message")?.Value);
		Assert.Contains ("passed=0", output);
		Assert.DoesNotContain ("process crashed", output);
	}

	[TestMethod]
	public async Task CancellationAfterProgressPreservesPartialResults ()
	{
		var stopwatch = Stopwatch.StartNew ();
		var (exitCode, output) = await RunAndroidTestsAsync (Progress, interruptAfterTranscript: true);
		Assert.AreEqual (130, exitCode, output);
		Assert.IsLessThan (TimeSpan.FromSeconds (25), stopwatch.Elapsed, "Ctrl+C must stop the still-running adb invocation.");
		Assert.Contains ("Incomplete test run", output);
		Assert.Contains ("[passed] Tests.One", output);
	}

	static readonly string MicrosoftAndroidRunPath = typeof (StreamingTests).Assembly
		.GetCustomAttributes<AssemblyMetadataAttribute> ()
		.Single (a => a.Key == "MicrosoftAndroidRunPath").Value ?? throw new InvalidOperationException ("Missing MicrosoftAndroidRunPath metadata.");

	// Runs the built Microsoft.Android.Run under 'dotnet test', the same way Android test projects run, with a fake adb
	// that replays an 'am instrument -w -r' transcript.
	async Task<(int ExitCode, string Output)> RunAndroidTestsAsync (string transcript, int adbExitCode = 0, bool executableAdb = true, bool interruptAfterTranscript = false)
	{
		if (OperatingSystem.IsWindows ())
			Assert.Inconclusive ("The fake adb is a Unix shell executable.");
		string adb = Path.Combine (directory, "fake-adb");
		// After Ctrl+C, Microsoft.Android.Run runs other adb commands to stop the app; only 'am instrument' replays the transcript.
		var ending = interruptAfterTranscript ? "kill -INT $PPID\nexec sleep 30" : $"exit {adbExitCode}";
		File.WriteAllText (adb, $"#!/bin/sh\ncase \"$*\" in *instrument*) ;; *) exit 0 ;; esac\ncat <<'TRANSCRIPT'\n{transcript}\nTRANSCRIPT\n{ending}\n");
		if (executableAdb && !OperatingSystem.IsWindows ())
			File.SetUnixFileMode (adb, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
		File.WriteAllText (Path.Combine (directory, "global.json"), """{ "test": { "runner": "Microsoft.Testing.Platform" } }""");

		var psi = new ProcessStartInfo (Path.GetFullPath (Path.Combine (RuntimeEnvironment.GetRuntimeDirectory (), "..", "..", "..", "dotnet"))) {
			WorkingDirectory = directory,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
		};
		foreach (var arg in new [] {
				"test", "--no-ansi", "--no-progress", "--test-modules", MicrosoftAndroidRunPath, "--root-directory", "/", "--",
				"--adb", adb, "--package", "tests", "--instrument", "runner",
				"--report-trx", "--report-trx-filename", "results.trx", "--results-directory", directory,
		})
			psi.ArgumentList.Add (arg);

		using var process = Process.Start (psi) ?? throw new InvalidOperationException ($"Failed to start '{psi.FileName}'.");
		var stdout = process.StandardOutput.ReadToEndAsync ();
		var stderr = process.StandardError.ReadToEndAsync ();
		using var timeout = new CancellationTokenSource (TimeSpan.FromMinutes (2));
		try {
			await process.WaitForExitAsync (timeout.Token);
		} catch (OperationCanceledException) {
			process.Kill (entireProcessTree: true);
			throw;
		}
		return (process.ExitCode, await stdout + await stderr);
	}
}
