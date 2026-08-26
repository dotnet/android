Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

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

$root = Join-Path $PSScriptRoot '.test-output'
Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction Ignore
try {
	$workItem = Join-Path $root 'work-items\sample'
	$platformTools = Join-Path $root 'platform-tools'
	New-Item -ItemType Directory -Force -Path $workItem, $platformTools | Out-Null
	Set-Content -LiteralPath (Join-Path $workItem 'app.apk') -Value 'apk'
	Set-Content -LiteralPath (Join-Path $platformTools 'adb.exe') -Value 'adb'
	[pscustomobject] @{
		name = 'sample'
		displayName = 'Sample APK Tests'
		packageName = 'example.tests'
		instrumentation = 'example.tests.TestInstrumentation'
	} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $workItem 'case.json')

	$propsPath = Join-Path $root 'items.props'
	& (Join-Path $PSScriptRoot 'prepare-apk-test-helix-submission.ps1') `
		-WorkItemsDirectory (Join-Path $root 'work-items') `
		-CorrelationPayloadDirectory (Join-Path $root 'correlation') `
		-ItemsPropsPath $propsPath `
		-ResultsDirectory (Join-Path $root 'results') `
		-PlatformToolsDirectory $platformTools `
		-TargetMinutes 15

	[xml] $props = Get-Content -LiteralPath $propsPath -Raw
	$item = $props.Project.ItemGroup._ApkTestHelixWorkItem
	Assert-Equal 'apk-tests-sample' ([string] $item.Include) 'Work item name should be deterministic.'
	Assert-Equal 'results.trx;console.log;logcat.log;device-state.log;case.json;work-item-error.log' ([string] $item.DownloadFilesFromResults) 'Result files should be downloaded.'

	$script = Get-Content -LiteralPath (Join-Path $workItem 'run-apk-tests.ps1') -Raw
	if (-not $script.Contains('$packageName = ''example.tests''') -or
		-not $script.Contains('$instrumentation = ''example.tests.TestInstrumentation''') -or
		-not $script.Contains('INSTRUMENTATION_RESULT: resultsPath=')) {
		throw 'Generated APK test script is missing required instrumentation values.'
	}
	$tokens = $null
	$errors = $null
	[Management.Automation.Language.Parser]::ParseFile((Join-Path $workItem 'run-apk-tests.ps1'), [ref] $tokens, [ref] $errors) | Out-Null
	if ($errors.Count -gt 0) {
		throw ($errors | ForEach-Object Message | Out-String)
	}

	Write-Host 'ApkTestsHelix tests passed.'
} finally {
	Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction Ignore
}
