Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'HostBuildTestsHelix.psm1') -Force

function Assert-Equal
{
	param (
		[Parameter(Mandatory)]
		[object] $Expected,

		[Parameter(Mandatory)]
		[object] $Actual,

		[Parameter(Mandatory)]
		[string] $Message
	)

	if ($Expected -ne $Actual) {
		throw "$Message Expected '$Expected', got '$Actual'."
	}
}

$testRoot = Join-Path $PSScriptRoot '.test-output'
Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction Ignore
New-Item -ItemType Directory -Force -Path $testRoot | Out-Null

try {
	$emptyHistory = [System.Collections.Generic.Dictionary[string,long]]::new([StringComparer]::Ordinal)
	$fallbackPlan = New-DurationBalancedWorkItems `
		-TestNames @('F', 'E', 'D', 'C', 'B', 'A') `
		-TimingHistory $emptyHistory `
		-TargetMinutes 1 `
		-DurationParallelism 1 `
		-FallbackTestDurationSeconds 30

	Assert-Equal 'count-fallback' $fallbackPlan.DurationMode 'No-history mode should be explicit.'
	Assert-Equal 3 $fallbackPlan.WorkItems.Count 'Equal fallback durations should create three full bins.'
	foreach ($workItem in $fallbackPlan.WorkItems) {
		Assert-Equal 2 $workItem.Tests.Count 'Each fallback bin should contain two tests.'
		Assert-Equal 60000 $workItem.EstimatedDurationMs 'Each fallback bin should target one minute.'
	}

	$history = [System.Collections.Generic.Dictionary[string,long]]::new([StringComparer]::Ordinal)
	$history.Add('Oversized', 720000)
	$history.Add('Eight', 480000)
	$history.Add('Six', 360000)
	$history.Add('Four', 240000)
	$historicalPlan = New-DurationBalancedWorkItems `
		-TestNames @('Four', 'Six', 'Eight', 'Oversized') `
		-TimingHistory $history `
		-TargetMinutes 10 `
		-DurationParallelism 1

	Assert-Equal 'historical' $historicalPlan.DurationMode 'Complete history should be reported.'
	Assert-Equal 3 $historicalPlan.WorkItems.Count 'Best-fit packing should create three bins.'
	$oversized = @($historicalPlan.WorkItems | Where-Object Oversized)
	Assert-Equal 1 $oversized.Count 'Only the over-target test should be oversized.'
	Assert-Equal 1 $oversized[0].Tests.Count 'An over-target test must remain isolated.'

	$partialHistory = [System.Collections.Generic.Dictionary[string,long]]::new([StringComparer]::Ordinal)
	$partialHistory.Add('KnownOne', 10000)
	$partialHistory.Add('KnownTwo', 30000)
	$partialPlan = New-DurationBalancedWorkItems `
		-TestNames @('KnownOne', 'KnownTwo', 'NewTest') `
		-TimingHistory $partialHistory `
		-TargetMinutes 1 `
		-DurationParallelism 1

	Assert-Equal 'partial-history' $partialPlan.DurationMode 'Missing tests should be reported as partial history.'
	Assert-Equal 20000 $partialPlan.MissingTestDurationMs 'Missing tests should use the selected-history average.'

	$zeroTimingPath = Join-Path $testRoot 'zero-timing.xml'
	Set-Content -LiteralPath $zeroTimingPath -Value '<tests><test name="FastTest" duration="0" /></tests>'
	$zeroTimingHistory = Get-HostBuildTestTimingHistory -Path $zeroTimingPath
	Assert-Equal 1 $zeroTimingHistory['FastTest'] 'Sub-millisecond TRX timings should retain a minimal positive load.'

	$specialNames = @("Namespace.Test('value')", 'Namespace.Path(C:\repo\file)')
	$runSettingsPath = Join-Path $testRoot 'special.runsettings'
	Write-NUnitRunSettings -TestNames $specialNames -Path $runSettingsPath
	$roundTrippedNames = Get-NUnitRunSettingsTestNames -Path $runSettingsPath
	Assert-Equal ($specialNames -join '|') ($roundTrippedNames -join '|') 'Runsettings names should round trip.'

	$singleRunSettingsPath = Join-Path $testRoot 'single.runsettings'
	Write-NUnitRunSettings -TestNames @('OnlyTest') -Path $singleRunSettingsPath
	$singleTest = Get-NUnitRunSettingsTestNames -Path $singleRunSettingsPath
	Assert-Equal 1 $singleTest.Count 'A one-test runsettings file should remain an array.'
	Assert-Equal 'OnlyTest' $singleTest[0] 'The single test should round trip.'

	$emptyRunSettingsPath = Join-Path $testRoot 'empty.runsettings'
	Set-Content -LiteralPath $emptyRunSettingsPath -Value "<RunSettings><NUnit><Where>test == 'dotnet-slicer-dummy-test-name'</Where></NUnit></RunSettings>"
	try {
		Get-NUnitRunSettingsTestNames -Path $emptyRunSettingsPath
		throw 'The slicer dummy test should have caused an error.'
	} catch {
		if ($_.Exception.Message -notlike '*matched no tests*') {
			throw
		}
	}

	$secondFallbackPlan = New-DurationBalancedWorkItems `
		-TestNames @('F', 'E', 'D', 'C', 'B', 'A') `
		-TimingHistory $emptyHistory `
		-TargetMinutes 1 `
		-DurationParallelism 1 `
		-FallbackTestDurationSeconds 30
	Assert-Equal `
		($fallbackPlan | ConvertTo-Json -Depth 8 -Compress) `
		($secondFallbackPlan | ConvertTo-Json -Depth 8 -Compress) `
		'Work-item generation should be deterministic.'

	$payloadDirectory = Join-Path $testRoot 'payloads'
	$propsPath = Join-Path $testRoot 'work-items.props'
	Write-HostBuildTestWorkItemPayloads `
		-Platform windows `
		-Plan $fallbackPlan `
		-WorkItemsDirectory $payloadDirectory `
		-WorkItemsPropsPath $propsPath `
		-TestAssemblyRelativePath 'bin\TestRelease\net10.0\Xamarin.Android.Build.Tests.dll' `
		-NUnitWorkers '-1' `
		-Configuration Release

	[xml] $props = Get-Content -LiteralPath $propsPath -Raw
	Assert-Equal 3 @($props.Project.ItemGroup._HostBuildTestHelixWorkItem).Count 'Props should contain every generated work item.'
	foreach ($item in @($props.Project.ItemGroup._HostBuildTestHelixWorkItem)) {
		if (-not (Test-Path -LiteralPath $item.PayloadDirectory -PathType Container)) {
			throw "Payload directory '$($item.PayloadDirectory)' was not created."
		}
		$command = Get-Content -LiteralPath (Join-Path $item.PayloadDirectory 'run-host-tests.cmd') -Raw
		if ($command.Contains('__CONFIGURATION__') -or
			-not $command.Contains('bin\Release\dotnet') -or
			$command.Contains('set "CONFIGURATION=Release"') -or
			-not $command.Contains('set "RUNNINGONCI=true"') -or
			-not $command.Contains('set "TEST_ANDROID_NDK_PATH=') -or
			-not $command.Contains('tar.exe -a -c -f diagnostics.zip')) {
			throw 'The generated command did not apply the requested build configuration.'
		}
		Assert-Equal 'results.trx;console.log;slice.runsettings;work-item.json;diagnostics.zip' ([string] $item.DownloadFilesFromResults) 'Windows results should be downloaded.'
	}

	$linuxPayloadDirectory = Join-Path $testRoot 'linux-payloads'
	$linuxPropsPath = Join-Path $testRoot 'linux-work-items.props'
	Write-HostBuildTestWorkItemPayloads `
		-Platform linux `
		-Plan $fallbackPlan `
		-WorkItemsDirectory $linuxPayloadDirectory `
		-WorkItemsPropsPath $linuxPropsPath `
		-TestAssemblyRelativePath 'bin/TestRelease/net10.0/Xamarin.Android.Build.Tests.dll' `
		-NUnitWorkers '-1' `
		-Configuration Release
	[xml] $linuxProps = Get-Content -LiteralPath $linuxPropsPath -Raw
	foreach ($item in @($linuxProps.Project.ItemGroup._HostBuildTestHelixWorkItem)) {
		Assert-Equal 'bash run-host-tests.sh' ([string] $item.Command) 'Linux work items should explicitly use bash.'
		Assert-Equal 'results.trx;console.log;slice.runsettings;work-item.json;diagnostics.tar.gz' ([string] $item.DownloadFilesFromResults) 'Linux results should be downloaded.'
	}

	Write-Host 'HostBuildTestsHelix tests passed.'
} finally {
	Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction Ignore
}
