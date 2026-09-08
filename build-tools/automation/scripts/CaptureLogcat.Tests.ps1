$ErrorActionPreference = 'Stop'

$captureScript = Join-Path $PSScriptRoot 'CaptureLogcat.ps1'
$powerShellExe = (Get-Process -Id $PID).Path
if (-not $powerShellExe) {
	throw 'Could not determine the current PowerShell executable.'
}

function Assert-True {
	param (
		[bool] $Condition,
		[string] $Message
	)

	if (-not $Condition) {
		throw $Message
	}
}

function Invoke-CaptureTest {
	param (
		[string] $Name,
		[string] $DevicesMode,
		[string] $LogcatMode,
		[int] $DeviceTimeoutSeconds = 10,
		[int] $LogcatTimeoutSeconds = 10
	)

	$destination = Join-Path $testDirectory "$Name.txt"
	$env:FAKE_ADB_DEVICES_MODE = $DevicesMode
	$env:FAKE_ADB_LOGCAT_MODE = $LogcatMode
	$stopwatch = [Diagnostics.Stopwatch]::StartNew()
	$output = & $powerShellExe `
		-NoLogo `
		-NoProfile `
		-File $captureScript `
		-Destination $destination `
		-AdbPath $fakeAdb `
		-DeviceTimeoutSeconds $DeviceTimeoutSeconds `
		-LogcatTimeoutSeconds $LogcatTimeoutSeconds `
		-TerminationTimeoutSeconds 1 2>&1
	$exitCode = $LASTEXITCODE
	$stopwatch.Stop()

	return [PSCustomObject] @{
		Destination = $destination
		Elapsed = $stopwatch.Elapsed
		ExitCode = $exitCode
		Output = ($output | ForEach-Object { "$_" }) -join "`n"
	}
}

$testDirectory = Join-Path ([IO.Path]::GetTempPath()) "CaptureLogcat.Tests-$([IO.Path]::GetRandomFileName())"
$fakeAdbScript = Join-Path $testDirectory 'FakeAdb.ps1'
New-Item -ItemType Directory -Force -Path $testDirectory | Out-Null

try {
	@'
if ($args.Count -eq 0) {
	exit 64
}

switch ($args[0]) {
	'devices' {
		switch ($env:FAKE_ADB_DEVICES_MODE) {
			'device' {
				[Console]::Out.WriteLine('List of devices attached')
				[Console]::Out.WriteLine('emulator-5570 device product:sdk_gphone64_arm64')
				[Console]::Out.Flush()
				exit 0
			}
			'none' {
				[Console]::Out.WriteLine('List of devices attached')
				[Console]::Out.Flush()
				exit 0
			}
			'fail' {
				[Console]::Error.WriteLine('device probe failed')
				[Console]::Error.Flush()
				exit 17
			}
			'hang' {
				Start-Sleep -Seconds 30
				exit 0
			}
			default {
				exit 65
			}
		}
	}
	'logcat' {
		switch ($env:FAKE_ADB_LOGCAT_MODE) {
			'success' {
				[Console]::Out.WriteLine('complete log line')
				[Console]::Out.Flush()
				exit 0
			}
			'fail' {
				[Console]::Error.WriteLine('logcat failed')
				[Console]::Error.Flush()
				exit 23
			}
			'hang' {
				[Console]::Out.WriteLine('partial log line')
				[Console]::Out.Flush()
				Start-Sleep -Seconds 30
				exit 0
			}
			default {
				exit 66
			}
		}
	}
	default {
		exit 67
	}
}
'@ | Set-Content -LiteralPath $fakeAdbScript -Encoding ASCII

	if ([Environment]::OSVersion.Platform -eq 'Unix') {
		$fakeAdb = Join-Path $testDirectory 'adb'
		$wrapper = @'
#!/bin/sh
exec "__POWERSHELL__" -NoLogo -NoProfile -File "__SCRIPT__" "$@"
'@
		$wrapper = $wrapper.Replace('__POWERSHELL__', $powerShellExe.Replace('"', '\"')).Replace('__SCRIPT__', $fakeAdbScript.Replace('"', '\"'))
		$wrapper | Set-Content -LiteralPath $fakeAdb -Encoding ASCII
		& chmod +x $fakeAdb
		if ($LASTEXITCODE -ne 0) {
			throw 'Could not make the fake adb executable.'
		}
	} else {
		$fakeAdb = Join-Path $testDirectory 'adb.cmd'
		$wrapper = @'
@echo off
"__POWERSHELL__" -NoLogo -NoProfile -File "__SCRIPT__" %*
'@
		$wrapper = $wrapper.Replace('__POWERSHELL__', $powerShellExe).Replace('__SCRIPT__', $fakeAdbScript)
		$wrapper | Set-Content -LiteralPath $fakeAdb -Encoding ASCII
	}

	$result = Invoke-CaptureTest -Name 'success' -DevicesMode 'device' -LogcatMode 'success'
	Assert-True ($result.ExitCode -eq 0) "Successful capture exited with $($result.ExitCode)."
	Assert-True ($result.Output -match 'logcat capture completed') "Successful capture did not report completion: $($result.Output)"
	Assert-True ((Get-Content -LiteralPath $result.Destination -Raw) -match 'complete log line') 'Successful capture did not preserve logcat output.'

	$result = Invoke-CaptureTest -Name 'no-device' -DevicesMode 'none' -LogcatMode 'success'
	Assert-True ($result.ExitCode -eq 0) "No-device capture exited with $($result.ExitCode)."
	Assert-True ($result.Output -match 'logcat capture skipped: no connected device') "No-device capture did not report the skip: $($result.Output)"
	Assert-True (-not (Test-Path -LiteralPath $result.Destination)) 'No-device capture unexpectedly created a logcat file.'

	$result = Invoke-CaptureTest -Name 'probe-failure' -DevicesMode 'fail' -LogcatMode 'success'
	Assert-True ($result.ExitCode -eq 0) "Failed device probe exited with $($result.ExitCode)."
	Assert-True ($result.Output -match '##vso\[task.logissue type=warning\].*adb devices exited with code 17') "Failed device probe did not emit the expected warning: $($result.Output)"
	Assert-True ($result.Output -match 'device probe failed') "Failed device probe did not include stderr: $($result.Output)"

	$result = Invoke-CaptureTest -Name 'probe-timeout' -DevicesMode 'hang' -LogcatMode 'success' -DeviceTimeoutSeconds 1
	Assert-True ($result.ExitCode -eq 0) "Timed-out device probe exited with $($result.ExitCode)."
	Assert-True ($result.Elapsed.TotalSeconds -lt 15) "Timed-out device probe took $($result.Elapsed.TotalSeconds) seconds."
	Assert-True ($result.Output -match '##vso\[task.logissue type=warning\].*adb devices timed out after 1 seconds') "Timed-out device probe did not emit the expected warning: $($result.Output)"

	$result = Invoke-CaptureTest -Name 'logcat-failure' -DevicesMode 'device' -LogcatMode 'fail'
	Assert-True ($result.ExitCode -eq 0) "Failed logcat capture exited with $($result.ExitCode)."
	Assert-True ($result.Output -match '##vso\[task.logissue type=warning\].*logcat capture exited with code 23') "Failed logcat capture did not emit the expected warning: $($result.Output)"
	Assert-True ($result.Output -match 'logcat failed') "Failed logcat capture did not include stderr: $($result.Output)"

	$result = Invoke-CaptureTest -Name 'logcat-timeout' -DevicesMode 'device' -LogcatMode 'hang' -LogcatTimeoutSeconds 1
	Assert-True ($result.ExitCode -eq 0) "Timed-out logcat capture exited with $($result.ExitCode)."
	Assert-True ($result.Elapsed.TotalSeconds -lt 15) "Timed-out logcat capture took $($result.Elapsed.TotalSeconds) seconds."
	Assert-True ($result.Output -match '##vso\[task.logissue type=warning\].*logcat capture timed out after 1 seconds') "Timed-out logcat capture did not emit the expected warning: $($result.Output)"
	Assert-True ((Get-Content -LiteralPath $result.Destination -Raw) -match 'partial log line') 'Timed-out capture did not preserve partial logcat output.'

	Write-Host 'CaptureLogcat tests passed.'
} finally {
	Remove-Item Env:FAKE_ADB_DEVICES_MODE -ErrorAction Ignore
	Remove-Item Env:FAKE_ADB_LOGCAT_MODE -ErrorAction Ignore
	Remove-Item -LiteralPath $testDirectory -Recurse -Force -ErrorAction Ignore
}
