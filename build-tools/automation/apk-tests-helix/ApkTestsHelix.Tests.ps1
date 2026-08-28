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
		-ItemsPropsPath $propsPath `
		-ResultsDirectory (Join-Path $root 'results') `
		-PlatformToolsDirectory $platformTools `
		-TargetMinutes 15

	[xml] $props = Get-Content -LiteralPath $propsPath -Raw
	$item = $props.Project.ItemGroup._ApkTestHelixWorkItem
	Assert-Equal 'apk-tests-sample' ([string] $item.Include) 'Work item name should be deterministic.'
	Assert-Equal 'results.trx;console.log;logcat.log;device-state.log;getprop.log;package-state.log;case.json;work-item-error.log' ([string] $item.DownloadFilesFromResults) 'Result files should be downloaded.'

	$script = Get-Content -LiteralPath (Join-Path $workItem 'run-apk-tests.ps1') -Raw
	if (-not $script.Contains('$packageName = ''example.tests''') -or
		-not $script.Contains('$instrumentation = ''example.tests.TestInstrumentation''') -or
		-not $script.Contains('INSTRUMENTATION_RESULT: resultsPath=') -or
		-not $script.Contains('$attempt -le 12') -or
		-not $script.Contains('$attempt -le 2')) {
		throw 'Generated APK test script is missing required instrumentation values.'
	}
	if (-not (Test-Path -LiteralPath (Join-Path $workItem 'platform-tools\adb.exe') -PathType Leaf)) {
		throw 'Platform tools were not copied into the work item payload.'
	}
	$tokens = $null
	$errors = $null
	[Management.Automation.Language.Parser]::ParseFile((Join-Path $workItem 'run-apk-tests.ps1'), [ref] $tokens, [ref] $errors) | Out-Null
	if ($errors.Count -gt 0) {
		throw ($errors | ForEach-Object Message | Out-String)
	}

	$manifestPath = Join-Path $root 'AndroidManifest.xml'
	@'
	<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="example.tests">
	  <uses-permission android:name="android.permission.INTERNET" />
	  <application android:name="example.tests.App">
	    <activity android:name="example.tests.Activity" android:configChanges="keyboardHidden" />
	  </application>
	</manifest>
'@ | Set-Content -LiteralPath $manifestPath
	& (Join-Path $PSScriptRoot 'inject-apk-test-manifest.ps1') `
		-ManifestPath $manifestPath `
		-PackageName 'example.tests' `
		-Instrumentation 'example.tests.TestInstrumentation'

	[xml] $manifest = Get-Content -LiteralPath $manifestPath -Raw
	$manager = [Xml.XmlNamespaceManager]::new($manifest.NameTable)
	$manager.AddNamespace('android', 'http://schemas.android.com/apk/res/android')
	Assert-Equal 'example.tests.App' $manifest.manifest.application.name 'Manifest injection should preserve the application.'
	Assert-Equal 'keyboardHidden' $manifest.manifest.application.activity.configChanges 'Manifest injection should preserve activity metadata.'
	Assert-Equal 1 @($manifest.SelectNodes("/manifest/instrumentation[@android:name='example.tests.TestInstrumentation']", $manager)).Count 'Instrumentation should be added once.'
	Assert-Equal 1 @($manifest.SelectNodes("/manifest/uses-permission[@android:name='android.permission.ACCESS_LOCAL_NETWORK']", $manager)).Count 'Android 17 local-network permission should be added.'
	Assert-Equal 1 @($manifest.SelectNodes("/manifest/uses-permission[@android:name='android.permission.NEARBY_WIFI_DEVICES']", $manager)).Count 'Android 16 local-network permission should be added.'

	Write-Host 'ApkTestsHelix tests passed.'
} finally {
	Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction Ignore
}
