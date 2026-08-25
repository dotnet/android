Set-StrictMode -Version Latest

function Get-NUnitRunSettingsTestNames
{
	param (
		[Parameter(Mandatory)]
		[string] $Path
	)

	[xml] $runSettings = Get-Content -LiteralPath $Path -Raw
	$where = [string] $runSettings.RunSettings.NUnit.Where
	$prefix = "test == '"
	$names = [System.Collections.Generic.List[string]]::new()
	$offset = 0

	while ($offset -lt $where.Length) {
		$start = $where.IndexOf($prefix, $offset, [StringComparison]::Ordinal)
		if ($start -lt 0) {
			break
		}

		$index = $start + $prefix.Length
		$name = [Text.StringBuilder]::new()
		$closed = $false
		while ($index -lt $where.Length) {
			$character = $where[$index]
			if ($character -eq '\') {
				if ($index + 1 -ge $where.Length) {
					throw "Invalid escaped test name in '$Path'."
				}
				$index++
				[void] $name.Append($where[$index])
			} elseif ($character -eq "'") {
				$closed = $true
				$index++
				break
			} else {
				[void] $name.Append($character)
			}
			$index++
		}

		if (-not $closed) {
			throw "Unterminated test name in '$Path'."
		}

		$names.Add($name.ToString())
		$offset = $index
	}

	if ($names.Count -eq 0) {
		throw "No tests were found in '$Path'."
	}
	if ($names.Count -eq 1 -and $names[0] -eq 'dotnet-slicer-dummy-test-name') {
		throw "The test filter matched no tests in '$Path'."
	}

	return ,$names.ToArray()
}

function Get-HostBuildTestTimingHistory
{
	param (
		[string] $Path
	)

	$durations = [System.Collections.Generic.Dictionary[string,long]]::new([StringComparer]::Ordinal)
	if ([string]::IsNullOrWhiteSpace($Path)) {
		return $durations
	}
	if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
		throw "Timing history '$Path' does not exist."
	}

	[xml] $balance = Get-Content -LiteralPath $Path -Raw
	foreach ($test in @($balance.tests.test)) {
		$name = [string] $test.name
		$duration = 0L
		if ([string]::IsNullOrWhiteSpace($name) -or
			-not [long]::TryParse([string] $test.duration, [ref] $duration) -or
			$duration -lt 0) {
			throw "Timing history '$Path' contains an invalid test entry."
		}
		if (-not $durations.TryAdd($name, [Math]::Max(1, $duration))) {
			throw "Timing history '$Path' contains duplicate test '$name'."
		}
	}

	return $durations
}

function New-DurationBalancedWorkItems
{
	param (
		[Parameter(Mandatory)]
		[string[]] $TestNames,

		[Parameter(Mandatory)]
		[System.Collections.Generic.IDictionary[string,long]] $TimingHistory,

		[Parameter(Mandatory)]
		[ValidateRange(1, 1440)]
		[int] $TargetMinutes,

		[Parameter(Mandatory)]
		[ValidateRange(0.1, 128)]
		[double] $DurationParallelism,

		[ValidateRange(1, 86400)]
		[int] $FallbackTestDurationSeconds = 60
	)

	$uniqueNames = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
	foreach ($name in $TestNames) {
		if ([string]::IsNullOrWhiteSpace($name)) {
			throw 'Test names cannot be empty.'
		}
		if (-not $uniqueNames.Add($name)) {
			throw "Duplicate test name '$name'."
		}
	}
	if ($uniqueNames.Count -eq 0) {
		throw 'At least one test is required.'
	}

	$knownDurations = [System.Collections.Generic.List[long]]::new()
	foreach ($name in $uniqueNames) {
		$duration = 0L
		if ($TimingHistory.TryGetValue($name, [ref] $duration)) {
			$knownDurations.Add($duration)
		}
	}

	if ($knownDurations.Count -eq 0) {
		$missingDuration = [long] $FallbackTestDurationSeconds * 1000
		$durationMode = 'count-fallback'
	} else {
		$missingDuration = [long] [Math]::Round(
			($knownDurations | Measure-Object -Average).Average,
			[MidpointRounding]::AwayFromZero
		)
		$durationMode = if ($knownDurations.Count -eq $uniqueNames.Count) { 'historical' } else { 'partial-history' }
	}

	$tests = [System.Collections.Generic.List[object]]::new()
	foreach ($name in $uniqueNames) {
		$duration = 0L
		$isEstimated = -not $TimingHistory.TryGetValue($name, [ref] $duration)
		if ($isEstimated) {
			$duration = $missingDuration
		}
		$tests.Add([pscustomobject] @{
			Name = $name
			DurationMs = $duration
			IsEstimated = $isEstimated
		})
	}

	$comparison = [System.Comparison[object]] {
		param ($left, $right)
		$durationComparison = $right.DurationMs.CompareTo($left.DurationMs)
		if ($durationComparison -ne 0) {
			return $durationComparison
		}
		return [StringComparer]::Ordinal.Compare($left.Name, $right.Name)
	}
	$tests.Sort($comparison)

	$targetLoadMs = [long] [Math]::Round(
		$TargetMinutes * 60000 * $DurationParallelism,
		[MidpointRounding]::AwayFromZero
	)
	$bins = [System.Collections.Generic.List[object]]::new()

	foreach ($test in $tests) {
		$selectedBin = $null
		if ($test.DurationMs -le $targetLoadMs) {
			foreach ($bin in $bins) {
				if ($bin.Oversized -or $bin.DurationMs + $test.DurationMs -gt $targetLoadMs) {
					continue
				}
				if ($null -eq $selectedBin -or
					$bin.DurationMs -gt $selectedBin.DurationMs -or
					($bin.DurationMs -eq $selectedBin.DurationMs -and $bin.Id -lt $selectedBin.Id)) {
					$selectedBin = $bin
				}
			}
		}

		if ($null -eq $selectedBin) {
			$selectedBin = [pscustomobject] @{
				Id = $bins.Count + 1
				DurationMs = 0L
				Oversized = $test.DurationMs -gt $targetLoadMs
				Tests = [System.Collections.Generic.List[object]]::new()
			}
			$bins.Add($selectedBin)
		}

		$selectedBin.Tests.Add($test)
		$selectedBin.DurationMs += $test.DurationMs
	}

	$workItems = [System.Collections.Generic.List[object]]::new()
	foreach ($bin in $bins) {
		$sortedNames = [System.Collections.Generic.List[string]]::new()
		foreach ($test in $bin.Tests) {
			$sortedNames.Add($test.Name)
		}
		$sortedNames.Sort([StringComparer]::Ordinal)

		$workItems.Add([pscustomobject] @{
			Id = $bin.Id
			DurationMs = $bin.DurationMs
			EstimatedDurationMs = [long] [Math]::Ceiling($bin.DurationMs / $DurationParallelism)
			Oversized = $bin.Oversized
			Tests = $sortedNames.ToArray()
		})
	}

	return [pscustomobject] @{
		DurationMode = $durationMode
		KnownTestCount = $knownDurations.Count
		EstimatedTestCount = $uniqueNames.Count - $knownDurations.Count
		MissingTestDurationMs = $missingDuration
		TargetLoadMs = $targetLoadMs
		TargetMinutes = $TargetMinutes
		DurationParallelism = $DurationParallelism
		TotalDurationMs = ($tests | Measure-Object -Property DurationMs -Sum).Sum
		WorkItems = $workItems.ToArray()
	}
}

function Write-NUnitRunSettings
{
	param (
		[Parameter(Mandatory)]
		[string[]] $TestNames,

		[Parameter(Mandatory)]
		[string] $Path
	)

	$settings = [Xml.XmlWriterSettings]::new()
	$settings.Indent = $true
	$settings.Encoding = [Text.UTF8Encoding]::new($false)
	$writer = [Xml.XmlWriter]::Create($Path, $settings)
	try {
		$filters = foreach ($name in $TestNames) {
			$escaped = $name.Replace('\', '\\').Replace("'", "\'")
			"test == '$escaped'"
		}
		$writer.WriteStartElement('RunSettings')
		$writer.WriteStartElement('NUnit')
		$writer.WriteElementString('Where', ($filters -join ' or '))
		$writer.WriteEndElement()
		$writer.WriteEndElement()
	} finally {
		$writer.Dispose()
	}
}

function Write-HostBuildTestCommand
{
	param (
		[Parameter(Mandatory)]
		[ValidateSet('windows', 'linux')]
		[string] $Platform,

		[Parameter(Mandatory)]
		[string] $TestAssemblyRelativePath,

		[Parameter(Mandatory)]
		[string] $NUnitWorkers,

		[Parameter(Mandatory)]
		[string] $Configuration,

		[Parameter(Mandatory)]
		[string] $Path
	)

	if ($Platform -eq 'windows') {
		$assemblyPath = $TestAssemblyRelativePath.Replace('/', '\')
		$command = @'
@echo off
setlocal
if "%HELIX_CORRELATION_PAYLOAD%"=="" exit /b 2
if "%HELIX_WORKITEM_ROOT%"=="" exit /b 2
if "%HELIX_WORKITEM_UPLOAD_ROOT%"=="" exit /b 2
set "REPO=%HELIX_CORRELATION_PAYLOAD%\repo"
set "DOTNET_ROOT=%REPO%\bin\__CONFIGURATION__\dotnet"
set "ANDROID_HOME=%HELIX_CORRELATION_PAYLOAD%\android-toolchain\sdk"
set "ANDROID_SDK_ROOT=%ANDROID_HOME%"
set "TEST_ANDROID_NDK_PATH=%HELIX_CORRELATION_PAYLOAD%\android-toolchain\ndk"
set "ANDROID_NDK_LATEST_HOME=%TEST_ANDROID_NDK_PATH%"
set "JAVA_HOME=%HELIX_CORRELATION_PAYLOAD%\jdk"
set "NUGET_PACKAGES=%HELIX_WORKITEM_ROOT%\nuget-packages"
set "GRADLE_USER_HOME=%HELIX_CORRELATION_PAYLOAD%\gradle"
set "DOTNET_CLI_HOME=%HELIX_WORKITEM_ROOT%\dotnet-home"
set "TEMP=%HELIX_WORKITEM_ROOT%\temp"
set "TMP=%TEMP%"
set "BUILD_STAGINGDIRECTORY=%HELIX_WORKITEM_UPLOAD_ROOT%"
set "PATH=%HELIX_CORRELATION_PAYLOAD%\dotnet-tools;%DOTNET_ROOT%;%ANDROID_HOME%\platform-tools;%JAVA_HOME%\bin;%PATH%"
set "RUNNINGONCI=true"
set "BuildInParallel=false"
set "_AndroidBuildRuntimeIdentifiersInParallel=false"
set "DOTNET_CLI_TELEMETRY_OPTOUT=1"
set "DOTNET_NOLOGO=1"
set "DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1"
set "DOTNET_GENERATE_ASPNET_CERTIFICATE=false"
set "DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true"
set "DOTNET_SDK_VULNERABILITY_CHECK_DISABLE=true"
set "DOTNET_SYSTEM_NET_SECURITY_NOREVOCATIONCHECKBYDEFAULT=true"
set "NuGetAudit=false"
set "DOTNET_DbgEnableMiniDump=1"
set "DOTNET_DbgMiniDumpType=2"
set "DOTNET_DbgMiniDumpName=%HELIX_WORKITEM_UPLOAD_ROOT%\dumps\%%e.%%p.dmp"
if not exist "%DOTNET_ROOT%\dotnet.exe" exit /b 3
if not exist "%REPO%\__TEST_ASSEMBLY__" exit /b 3
mkdir "%HELIX_WORKITEM_UPLOAD_ROOT%\dumps" 2>nul
mkdir "%DOTNET_CLI_HOME%" 2>nul
mkdir "%NUGET_PACKAGES%" 2>nul
mkdir "%TEMP%" 2>nul
copy /y "%~dp0slice.runsettings" "%HELIX_WORKITEM_UPLOAD_ROOT%\slice.runsettings" >nul
copy /y "%~dp0work-item.json" "%HELIX_WORKITEM_UPLOAD_ROOT%\work-item.json" >nul
pushd "%REPO%"
"%DOTNET_ROOT%\dotnet.exe" test "%REPO%\__TEST_ASSEMBLY__" --settings "%~dp0slice.runsettings" --logger "trx;LogFileName=results.trx" --results-directory "%HELIX_WORKITEM_UPLOAD_ROOT%" -- NUnit.NumberOfTestWorkers=__NUNIT_WORKERS__ > "%HELIX_WORKITEM_UPLOAD_ROOT%\console.log" 2>&1
set "testExitCode=%ERRORLEVEL%"
type "%HELIX_WORKITEM_UPLOAD_ROOT%\console.log"
pushd "%HELIX_WORKITEM_UPLOAD_ROOT%"
tar.exe -a -c -f diagnostics.zip --exclude=diagnostics.zip TestRelease dumps >nul 2>&1
popd
"%DOTNET_ROOT%\dotnet.exe" build-server shutdown >nul 2>&1
popd
exit /b %testExitCode%
'@
		$command = $command.Replace('__TEST_ASSEMBLY__', $assemblyPath)
		$command = $command.Replace('__NUNIT_WORKERS__', $NUnitWorkers)
		$command = $command.Replace('__CONFIGURATION__', $Configuration)
		[IO.File]::WriteAllText($Path, $command, [Text.Encoding]::ASCII)
	} else {
		$assemblyPath = $TestAssemblyRelativePath.Replace('\', '/')
		$command = @'
#!/usr/bin/env bash
set -euo pipefail
: "${HELIX_CORRELATION_PAYLOAD:?}"
: "${HELIX_WORKITEM_ROOT:?}"
: "${HELIX_WORKITEM_UPLOAD_ROOT:?}"
REPO="$HELIX_CORRELATION_PAYLOAD/repo"
export DOTNET_ROOT="$REPO/bin/__CONFIGURATION__/dotnet"
export ANDROID_HOME="$HELIX_CORRELATION_PAYLOAD/android-toolchain/sdk"
export ANDROID_SDK_ROOT="$ANDROID_HOME"
export JAVA_HOME="$HELIX_CORRELATION_PAYLOAD/jdk"
export NUGET_PACKAGES="$HELIX_WORKITEM_ROOT/nuget-packages"
export GRADLE_USER_HOME="$HELIX_CORRELATION_PAYLOAD/gradle"
export DOTNET_CLI_HOME="$HELIX_WORKITEM_ROOT/dotnet-home"
export TMPDIR="$HELIX_WORKITEM_ROOT/temp"
export BUILD_STAGINGDIRECTORY="$HELIX_WORKITEM_UPLOAD_ROOT"
export PATH="$HELIX_CORRELATION_PAYLOAD/dotnet-tools:$DOTNET_ROOT:$ANDROID_HOME/platform-tools:$JAVA_HOME/bin:$PATH"
export RUNNINGONCI=true
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
export DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true
export DOTNET_SDK_VULNERABILITY_CHECK_DISABLE=true
export DOTNET_SYSTEM_NET_SECURITY_NOREVOCATIONCHECKBYDEFAULT=true
export NuGetAudit=false
export DOTNET_DbgEnableMiniDump=1
export DOTNET_DbgMiniDumpType=2
export DOTNET_DbgMiniDumpName="$HELIX_WORKITEM_UPLOAD_ROOT/dumps/%e.%p.dmp"
test -x "$DOTNET_ROOT/dotnet"
test -f "$REPO/__TEST_ASSEMBLY__"
mkdir -p "$HELIX_WORKITEM_UPLOAD_ROOT/dumps" "$DOTNET_CLI_HOME" "$NUGET_PACKAGES" "$TMPDIR"
cp "$HELIX_WORKITEM_ROOT/slice.runsettings" "$HELIX_WORKITEM_UPLOAD_ROOT/slice.runsettings"
cp "$HELIX_WORKITEM_ROOT/work-item.json" "$HELIX_WORKITEM_UPLOAD_ROOT/work-item.json"
cd "$REPO"
set +e
"$DOTNET_ROOT/dotnet" test "$REPO/__TEST_ASSEMBLY__" --settings "$HELIX_WORKITEM_ROOT/slice.runsettings" --logger "trx;LogFileName=results.trx" --results-directory "$HELIX_WORKITEM_UPLOAD_ROOT" -- NUnit.NumberOfTestWorkers=__NUNIT_WORKERS__ 2>&1 | tee "$HELIX_WORKITEM_UPLOAD_ROOT/console.log"
test_exit=${PIPESTATUS[0]}
set -e
(cd "$HELIX_WORKITEM_UPLOAD_ROOT" && find . -type f \( -name '*.binlog' -o -name '*.dmp' -o -name '*.log' -o -name '*.txt' \) -print0 | tar --null -czf diagnostics.tar.gz --files-from=-) || true
"$DOTNET_ROOT/dotnet" build-server shutdown >/dev/null 2>&1 || true
exit "$test_exit"
'@
		$command = $command.Replace('__TEST_ASSEMBLY__', $assemblyPath)
		$command = $command.Replace('__NUNIT_WORKERS__', $NUnitWorkers)
		$command = $command.Replace('__CONFIGURATION__', $Configuration)
		[IO.File]::WriteAllText($Path, $command, [Text.UTF8Encoding]::new($false))
	}
}

function Write-HostBuildTestWorkItemPayloads
{
	param (
		[Parameter(Mandatory)]
		[ValidateSet('windows', 'linux')]
		[string] $Platform,

		[Parameter(Mandatory)]
		[object] $Plan,

		[Parameter(Mandatory)]
		[string] $WorkItemsDirectory,

		[Parameter(Mandatory)]
		[string] $WorkItemsPropsPath,

		[Parameter(Mandatory)]
		[string] $TestAssemblyRelativePath,

		[Parameter(Mandatory)]
		[string] $NUnitWorkers,

		[Parameter(Mandatory)]
		[string] $Configuration
	)

	Remove-Item -LiteralPath $WorkItemsDirectory -Recurse -Force -ErrorAction Ignore
	New-Item -ItemType Directory -Force -Path $WorkItemsDirectory | Out-Null

	$propsSettings = [Xml.XmlWriterSettings]::new()
	$propsSettings.Indent = $true
	$propsSettings.Encoding = [Text.UTF8Encoding]::new($false)
	$propsWriter = [Xml.XmlWriter]::Create($WorkItemsPropsPath, $propsSettings)
	try {
		$propsWriter.WriteStartElement('Project')
		$propsWriter.WriteStartElement('ItemGroup')

		foreach ($workItem in $Plan.WorkItems) {
			$name = 'host-build-tests-{0}-{1:d3}' -f $Platform, $workItem.Id
			$payloadDirectory = Join-Path $WorkItemsDirectory $name
			New-Item -ItemType Directory -Force -Path $payloadDirectory | Out-Null

			Write-NUnitRunSettings -TestNames $workItem.Tests -Path (Join-Path $payloadDirectory 'slice.runsettings')
			$commandName = if ($Platform -eq 'windows') { 'run-host-tests.cmd' } else { 'run-host-tests.sh' }
			$helixCommand = if ($Platform -eq 'windows') { $commandName } else { "bash $commandName" }
			Write-HostBuildTestCommand `
				-Platform $Platform `
				-TestAssemblyRelativePath $TestAssemblyRelativePath `
				-NUnitWorkers $NUnitWorkers `
				-Configuration $Configuration `
				-Path (Join-Path $payloadDirectory $commandName)

			[pscustomobject] @{
				name = $name
				durationMode = $Plan.DurationMode
				estimatedDurationMs = $workItem.EstimatedDurationMs
				durationLoadMs = $workItem.DurationMs
				oversized = $workItem.Oversized
				targetMinutes = $Plan.TargetMinutes
				durationParallelism = $Plan.DurationParallelism
				testCount = $workItem.Tests.Count
				tests = $workItem.Tests
			} | ConvertTo-Json -Depth 5 |
				Set-Content -LiteralPath (Join-Path $payloadDirectory 'work-item.json') -Encoding utf8NoBOM

			$propsWriter.WriteStartElement('_HostBuildTestHelixWorkItem')
			$propsWriter.WriteAttributeString('Include', $name)
			$propsWriter.WriteElementString('PayloadDirectory', $payloadDirectory)
			$propsWriter.WriteElementString('Command', $helixCommand)
			$diagnosticsArchive = if ($Platform -eq 'windows') { 'diagnostics.zip' } else { 'diagnostics.tar.gz' }
			$propsWriter.WriteElementString('DownloadFilesFromResults', "results.trx;console.log;slice.runsettings;work-item.json;$diagnosticsArchive")
			$propsWriter.WriteElementString('EstimatedDurationMs', [string] $workItem.EstimatedDurationMs)
			$propsWriter.WriteElementString('TestCount', [string] $workItem.Tests.Count)
			$propsWriter.WriteEndElement()
		}

		$propsWriter.WriteEndElement()
		$propsWriter.WriteEndElement()
	} finally {
		$propsWriter.Dispose()
	}
}

Export-ModuleMember -Function `
	Get-NUnitRunSettingsTestNames, `
	Get-HostBuildTestTimingHistory, `
	New-DurationBalancedWorkItems, `
	Write-NUnitRunSettings, `
	Write-HostBuildTestWorkItemPayloads
