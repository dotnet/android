param (
	[switch] $InjectAssertionFailure
)

$ErrorActionPreference = 'Stop'

$runnerScript = Join-Path $PSScriptRoot 'RunMauiR2RHelix.ps1'
$powerShellExe = (Get-Process -Id $PID).Path
if (-not $powerShellExe) {
	$powerShellExe = 'powershell.exe'
}

function Assert-Equal ($Expected, $Actual, [string] $Message)
{
	if ($Expected -ne $Actual) {
		throw "$Message Expected '$Expected', but found '$Actual'."
	}
}

function Assert-Contains ([string []] $Lines, [string] $Pattern, [string] $Message)
{
	if (-not ($Lines | Where-Object { $_ -match $Pattern })) {
		throw "$Message Pattern '$Pattern' was not found."
	}
}

function Assert-NotContains ([string []] $Lines, [string] $Pattern, [string] $Message)
{
	if ($Lines | Where-Object { $_ -match $Pattern }) {
		throw "$Message Pattern '$Pattern' was unexpectedly found."
	}
}

function Assert-MatchCount ([string []] $Lines, [string] $Pattern, [int] $Expected, [string] $Message)
{
	$actual = @($Lines | Where-Object { $_ -match $Pattern }).Count
	Assert-Equal $Expected $actual $Message
}

$testRoot = Join-Path ([IO.Path]::GetTempPath()) "Maui R2R $([IO.Path]::GetRandomFileName())"
$fakeAdb = Join-Path $testRoot 'FakeAdb.ps1'
$apk = Join-Path $testRoot 'MauiR2R-Signed.apk'
$suitePassed = $false

try {
	New-Item -ItemType Directory -Path $testRoot | Out-Null
	Set-Content -LiteralPath $apk -Value 'test APK placeholder' -Encoding ASCII
	if ($InjectAssertionFailure) {
		Assert-Equal 'expected' 'injected failure' 'Injected process-level assertion failure.'
	}

	@'
$ErrorActionPreference = 'Stop'

$command = $args -join ' '
$transcriptPath = $env:FAKE_ADB_TRANSCRIPT
$scenario = $env:FAKE_ADB_SCENARIO
$previousCommands = @()
if (Test-Path -LiteralPath $transcriptPath) {
	$previousCommands = @(Get-Content -LiteralPath $transcriptPath)
}
Add-Content -LiteralPath $transcriptPath -Value $command -Encoding ASCII

$serverRestarted = [bool]($previousCommands | Where-Object { $_ -eq 'start-server' })
$deviceRebooted = [bool]($previousCommands | Where-Object { $_ -match '^-s test-device reboot$' })
$targetUninstalled = [bool]($previousCommands | Where-Object { $_ -match '^-s test-device uninstall com\.xamarin\.mauir2r\.' })
$deviceQueryCount = @($previousCommands | Where-Object { $_ -eq 'devices -l' }).Count

if ($command -eq 'version') {
	if ($scenario -eq 'overall-timeout') {
		Start-Sleep -Seconds 5
	}
	Write-Output 'Android Debug Bridge version 1.0.41'
	exit 0
}

if ($command -eq 'devices -l') {
	Write-Output 'List of devices attached'
	if ($scenario -eq 'daemon-noise') {
		Write-Output '* daemon not running; starting now at tcp:5037'
		Write-Output '* daemon started successfully'
	}
	switch ($scenario) {
		'recover-device' {
			if ($serverRestarted) {
				Write-Output 'test-device             device product:test model:Test_Device device:test transport_id:1'
			} else {
				Write-Output 'test-device             unauthorized transport_id:1'
			}
		}
		'recover-device-after-polls' {
			if ($serverRestarted -and $deviceQueryCount -ge 3) {
				Write-Output 'test-device             device product:test model:Test_Device device:test transport_id:1'
			} else {
				Write-Output 'test-device             unauthorized transport_id:1'
			}
		}
		'unauthorized' {
			Write-Output 'test-device             unauthorized transport_id:1'
		}
		'no-device' {
		}
		'multiple-devices' {
			Write-Output 'test-device             device product:test model:Test_Device device:test transport_id:1'
			Write-Output 'other-device            device product:test model:Other_Device device:test transport_id:2'
		}
		default {
			Write-Output 'test-device             device product:test model:Test_Device device:test transport_id:1'
		}
	}
	exit 0
}

if ($command -eq 'kill-server' -or $command -eq 'start-server') {
	exit 0
}

if ($command -match '^-s test-device shell getprop sys\.boot_completed$') {
	Write-Output '1'
	exit 0
}

if ($command -match '^-s test-device shell pm path android$') {
	Write-Output 'package:/system/framework/framework-res.apk'
	exit 0
}

if ($command -match '^-s test-device shell getprop$') {
	if ($scenario -eq 'unicode-diagnostics') {
		Write-Output ('[ro.product.name]: [caf' + [string][char]0x00E9 + '-' + [string][char]0x6F22 + ']')
	} else {
		Write-Output '[ro.build.fingerprint]: [test/fingerprint]'
	}
	exit 0
}

if ($command -match '^-s test-device shell df ') {
	Write-Output '/dev/block/test 100G 20G 80G 20% /data'
	exit 0
}

if ($command -match '^-s test-device shell dumpsys (diskstats|storaged)') {
	Write-Output 'diagnostic output'
	exit 0
}

if ($command -match '^-s test-device shell pm list packages -3 -U$') {
	Write-Output 'package:com.xamarin.mauir2r.arm64.baseline uid:10123'
	exit 0
}

if ($command -match '^-s test-device shell pm path com\.xamarin\.mauir2r\.') {
	if ($scenario -eq 'unicode-diagnostics') {
		Write-Output ('package:/data/app/caf' + [string][char]0x00E9 + '-' + [string][char]0x6F22 + '/base.apk')
	} else {
		Write-Output 'package:/data/app/maui/base.apk'
	}
	exit 0
}

if ($command -match '^-s test-device shell dumpsys package com\.xamarin\.mauir2r\.') {
	if ($scenario -eq 'unicode-diagnostics') {
		Write-Output ('Package label: caf' + [string][char]0x00E9 + '-' + [string][char]0x6F22)
	} else {
		Write-Output 'Package diagnostic output'
	}
	exit 0
}

if ($command -match '^-s test-device install -r ') {
	switch ($scenario) {
		'uid-recovery' {
			if (-not $deviceRebooted) {
				[Console]::Error.WriteLine('Failure [INSTALL_FAILED_INSUFFICIENT_STORAGE: Scanning Failed.: Package com.xamarin.mauir2r.arm64.baseline could not be assigned a valid UID]')
				exit 1
			}
		}
		'uid-persistent' {
			[Console]::Error.WriteLine('Failure [INSTALL_FAILED_INSUFFICIENT_STORAGE: Scanning Failed.: Package com.xamarin.mauir2r.arm64.baseline could not be assigned a valid UID]')
			exit 1
		}
		'update-incompatible' {
			if (-not $targetUninstalled) {
				[Console]::Error.WriteLine('Failure [INSTALL_FAILED_UPDATE_INCOMPATIBLE: Package signatures do not match previously installed version]')
				exit 1
			}
		}
		'unknown-install-failure' {
			[Console]::Error.WriteLine('Failure [INSTALL_FAILED_INVALID_APK: Package is invalid]')
			exit 1
		}
	}

	Write-Output 'Success'
	exit 0
}

if ($command -match '^-s test-device uninstall com\.xamarin\.mauir2r\.') {
	Write-Output 'Success'
	exit 0
}

if ($command -eq '-s test-device reboot') {
	exit 0
}

if ($command -match '^-s test-device shell am force-stop com\.xamarin\.mauir2r\.') {
	exit 0
}

if ($command -match '^-s test-device shell pm clear com\.xamarin\.mauir2r\.') {
	Write-Output 'Success'
	exit 0
}

if ($command -eq '-s test-device logcat -c') {
	exit 0
}

if ($command -match '^-s test-device shell monkey -p com\.xamarin\.mauir2r\.') {
	Write-Output 'Events injected: 1'
	exit 0
}

if ($command -match '^-s test-device shell pidof com\.xamarin\.mauir2r\.') {
	if ($scenario -eq 'pid-missing') {
		exit 0
	}
	Write-Output '1234'
	exit 0
}

if ($command -eq '-s test-device logcat -d -b all') {
	if ($scenario -eq 'unicode-diagnostics') {
		Write-Output ('FATAL EXCEPTION: caf' + [string][char]0x00E9 + '-' + [string][char]0x6F22)
	} else {
		Write-Output 'test logcat'
	}
	exit 0
}

[Console]::Error.WriteLine("Unexpected fake adb command: $command")
exit 2
'@ | Set-Content -LiteralPath $fakeAdb -Encoding ASCII

	function Invoke-TestCase ([string] $Name, [int] $ExpectedExitCode, [bool] $SkipDiagnostics = $true, [int] $DeviceTimeoutSeconds = 0, [int] $OverallTimeoutSeconds = 540)
	{
		$caseDirectory = Join-Path $testRoot $Name
		New-Item -ItemType Directory -Path $caseDirectory | Out-Null
		$transcriptPath = Join-Path $caseDirectory 'adb-transcript.log'
		$outputPath = Join-Path $caseDirectory 'runner-output.log'

		$env:FAKE_ADB_SCENARIO = $Name
		$env:FAKE_ADB_TRANSCRIPT = $transcriptPath
		$env:HELIX_WORKITEM_UPLOAD_ROOT = $caseDirectory

		$optionalArguments = @()
		if ($SkipDiagnostics) {
			$optionalArguments += '-SkipExtendedDiagnostics'
		}

		& $powerShellExe -NoLogo -NoProfile -File $runnerScript `
			-AdbPath $fakeAdb `
			-ApkPath $apk `
			-PackageName 'com.xamarin.mauir2r.arm64.baseline' `
			-ScenarioName $Name `
			-ConfigurationName $Name `
			-RuntimeIdentifier 'android-arm64' `
			-DeviceRecoveryTimeoutSeconds $DeviceTimeoutSeconds `
			-AndroidReadyTimeoutSeconds 0 `
			-OverallTimeoutSeconds $OverallTimeoutSeconds `
			-PollIntervalMilliseconds 0 `
			-RebootInitialDelayMilliseconds 0 `
			-LaunchWaitMilliseconds 0 `
			@optionalArguments *> $outputPath

		$actualExitCode = $LASTEXITCODE
		$output = @(Get-Content -LiteralPath $outputPath)
		if ($actualExitCode -ne $ExpectedExitCode) {
			throw "Test case '$Name' exited with '$actualExitCode' instead of '$ExpectedExitCode'. Output:`n$($output -join [Environment]::NewLine)"
		}

		return [pscustomobject]@{
			Name = $Name
			Transcript = @(Get-Content -LiteralPath $transcriptPath)
			Output = $output
			Directory = $caseDirectory
		}
	}

	$healthy = Invoke-TestCase 'healthy' 0 $false
	Assert-NotContains $healthy.Transcript ' uninstall ' 'Healthy device flow must not uninstall the package.'
	Assert-NotContains $healthy.Transcript ' reboot$' 'Healthy device flow must not reboot the device.'
	Assert-Contains $healthy.Transcript ' shell pm list packages -3 -U$' 'Healthy device flow must capture package UID diagnostics.'
	Assert-MatchCount $healthy.Transcript ' shell pm clear com\.xamarin\.mauir2r\.arm64\.baseline$' 2 'Healthy device flow must clear package data before and after the smoke test.'
	Assert-MatchCount $healthy.Transcript ' shell am force-stop com\.xamarin\.mauir2r\.arm64\.baseline$' 2 'Healthy device flow must force-stop the package before and after the smoke test.'

	$recoveredDevice = Invoke-TestCase 'recover-device' 0
	Assert-MatchCount $recoveredDevice.Transcript '^kill-server$' 1 'Device enumeration recovery must restart the ADB server only once.'
	Assert-MatchCount $recoveredDevice.Transcript '^start-server$' 1 'Device enumeration recovery must restart the ADB server only once.'
	Assert-MatchCount $recoveredDevice.Transcript ' install -r ' 1 'Recovered device flow must install once.'

	$recoveredAfterPolls = Invoke-TestCase 'recover-device-after-polls' 0 $true 5
	$pollSnapshots = @(Get-ChildItem -LiteralPath $recoveredAfterPolls.Directory -Filter 'adb-devices-recover-device-after-polls-post-adb-restart*.log')
	Assert-Equal 3 $pollSnapshots.Count 'Each device recovery poll must preserve a separate adb devices snapshot.'

	$daemonNoise = Invoke-TestCase 'daemon-noise' 0
	Assert-NotContains $daemonNoise.Transcript '^kill-server$' 'ADB daemon startup messages must not be treated as extra or unauthorized devices.'
	Assert-MatchCount $daemonNoise.Transcript ' install -r ' 1 'ADB daemon startup messages must not block installation on the one real device.'

	$unauthorized = Invoke-TestCase 'unauthorized' 1
	Assert-MatchCount $unauthorized.Transcript '^kill-server$' 1 'Unauthorized device flow must attempt one ADB server restart.'
	Assert-NotContains $unauthorized.Transcript ' install -r ' 'Persistent unauthorized device flow must fail before installation.'
	Assert-Contains $unauthorized.Output 'device-lab owner must attach, authorize, or quarantine' 'Unauthorized failure must identify the service-owner action.'

	$noDevice = Invoke-TestCase 'no-device' 1
	Assert-MatchCount $noDevice.Transcript '^kill-server$' 1 'Missing-device flow must attempt one ADB server restart.'
	Assert-NotContains $noDevice.Transcript ' install -r ' 'Persistent missing-device flow must fail before installation.'
	Assert-Contains $noDevice.Output 'device-lab owner must attach, authorize, or quarantine' 'Missing-device failure must identify the service-owner action.'

	$multipleDevices = Invoke-TestCase 'multiple-devices' 1
	Assert-NotContains $multipleDevices.Transcript '^kill-server$' 'Multiple-device flow must fail without restarting the ADB server.'
	Assert-NotContains $multipleDevices.Transcript ' install -r ' 'Multiple-device flow must fail before installation.'
	Assert-Contains $multipleDevices.Output 'expected exactly one Android device' 'Multiple-device failure must identify the ambiguous device state.'

	$uidRecovery = Invoke-TestCase 'uid-recovery' 0
	Assert-MatchCount $uidRecovery.Transcript ' install -r ' 2 'UID exhaustion recovery must retry installation exactly once.'
	Assert-MatchCount $uidRecovery.Transcript '^-s test-device reboot$' 1 'UID exhaustion recovery must reboot exactly once.'
	Assert-NotContains $uidRecovery.Transcript ' uninstall ' 'UID exhaustion recovery must not uninstall unrelated or target packages.'

	$uidPersistent = Invoke-TestCase 'uid-persistent' 1
	Assert-MatchCount $uidPersistent.Transcript ' install -r ' 2 'Persistent UID exhaustion must stop after one retry.'
	Assert-MatchCount $uidPersistent.Transcript '^-s test-device reboot$' 1 'Persistent UID exhaustion must reboot exactly once.'
	Assert-Contains $uidPersistent.Output 'UID exhaustion persisted after one device reboot' 'Persistent UID exhaustion must identify the Helix-owner action.'

	$updateIncompatible = Invoke-TestCase 'update-incompatible' 0
	Assert-MatchCount $updateIncompatible.Transcript ' install -r ' 2 'Incompatible signature recovery must retry installation exactly once.'
	Assert-MatchCount $updateIncompatible.Transcript '^-s test-device uninstall com\.xamarin\.mauir2r\.arm64\.baseline$' 1 'Incompatible signature recovery must uninstall only the selected package.'
	Assert-NotContains $updateIncompatible.Transcript ' reboot$' 'Incompatible signature recovery must not reboot the device.'

	$unknownFailure = Invoke-TestCase 'unknown-install-failure' 1
	Assert-MatchCount $unknownFailure.Transcript ' install -r ' 1 'Unknown install failures must not be retried.'
	Assert-NotContains $unknownFailure.Transcript ' uninstall ' 'Unknown install failures must not uninstall packages.'
	Assert-NotContains $unknownFailure.Transcript ' reboot$' 'Unknown install failures must not reboot the device.'
	Assert-Contains $unknownFailure.Output 'no retry was attempted' 'Unknown install failures must preserve explicit no-retry behavior.'

	$pidMissing = Invoke-TestCase 'pid-missing' 1
	Assert-Contains $pidMissing.Output 'did not remain running after launch' "Empty pidof output must report the smoke-test failure instead of a null-reference error. Output: $($pidMissing.Output -join ' | ')"
	Assert-NotContains $pidMissing.Output 'null-valued expression' 'Empty adb output must remain a non-null string.'

	$unicodeDiagnostics = Invoke-TestCase 'unicode-diagnostics' 0 $false
	$unicodeText = 'caf' + [string][char]0x00E9 + '-' + [string][char]0x6F22
	$unicodeArtifacts = @(
		'device-state-unicode-diagnostics-initial.log',
		'pm-path-command-unicode-diagnostics.log',
		'pm-path-unicode-diagnostics.log',
		'dumpsys-package-command-unicode-diagnostics.log',
		'dumpsys-package-unicode-diagnostics.log',
		'logcat-command-unicode-diagnostics.log',
		'logcat-unicode-diagnostics.log'
	)
	foreach ($artifact in $unicodeArtifacts) {
		$content = Get-Content -LiteralPath (Join-Path $unicodeDiagnostics.Directory $artifact) -Raw -Encoding UTF8
		Assert-Contains @($content) ([Regex]::Escape($unicodeText)) "ADB diagnostic artifact '$artifact' must preserve UTF-8 output."
	}

	$overallTimeout = Invoke-TestCase 'overall-timeout' 1 $true 0 1
	Assert-MatchCount $overallTimeout.Transcript '^version$' 1 'The end-to-end deadline must stop the hanging adb command without starting later operations.'
	Assert-Contains $overallTimeout.Output 'reached its 1-second end-to-end deadline' 'The end-to-end deadline must fail explicitly before the outer Helix timeout.'
	Assert-Equal 1 @(Get-ChildItem -LiteralPath $overallTimeout.Directory -Filter 'overall-deadline-overall-timeout.log').Count 'The command that consumes the end-to-end deadline must retain its diagnostic transcript.'

	Write-Host 'MAUI R2R Helix recovery tests passed.'
	$suitePassed = $true
} finally {
	Remove-Item Env:FAKE_ADB_SCENARIO -ErrorAction Ignore
	Remove-Item Env:FAKE_ADB_TRANSCRIPT -ErrorAction Ignore
	Remove-Item Env:HELIX_WORKITEM_UPLOAD_ROOT -ErrorAction Ignore
	Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction Ignore
}

if ($suitePassed) {
	exit 0
}
