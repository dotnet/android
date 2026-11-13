// Runs one CI job's share (slice) of an NUnit test assembly that uses Microsoft.Testing.Platform (MTP).
//
// Usage:
//   dotnet run build-tools/automation/scripts/RunSlicedTests.cs -- --dotnet <path> --test-assembly <dll>
//       --results-directory <dir> [--slice <N>] [--total-slices <M>] [--filter <MTP filter>]
//       [--workers <count>] [--retry <true|false>]
//
// 1. Lists the tests with `--list-tests json`. The optional --filter is given to MTP `--filter`.
// 2. Sorts the unique test names and selects every test where (index % M) == (N - 1).
// 3. Runs only the selected tests, and fails if fewer tests run than were selected.
// 4. With --retry true, removes the failed results from the TRX file and runs the failed tests
//    one more time. The retry TRX file is written to "<results-directory>/retry".
//
// The selected tests are given to NUnit as a `<Where>` filter (`test =~ "^name$"`) in a
// runsettings file. `--filter-uid` cannot select all NUnit tests: the UIDs of parameterized
// tests contain quote characters, which MTP removes from response files, and a long list of
// UIDs can be longer than the Windows command-line limit.
//
// Exits with the MTP exit code of the last test run (0 when all tests passed).

using System.Diagnostics;
using System.Security;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

string dotnet = "", testAssembly = "", resultsDirectory = "", filter = "";
int slice = 1, totalSlices = 1, workers = 0;
bool retry = false;

for (int a = 0; a < args.Length; a++) {
	// Accept both `--name value` and `--name=value`; a filter value can be empty.
	string name = args [a];
	string? value = null;
	int equals = name.IndexOf ('=');
	if (name.StartsWith ("--", StringComparison.Ordinal) && equals > 0) {
		value = name.Substring (equals + 1);
		name = name.Substring (0, equals);
	}
	string Value () => value ?? (a + 1 < args.Length ? args [++a] : throw new ArgumentException ($"Missing value for '{name}'."));

	switch (name) {
		case "--dotnet": dotnet = Value (); break;
		case "--test-assembly": testAssembly = Value (); break;
		case "--results-directory": resultsDirectory = Value (); break;
		case "--slice": slice = int.Parse (Value ()); break;
		case "--total-slices": totalSlices = int.Parse (Value ()); break;
		case "--filter": filter = Value (); break;
		case "--workers": workers = int.Parse (Value ()); break;
		case "--retry": retry = bool.Parse (Value ()); break;
		default: return Fail ($"Unknown argument '{args [a]}'.");
	}
}
if (dotnet.Length == 0 || testAssembly.Length == 0 || resultsDirectory.Length == 0)
	return Fail ("--dotnet, --test-assembly, and --results-directory are required.");
if (totalSlices < 1 || slice < 1 || slice > totalSlices)
	return Fail ($"Invalid slice {slice} of {totalSlices}.");

Directory.CreateDirectory (resultsDirectory);

// List the tests
var listArgs = new List<string> { testAssembly, "--list-tests", "json", "--no-banner" };
if (filter.Length > 0) {
	listArgs.Add ("--filter");
	listArgs.Add (filter);
}
var list = new ProcessStartInfo (dotnet, listArgs) { RedirectStandardOutput = true };
string json;
using (var process = Process.Start (list) ?? throw new InvalidOperationException ($"Could not start '{dotnet}'.")) {
	json = process.StandardOutput.ReadToEnd ();
	process.WaitForExit ();
	if (process.ExitCode != 0) {
		Console.WriteLine (json);
		return Fail ($"Listing the tests in {testAssembly} failed with exit code {process.ExitCode}.");
	}
}

// Some test names are listed more than once, and some differ only in case.
// MTP reports one result for each unique name, so use an ordinal set of unique names.
var uids = new SortedSet<string> (StringComparer.Ordinal);
using (var document = JsonDocument.Parse (json.Substring (json.IndexOf ('{')))) {
	foreach (var test in document.RootElement.GetProperty ("tests").EnumerateArray ())
		uids.Add (test.GetProperty ("uid").GetString () ?? "");
}
var selected = new SortedSet<string> (StringComparer.Ordinal);
int index = 0;
foreach (var uid in uids) {
	if (index++ % totalSlices == slice - 1)
		selected.Add (uid);
}
Console.WriteLine ($"Found {uids.Count} tests in {testAssembly}; slice {slice} of {totalSlices} has {selected.Count} tests:");
foreach (var uid in selected)
	Console.WriteLine ($"  {uid}");
if (selected.Count == 0)
	return Fail ($"Slice {slice} of {totalSlices} has no tests. Check the test assembly and the filter '{filter}'.");

int exitCode = RunTests (selected, resultsDirectory);
Console.WriteLine ($"Test run exit code: {exitCode}");

// MTP exit code 2 means that at least one test failed
if (retry && exitCode == 2) {
	XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
	var failed = new SortedSet<string> (StringComparer.Ordinal);
	int notRetried = 0;
	foreach (var trxPath in Directory.GetFiles (resultsDirectory, "*.trx")) {
		var trx = XDocument.Load (trxPath);
		var methods = trx.Descendants (ns + "UnitTest")
			.ToDictionary (t => (string?) t.Attribute ("id") ?? "", t => (string?) t.Element (ns + "TestMethod")?.Attribute ("className") ?? "");
		foreach (var result in trx.Descendants (ns + "UnitTestResult").Where (r => (string?) r.Attribute ("outcome") == "Failed").ToList ()) {
			// The test name in the TRX file does not include the namespace and class
			var uid = $"{methods.GetValueOrDefault ((string?) result.Attribute ("testId") ?? "")}.{(string?) result.Attribute ("testName")}";
			if (selected.Contains (uid)) {
				failed.Add (uid);
				result.Remove ();
			} else {
				notRetried++;
				Console.WriteLine ($"##vso[task.logissue type=warning]Cannot run '{uid}' again; it is not in the list of tests.");
			}
		}
		trx.Save (trxPath);
	}
	if (failed.Count > 0) {
		Console.WriteLine ($"Running {failed.Count} failed tests again:");
		foreach (var uid in failed)
			Console.WriteLine ($"  {uid}");
		Console.WriteLine ("##vso[task.setvariable variable=TestsRetried]true");
		int retryExitCode = RunTests (failed, Path.Combine (resultsDirectory, "retry"));
		Console.WriteLine ($"Retry exit code: {retryExitCode}");
		// Keep the failure if some failed tests could not be run again
		if (notRetried == 0)
			exitCode = retryExitCode;
	}
}

return exitCode;

// Runs the tests with the names in `tests`. Returns the MTP exit code.
int RunTests (IReadOnlyCollection<string> tests, string directory)
{
	Directory.CreateDirectory (directory);
	// Use an anchored regex for each test name. NUnit removes spaces outside quotes in
	// `test == "..."` values, for example `M((False, CoreCLR))`, so spaces are written as \x20.
	// NUnit filter strings use backslash to escape '\' and '"'.
	var where = string.Join (" || ", tests.Select (t => {
		var regex = "^" + Regex.Escape (t).Replace ("\\ ", "\\x20") + "$";
		return "test =~ \"" + regex.Replace ("\\", "\\\\").Replace ("\"", "\\\"") + "\"";
	}));
	var nunit = $"<Where>{SecurityElement.Escape (where)}</Where>";
	if (workers > 0)
		nunit += $"<NumberOfTestWorkers>{workers}</NumberOfTestWorkers>";
	var runSettings = Path.Combine (directory, Path.GetFileNameWithoutExtension (testAssembly) + ".runsettings");
	File.WriteAllText (runSettings, $"<RunSettings><NUnit>{nunit}</NUnit></RunSettings>");

	var run = new ProcessStartInfo (dotnet, [
		testAssembly,
		"--settings", runSettings,
		"--minimum-expected-tests", tests.Count.ToString (),
		"--report-trx",
		"--results-directory", directory,
		"--no-banner",
		"--progress", "off",
	]);
	using var process = Process.Start (run) ?? throw new InvalidOperationException ($"Could not start '{dotnet}'.");
	process.WaitForExit ();
	return process.ExitCode;
}

static int Fail (string message)
{
	Console.WriteLine ($"##vso[task.logissue type=error]{message}");
	return 1;
}
