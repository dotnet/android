<#
.SYNOPSIS
Runs one CI job's share (slice) of an NUnit test assembly that uses Microsoft.Testing.Platform (MTP).

.DESCRIPTION
1. Lists the tests with `--list-tests json`. The optional -TestFilter is given to `--filter`.
2. Sorts the test names and selects every test where (index % TotalSlices) == (SliceNumber - 1).
3. Runs only the selected tests, and fails if fewer tests run than were selected.
4. With -RetryFailedTests, removes the failed results from the TRX file and runs the failed
   tests one more time. The retry TRX file is written to "<ResultsDirectory>/retry".

The selected tests are given to NUnit as a `<Where>` filter (`test =~ "^name$"`) in a
runsettings file. `--filter-uid` cannot select all NUnit tests: the UIDs of parameterized tests
contain quote characters, which MTP removes from response files, and a long list of UIDs can be
longer than the Windows command-line limit.

The script exits with the MTP exit code of the last test run (0 when all tests passed).
#>
param(
	[Parameter(Mandatory)] [string] $DotNet,
	[Parameter(Mandatory)] [string] $TestAssembly,
	[Parameter(Mandatory)] [string] $ResultsDirectory,
	[int] $SliceNumber = 1,
	[int] $TotalSlices = 1,
	# MTP `--filter` syntax, for example: TestCategory!=SystemApplication&TestCategory!=Localization
	[string] $TestFilter = '',
	[int] $Workers = 0,
	[switch] $RetryFailedTests
)

$ErrorActionPreference = 'Stop'

if ($TotalSlices -lt 1 -or $SliceNumber -lt 1 -or $SliceNumber -gt $TotalSlices) {
	throw "Invalid slice $SliceNumber of $TotalSlices."
}

New-Item -ItemType Directory -Force -Path $ResultsDirectory | Out-Null
$name = [IO.Path]::GetFileNameWithoutExtension($TestAssembly)

function Get-Tests {
	$listArgs = @('--list-tests', 'json', '--no-banner')
	if ($TestFilter) {
		$listArgs += @('--filter', $TestFilter)
	}
	$output = & $DotNet $TestAssembly @listArgs
	if ($LASTEXITCODE -ne 0) {
		$output | Write-Host
		throw "Listing the tests in $TestAssembly failed with exit code $LASTEXITCODE."
	}
	$json = $output -join "`n"
	return (ConvertFrom-Json $json.Substring($json.IndexOf('{'))).tests
}

# Runs the tests with the names in $Tests. Returns the MTP exit code.
function Invoke-Tests([string[]] $Tests, [string] $Directory) {
	New-Item -ItemType Directory -Force -Path $Directory | Out-Null
	# Use an anchored regex for each test name. NUnit removes spaces outside quotes in
	# `test == "..."` values, for example `M((False, CoreCLR))`, so spaces are written as \x20.
	# NUnit filter strings use backslash to escape '\' and '"'.
	$where = ($Tests | ForEach-Object {
		$regex = '^' + [Text.RegularExpressions.Regex]::Escape($_).Replace('\ ', '\x20') + '$'
		'test =~ "' + $regex.Replace('\', '\\').Replace('"', '\"') + '"'
	}) -join ' || '
	$nunit = "<Where>$([Security.SecurityElement]::Escape($where))</Where>"
	if ($Workers -gt 0) {
		$nunit += "<NumberOfTestWorkers>$Workers</NumberOfTestWorkers>"
	}
	$runSettings = Join-Path $Directory "$name.runsettings"
	Set-Content -Path $runSettings -Value "<RunSettings><NUnit>$nunit</NUnit></RunSettings>"

	& $DotNet $TestAssembly `
		--settings $runSettings `
		--minimum-expected-tests $Tests.Count `
		--report-trx `
		--results-directory $Directory `
		--no-banner `
		--progress off | Write-Host
	return $LASTEXITCODE
}

# Some test names are listed more than once, and some differ only in case.
# MTP reports one result for each unique name, so use an ordinal set of unique names.
$uids = [Collections.Generic.SortedSet[string]]::new([string[]] (Get-Tests).uid, [StringComparer]::Ordinal)
$selected = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$i = 0
foreach ($uid in $uids) {
	if ($i++ % $TotalSlices -eq $SliceNumber - 1) {
		$selected.Add($uid) | Out-Null
	}
}
Write-Host "Found $($uids.Count) tests in $TestAssembly; slice $SliceNumber of $TotalSlices has $($selected.Count) tests:"
$selected | ForEach-Object { Write-Host "  $_" }
if ($selected.Count -eq 0) {
	throw "Slice $SliceNumber of $TotalSlices has no tests. Check the test assembly and the filter '$TestFilter'."
}

$exitCode = Invoke-Tests -Tests $selected -Directory $ResultsDirectory
Write-Host "Test run exit code: $exitCode"

# MTP exit code 2 means that at least one test failed
if ($RetryFailedTests -and $exitCode -eq 2) {
	$failed = [Collections.Generic.SortedSet[string]]::new([StringComparer]::Ordinal)
	$notRetried = 0
	foreach ($trxFile in Get-ChildItem -Path $ResultsDirectory -Filter *.trx) {
		[xml] $trx = Get-Content -Raw $trxFile.FullName
		$ns = New-Object Xml.XmlNamespaceManager $trx.NameTable
		$ns.AddNamespace('t', $trx.DocumentElement.NamespaceURI)
		foreach ($result in $trx.SelectNodes('//t:UnitTestResult[@outcome="Failed"]', $ns)) {
			$method = $trx.SelectSingleNode("//t:UnitTest[@id='$($result.testId)']/t:TestMethod", $ns)
			# The test name in the TRX file does not include the namespace and class
			$uid = "$($method.className).$($result.testName)"
			if ($selected.Contains($uid)) {
				$failed.Add($uid) | Out-Null
				$result.ParentNode.RemoveChild($result) | Out-Null
			} else {
				$notRetried++
				Write-Host "##vso[task.logissue type=warning]Cannot run '$uid' again; it is not in the list of tests."
			}
		}
		$trx.Save($trxFile.FullName)
	}
	if ($failed.Count -gt 0) {
		Write-Host "Running $($failed.Count) failed tests again:"
		$failed | ForEach-Object { Write-Host "  $_" }
		Write-Host "##vso[task.setvariable variable=TestsRetried]true"
		$retryExitCode = Invoke-Tests -Tests $failed -Directory (Join-Path $ResultsDirectory 'retry')
		Write-Host "Retry exit code: $retryExitCode"
		# Keep the failure if some failed tests could not be run again
		if ($notRetried -eq 0) {
			$exitCode = $retryExitCode
		}
	}
}

exit $exitCode
