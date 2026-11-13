// Runs one CI job's share of the tests in an NUnit test assembly that uses Microsoft.Testing.Platform (MTP):
//
//   dotnet run RunSlicedTests.cs -- --test-assembly <dll> --results-directory <dir>
//     [--slice 1] [--total-slices 1] [--filter <MTP filter>] [--workers -1] [--retry false]
//
// The job gets every Nth test from `--list-tests json`. We do not use `--filter-uid`, because
// MTP removes '"' from response files, and NUnit test names often contain '"'.
// Instead, the tests are selected with an NUnit <Where> filter in a .runsettings file.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

var options = new Dictionary<string, string> {
	["--slice"] = "1",
	["--total-slices"] = "1",
	["--filter"] = "",
	["--workers"] = "-1",
	["--retry"] = "false",
};
for (int i = 0; i + 1 < args.Length; i += 2)
	options [args [i]] = args [i + 1];

string testAssembly = options ["--test-assembly"];
string resultsDirectory = options ["--results-directory"];
int slice = int.Parse (options ["--slice"]);
int totalSlices = int.Parse (options ["--total-slices"]);
// Use the same dotnet that runs this file
string dotnet = Path.Combine (RuntimeEnvironment.GetRuntimeDirectory (), "..", "..", "..", "dotnet");

var list = Process.Start (new ProcessStartInfo (dotnet, [testAssembly, "--list-tests", "json", "--filter", options ["--filter"], "--no-banner"]) {
	RedirectStandardOutput = true,
}) ?? throw new InvalidOperationException ($"Could not start {dotnet}");
string json = list.StandardOutput.ReadToEnd ();
list.WaitForExit ();
if (list.ExitCode != 0) {
	Console.WriteLine (json);
	Console.Error.WriteLine ($"Listing the tests failed with exit code {list.ExitCode}.");
	return list.ExitCode;
}

// MTP reports one result for each unique name. Some names differ only in case.
var tests = new SortedSet<string> (StringComparer.Ordinal);
foreach (var test in JsonDocument.Parse (json [json.IndexOf ('{')..]).RootElement.GetProperty ("tests").EnumerateArray ())
	tests.Add (test.GetProperty ("uid").GetString () ?? "");

var selected = new SortedSet<string> (tests.Where ((_, index) => index % totalSlices == slice - 1), StringComparer.Ordinal);
Console.WriteLine ($"Slice {slice} of {totalSlices} has {selected.Count} of {tests.Count} tests:");
foreach (var test in selected)
	Console.WriteLine ($"  {test}");
if (selected.Count == 0) {
	Console.Error.WriteLine ("No tests to run.");
	return 1;
}

int exitCode = RunTests (selected, resultsDirectory);

// Exit code 2: some tests failed. Remove the failed results and run those tests again.
if (bool.Parse (options ["--retry"]) && exitCode == 2) {
	XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
	var failed = new SortedSet<string> (StringComparer.Ordinal);
	foreach (var trxPath in Directory.GetFiles (resultsDirectory, "*.trx")) {
		var trx = XDocument.Load (trxPath);
		var classNames = trx.Descendants (ns + "UnitTest").ToDictionary (
			t => (string?) t.Attribute ("id"),
			t => (string?) t.Element (ns + "TestMethod")?.Attribute ("className"));
		foreach (var result in trx.Descendants (ns + "UnitTestResult").Where (r => (string?) r.Attribute ("outcome") == "Failed").ToList ()) {
			failed.Add ($"{classNames [(string?) result.Attribute ("testId")]}.{(string?) result.Attribute ("testName")}");
			result.Remove ();
		}
		trx.Save (trxPath);
	}
	if (failed.Count > 0) {
		Console.WriteLine ("##vso[task.setvariable variable=TestsRetried]true");
		exitCode = RunTests (failed, Path.Combine (resultsDirectory, "retry"));
	}
}

return exitCode;

// Returns the MTP exit code. Fails if fewer tests run than expected.
int RunTests (IReadOnlyCollection<string> tests, string directory)
{
	Directory.CreateDirectory (directory);
	// NUnit removes spaces outside quotes in `test == "..."`, so use `test =~ "^...$"` with \x20.
	var where = string.Join (" || ", tests.Select (t => {
		var regex = "^" + Regex.Escape (t).Replace ("\\ ", "\\x20") + "$";
		return "test =~ \"" + regex.Replace ("\\", "\\\\").Replace ("\"", "\\\"") + "\"";
	}));
	var runSettings = Path.Combine (directory, "tests.runsettings");
	File.WriteAllText (runSettings, $"""
		<RunSettings>
		  <NUnit>
		    <Where>{SecurityElement.Escape (where)}</Where>
		    <NumberOfTestWorkers>{options ["--workers"]}</NumberOfTestWorkers>
		  </NUnit>
		</RunSettings>
		""");

	using var process = Process.Start (dotnet, [
		testAssembly,
		"--settings", runSettings,
		"--minimum-expected-tests", tests.Count.ToString (),
		"--report-trx",
		"--results-directory", directory,
		"--no-banner",
		"--progress", "off",
	]);
	process.WaitForExit ();
	Console.WriteLine ($"Exit code: {process.ExitCode}");
	return process.ExitCode;
}
