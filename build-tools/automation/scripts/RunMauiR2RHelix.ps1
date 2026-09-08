param (
	[Parameter(Mandatory = $true)]
	[string] $AdbPath,
	[Parameter(Mandatory = $true)]
	[string] $ApkPath,
	[Parameter(Mandatory = $true)]
	[string] $PackageName,
	[Parameter(Mandatory = $true)]
	[string] $ScenarioName,
	[Parameter(Mandatory = $true)]
	[string] $ConfigurationName,
	[Parameter(Mandatory = $true)]
	[string] $RuntimeIdentifier,
	[int] $DeviceRecoveryTimeoutSeconds = 30,
	[int] $AndroidReadyTimeoutSeconds = 120,
	[ValidateRange(1, 540)]
	[int] $OverallTimeoutSeconds = 540,
	[int] $PollIntervalMilliseconds = 2000,
	[int] $RebootInitialDelayMilliseconds = 5000,
	[int] $LaunchWaitMilliseconds = 10000,
	[switch] $SkipExtendedDiagnostics
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$allowedPackages = @(
	'com.xamarin.mauir2r.arm64.baseline',
	'com.xamarin.mauir2r.arm64.fullr2r',
	'com.xamarin.mauir2r.arm.baseline',
	'com.xamarin.mauir2r.arm.fullr2r'
)

$powerShellExe = (Get-Process -Id $PID).Path
if (-not $powerShellExe) {
	$powerShellExe = 'powershell.exe'
}

$uploadDirectory = $env:HELIX_WORKITEM_UPLOAD_ROOT
if (-not $uploadDirectory) {
	$uploadDirectory = $PSScriptRoot
}

$selectedSerial = $null
$applicationInstalled = $false
$failureDiagnosticsCaptured = $false
$exitCode = 0
$workItemDeadline = [DateTime]::UtcNow.AddSeconds($OverallTimeoutSeconds)
$commandResultCounts = @{}

function ConvertTo-ProcessArgument ([string] $Argument)
{
	if ($Argument -notmatch '[\s"]') {
		return $Argument
	}

	return '"' + $Argument.Replace('"', '\"') + '"'
}

function Get-CombinedOutput ($Result)
{
	return (@($Result.StandardOutput, $Result.StandardError) |
		Where-Object { $_ } |
		ForEach-Object { $_.Trim() } |
		Where-Object { $_ }) -join [Environment]::NewLine
}

function Test-ContainsIgnoreCase ([string] $Value, [string] $Expected)
{
	return $Value.IndexOf($Expected, [StringComparison]::OrdinalIgnoreCase) -ge 0
}

function Get-RemainingWorkItemMilliseconds
{
	$remaining = [Math]::Floor(($workItemDeadline - [DateTime]::UtcNow).TotalMilliseconds)
	if ($remaining -le 0) {
		return 0
	}
	return [int][Math]::Min($remaining, [int]::MaxValue)
}

function Throw-WorkItemDeadlineExceeded ([string] $Operation)
{
	throw "MAUI R2R Helix work item reached its $OverallTimeoutSeconds-second end-to-end deadline while $Operation. No additional recovery was attempted, leaving time for final error handling before the 10-minute Helix timeout."
}

function Start-BoundedSleep ([int] $Milliseconds, [string] $Operation)
{
	if ($Milliseconds -le 0) {
		return
	}

	$remaining = Get-RemainingWorkItemMilliseconds
	if ($remaining -le 0) {
		Throw-WorkItemDeadlineExceeded $Operation
	}

	$sleepMilliseconds = [Math]::Min($Milliseconds, $remaining)
	Start-Sleep -Milliseconds $sleepMilliseconds
	if ($sleepMilliseconds -lt $Milliseconds) {
		Throw-WorkItemDeadlineExceeded $Operation
	}
}

function Write-CommandResult ([string] $Name, [string []] $Arguments, $Result)
{
	$safeName = $Name -replace '[^A-Za-z0-9_.-]', '-'
	$count = 1
	if ($commandResultCounts.ContainsKey($safeName)) {
		$count = $commandResultCounts[$safeName] + 1
	}
	$script:commandResultCounts[$safeName] = $count
	if ($count -gt 1) {
		$safeName = "$safeName-attempt-$count"
	}
	$path = Join-Path $uploadDirectory "$safeName.log"
	$content = @(
		"Command: adb $($Arguments -join ' ')",
		"ExitCode: $($Result.ExitCode)",
		"TimedOut: $($Result.TimedOut)",
		'',
		'===== stdout =====',
		$Result.StandardOutput,
		'===== stderr =====',
		$Result.StandardError
	) -join [Environment]::NewLine
	Set-Content -LiteralPath $path -Value $content -Encoding ASCII
}

function Invoke-Adb ([string []] $Arguments, [int] $TimeoutSeconds = 30, [switch] $Quiet)
{
	$stdoutPath = Join-Path ([IO.Path]::GetTempPath()) "$([IO.Path]::GetRandomFileName()).stdout"
	$stderrPath = Join-Path ([IO.Path]::GetTempPath()) "$([IO.Path]::GetRandomFileName()).stderr"
	$process = $null
	$timedOut = $false
	$deadlineLimited = $false

	if ($AdbPath.EndsWith('.ps1', [StringComparison]::OrdinalIgnoreCase)) {
		$filePath = $powerShellExe
		$processArguments = @('-NoLogo', '-NoProfile', '-File', $AdbPath) + $Arguments
	} else {
		$filePath = $AdbPath
		$processArguments = $Arguments
	}

	$argumentList = @($processArguments | ForEach-Object { ConvertTo-ProcessArgument $_ })
	if (-not $Quiet) {
		Write-Host "adb $($Arguments -join ' ')"
	}

	try {
		if ((Get-RemainingWorkItemMilliseconds) -le 0) {
			Throw-WorkItemDeadlineExceeded "starting 'adb $($Arguments -join ' ')'"
		}
		$process = Start-Process -FilePath $filePath -ArgumentList $argumentList -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -NoNewWindow -PassThru
		# Windows PowerShell requires the process handle to be cached before waiting when output is redirected.
		$null = $process.Handle
		$requestedTimeoutMilliseconds = [Math]::Max(1, $TimeoutSeconds * 1000)
		$remainingMilliseconds = Get-RemainingWorkItemMilliseconds
		$deadlineLimited = $remainingMilliseconds -lt $requestedTimeoutMilliseconds
		$timeoutMilliseconds = [Math]::Max(1, [Math]::Min($requestedTimeoutMilliseconds, $remainingMilliseconds))
		if (-not $process.WaitForExit($timeoutMilliseconds)) {
			$timedOut = $true
			try {
				$process.Kill()
				$process.WaitForExit()
			} catch {
				Write-Warning "Could not terminate timed-out adb process: $($_.Exception.Message)"
			}
		} else {
			$process.WaitForExit()
		}

		[string] $standardOutput = if (Test-Path $stdoutPath) { Get-Content -LiteralPath $stdoutPath -Raw } else { "" }
		[string] $standardError = if (Test-Path $stderrPath) { Get-Content -LiteralPath $stderrPath -Raw } else { "" }
		if ($null -eq $standardOutput) {
			$standardOutput = ""
		}
		if ($null -eq $standardError) {
			$standardError = ""
		}
		$commandExitCode = if ($timedOut) { -1 } else { $process.ExitCode }

		if (-not $Quiet) {
			if ($standardOutput) {
				Write-Host $standardOutput.TrimEnd()
			}
			if ($standardError) {
				Write-Host $standardError.TrimEnd()
			}
		}
		$result = [pscustomobject]@{
			ExitCode = $commandExitCode
			StandardOutput = $standardOutput
			StandardError = $standardError
			TimedOut = $timedOut
		}
		if ($timedOut -and $deadlineLimited) {
			Write-CommandResult "overall-deadline-$ConfigurationName" $Arguments $result
			Throw-WorkItemDeadlineExceeded "running 'adb $($Arguments -join ' ')'"
		}
		return $result
	} finally {
		Remove-Item -LiteralPath $stdoutPath, $stderrPath -Force -ErrorAction Ignore
		if ($process) {
			$process.Dispose()
		}
	}
}

function Get-AdbDeviceSnapshot ([string] $Name)
{
	$arguments = @('devices', '-l')
	$result = Invoke-Adb $arguments
	Write-CommandResult "adb-devices-$ConfigurationName-$Name" $arguments $result

	$devices = @()
	foreach ($line in ($result.StandardOutput -split '\r?\n')) {
		if (-not $line -or $line.StartsWith('List of devices attached', [StringComparison]::OrdinalIgnoreCase)) {
			continue
		}
		if ($line -match '^\s*(\S+)\s+(\S+)(?:\s+.*)?$') {
			$devices += [pscustomobject]@{
				Serial = $Matches[1]
				State = $Matches[2].ToLowerInvariant()
				Line = $line.Trim()
			}
		}
	}

	return [pscustomobject]@{
		Result = $result
		Devices = @($devices)
	}
}

function Select-HelixDevice ([string] $Name)
{
	$snapshot = Get-AdbDeviceSnapshot $Name
	$devices = @($snapshot.Devices)

	if ($snapshot.Result.ExitCode -ne 0) {
		return [pscustomobject]@{
			Ready = $false
			Recoverable = $true
			Serial = $null
			Reason = "adb devices failed with exit code $($snapshot.Result.ExitCode)"
		}
	}

	if ($devices.Count -eq 0) {
		return [pscustomobject]@{
			Ready = $false
			Recoverable = $true
			Serial = $null
			Reason = 'no attached Android device was detected'
		}
	}

	if ($devices.Count -ne 1) {
		return [pscustomobject]@{
			Ready = $false
			Recoverable = $false
			Serial = $null
			Reason = "expected exactly one Android device, but found $($devices.Count): $($devices.Line -join '; ')"
		}
	}

	$device = $devices[0]
	if ($device.State -ne 'device') {
		return [pscustomobject]@{
			Ready = $false
			Recoverable = $true
			Serial = $device.Serial
			Reason = "Android device '$($device.Serial)' is '$($device.State)', not authorized and online"
		}
	}

	return [pscustomobject]@{
		Ready = $true
		Recoverable = $false
		Serial = $device.Serial
		Reason = "Android device '$($device.Serial)' is ready"
	}
}

function Restart-AdbServer
{
	Write-Host 'Restarting the ADB server once to recover device enumeration.'
	$killArguments = @('kill-server')
	$killResult = Invoke-Adb $killArguments -TimeoutSeconds 15
	Write-CommandResult "adb-kill-server-$ConfigurationName" $killArguments $killResult

	$startArguments = @('start-server')
	$startResult = Invoke-Adb $startArguments -TimeoutSeconds 30
	Write-CommandResult "adb-start-server-$ConfigurationName" $startArguments $startResult
	if ($startResult.ExitCode -ne 0) {
		throw "MAUI R2R Helix infrastructure failure: adb start-server failed with exit code $($startResult.ExitCode)."
	}
}

function Wait-ForReadyDevice ([int] $TimeoutSeconds, [string] $Name, [string] $ExpectedSerial = "")
{
	$deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
	do {
		$selection = Select-HelixDevice $Name
		if ($selection.Ready) {
			if (-not $ExpectedSerial -or $selection.Serial -eq $ExpectedSerial) {
				return $selection
			}
			return [pscustomobject]@{
				Ready = $false
				Recoverable = $false
				Serial = $selection.Serial
				Reason = "expected Android device '$ExpectedSerial', but '$($selection.Serial)' was attached"
			}
		}
		if (-not $selection.Recoverable) {
			return $selection
		}
		if ([DateTime]::UtcNow -ge $deadline) {
			return $selection
		}
		Start-BoundedSleep $PollIntervalMilliseconds 'waiting for an authorized Android device'
	} while ($true)
}

function Resolve-HelixDevice
{
	$selection = Select-HelixDevice 'initial'
	if ($selection.Ready) {
		return $selection.Serial
	}
	if (-not $selection.Recoverable) {
		throw "MAUI R2R Helix infrastructure failure: $($selection.Reason)."
	}

	Write-Warning "$($selection.Reason); attempting one bounded ADB server recovery."
	Restart-AdbServer
	$selection = Wait-ForReadyDevice $DeviceRecoveryTimeoutSeconds 'post-adb-restart' $selection.Serial
	if (-not $selection.Ready) {
		throw "MAUI R2R Helix infrastructure failure after one ADB server restart: $($selection.Reason). The Helix device-lab owner must attach, authorize, or quarantine this device."
	}

	return $selection.Serial
}

function Wait-ForAndroidReady ([string] $Serial, [string] $Name)
{
	$deadline = [DateTime]::UtcNow.AddSeconds($AndroidReadyTimeoutSeconds)
	do {
		$bootArguments = @('-s', $Serial, 'shell', 'getprop', 'sys.boot_completed')
		$bootResult = Invoke-Adb $bootArguments -TimeoutSeconds 15 -Quiet
		$packageArguments = @('-s', $Serial, 'shell', 'pm', 'path', 'android')
		$packageResult = Invoke-Adb $packageArguments -TimeoutSeconds 20 -Quiet

		$bootCompleted = $bootResult.ExitCode -eq 0 -and $bootResult.StandardOutput.Trim() -eq '1'
		$packageManagerReady = $packageResult.ExitCode -eq 0 -and $packageResult.StandardOutput.Trim().StartsWith('package:', [StringComparison]::Ordinal)
		if ($bootCompleted -and $packageManagerReady) {
			Write-CommandResult "android-boot-$ConfigurationName-$Name" $bootArguments $bootResult
			Write-CommandResult "android-package-manager-$ConfigurationName-$Name" $packageArguments $packageResult
			return $true
		}

		if ([DateTime]::UtcNow -ge $deadline) {
			Write-CommandResult "android-boot-$ConfigurationName-$Name" $bootArguments $bootResult
			Write-CommandResult "android-package-manager-$ConfigurationName-$Name" $packageArguments $packageResult
			return $false
		}
		Start-BoundedSleep $PollIntervalMilliseconds 'waiting for Android and its package manager'
	} while ($true)
}

function Capture-DeviceDiagnostics ([string] $Serial, [string] $Name)
{
	Write-Host "Capturing Android device diagnostics: $Name"
	$snapshot = Get-AdbDeviceSnapshot "diagnostics-$Name"
	$onlineDevice = @($snapshot.Devices | Where-Object { $_.Serial -eq $Serial -and $_.State -eq 'device' })
	if ($onlineDevice.Count -ne 1) {
		Write-Warning "Skipping shell diagnostics because Android device '$Serial' is not online."
		return
	}
	if ($SkipExtendedDiagnostics) {
		return
	}

	$commands = @(
		@{ Title = 'getprop'; Arguments = @('-s', $Serial, 'shell', 'getprop') },
		@{ Title = 'df-data'; Arguments = @('-s', $Serial, 'shell', 'df', '/data') },
		@{ Title = 'df-emulated-storage'; Arguments = @('-s', $Serial, 'shell', 'df', '/storage/emulated/0') },
		@{ Title = 'dumpsys-diskstats'; Arguments = @('-s', $Serial, 'shell', 'dumpsys', 'diskstats') },
		@{ Title = 'dumpsys-storaged'; Arguments = @('-s', $Serial, 'shell', 'dumpsys', 'storaged') },
		@{ Title = 'third-party-packages'; Arguments = @('-s', $Serial, 'shell', 'pm', 'list', 'packages', '-3', '-U') },
		@{ Title = 'target-package-path'; Arguments = @('-s', $Serial, 'shell', 'pm', 'path', $PackageName) },
		@{ Title = 'target-package-details'; Arguments = @('-s', $Serial, 'shell', 'dumpsys', 'package', $PackageName) }
	)

	$content = @()
	foreach ($command in $commands) {
		$result = Invoke-Adb $command.Arguments -TimeoutSeconds 15 -Quiet
		$content += @(
			"===== $($command.Title) =====",
			"Command: adb $($command.Arguments -join ' ')",
			"ExitCode: $($result.ExitCode)",
			$Result.StandardOutput,
			$Result.StandardError,
			''
		)
	}

	$safeName = $Name -replace '[^A-Za-z0-9_.-]', '-'
	Set-Content -LiteralPath (Join-Path $uploadDirectory "device-state-$ConfigurationName-$safeName.log") -Value ($content -join [Environment]::NewLine) -Encoding ASCII
}

function Restart-AndroidDevice ([string] $Serial)
{
	Write-Warning "Rebooting Android device '$Serial' once to recover package UID allocation."
	$arguments = @('-s', $Serial, 'reboot')
	$result = Invoke-Adb $arguments -TimeoutSeconds 30
	Write-CommandResult "adb-reboot-$ConfigurationName" $arguments $result
	if ($result.ExitCode -ne 0) {
		throw "MAUI R2R Helix infrastructure failure: adb reboot failed with exit code $($result.ExitCode)."
	}

	Start-BoundedSleep $RebootInitialDelayMilliseconds 'waiting for the recovery reboot to begin'
	$selection = Wait-ForReadyDevice $AndroidReadyTimeoutSeconds 'post-device-reboot' $Serial
	if (-not $selection.Ready) {
		throw "MAUI R2R Helix infrastructure failure: Android device '$Serial' did not reconnect after its one recovery reboot. The Helix device-lab owner must repair or quarantine it."
	}
	if (-not (Wait-ForAndroidReady $Serial 'post-device-reboot')) {
		throw "MAUI R2R Helix infrastructure failure: Android device '$Serial' did not finish booting with a responsive package manager after its one recovery reboot."
	}
}

function Install-MauiR2RApk ([string] $Serial)
{
	$updateRecoveryUsed = $false
	$uidRecoveryUsed = $false
	$attempt = 0

	while ($true) {
		$attempt++
		$arguments = @('-s', $Serial, 'install', '-r', $ApkPath)
		$result = Invoke-Adb $arguments -TimeoutSeconds 180
		Write-CommandResult "install-$ConfigurationName-attempt-$attempt" $arguments $result
		if ($result.ExitCode -eq 0) {
			return
		}

		$output = Get-CombinedOutput $result
		$isUpdateSignatureFailure = Test-ContainsIgnoreCase $output 'INSTALL_FAILED_UPDATE_INCOMPATIBLE'
		$isUidExhaustion = (Test-ContainsIgnoreCase $output 'INSTALL_FAILED_INSUFFICIENT_STORAGE') -and
			(Test-ContainsIgnoreCase $output 'could not be assigned a valid UID')

		if ($isUpdateSignatureFailure -and -not $updateRecoveryUsed) {
			$updateRecoveryUsed = $true
			Write-Warning "The existing MAUI R2R package has an incompatible signature; uninstalling only '$PackageName' before one install retry."
			$uninstallArguments = @('-s', $Serial, 'uninstall', $PackageName)
			$uninstallResult = Invoke-Adb $uninstallArguments -TimeoutSeconds 60
			Write-CommandResult "uninstall-$ConfigurationName-update-signature" $uninstallArguments $uninstallResult
			if ($uninstallResult.ExitCode -ne 0) {
				throw "APK replacement failed with an incompatible signature, and cleanup of the known package '$PackageName' failed with exit code $($uninstallResult.ExitCode)."
			}
			continue
		}

		if ($isUidExhaustion -and -not $uidRecoveryUsed) {
			$uidRecoveryUsed = $true
			Capture-DeviceDiagnostics $Serial 'uid-exhaustion-before-reboot'
			Restart-AndroidDevice $Serial
			Capture-DeviceDiagnostics $Serial 'uid-exhaustion-after-reboot'
			continue
		}

		if ($isUpdateSignatureFailure) {
			Capture-DeviceDiagnostics $Serial 'update-signature-failure'
			$script:failureDiagnosticsCaptured = $true
			throw "APK replacement still failed with an incompatible signature after uninstalling only '$PackageName' and retrying once. Last install output: $output"
		}

		if ($isUidExhaustion) {
			$script:failureDiagnosticsCaptured = $true
			throw "MAUI R2R Helix infrastructure failure: Android package UID exhaustion persisted after one device reboot. The Helix device-lab owner must reprovision or quarantine this device. Last install output: $output"
		}

		Capture-DeviceDiagnostics $Serial 'install-failure'
		$script:failureDiagnosticsCaptured = $true
		if ($result.TimedOut) {
			throw "APK installation timed out; no retry was attempted because the failure did not match a recognized infrastructure recovery signature."
		}
		throw "APK installation failed without a recognized infrastructure recovery signature; no retry was attempted. Last install output: $output"
	}
}

function Reset-MauiR2RPackage ([string] $Serial)
{
	$forceStopArguments = @('-s', $Serial, 'shell', 'am', 'force-stop', $PackageName)
	$forceStopResult = Invoke-Adb $forceStopArguments -TimeoutSeconds 30
	Write-CommandResult "force-stop-$ConfigurationName-before-launch" $forceStopArguments $forceStopResult
	if ($forceStopResult.ExitCode -ne 0) {
		throw "Could not force-stop the installed MAUI R2R package '$PackageName'."
	}

	$clearArguments = @('-s', $Serial, 'shell', 'pm', 'clear', $PackageName)
	$clearResult = Invoke-Adb $clearArguments -TimeoutSeconds 60
	Write-CommandResult "clear-package-$ConfigurationName-before-launch" $clearArguments $clearResult
	if ($clearResult.ExitCode -ne 0) {
		throw "Could not clear the installed MAUI R2R package '$PackageName' before launch."
	}
	if (-not (Test-ContainsIgnoreCase $clearResult.StandardOutput 'Success')) {
		Write-Warning "Package data clear returned exit code 0 without the usual 'Success' output."
	}
}

function Verify-MauiR2RPackage ([string] $Serial)
{
	$pathArguments = @('-s', $Serial, 'shell', 'pm', 'path', $PackageName)
	$pathResult = Invoke-Adb $pathArguments -TimeoutSeconds 30
	Write-CommandResult "pm-path-command-$ConfigurationName" $pathArguments $pathResult
	Set-Content -LiteralPath (Join-Path $uploadDirectory "pm-path-$ConfigurationName.log") -Value $pathResult.StandardOutput -Encoding ASCII
	if ($pathResult.ExitCode -ne 0 -or -not $pathResult.StandardOutput.Trim().StartsWith('package:', [StringComparison]::Ordinal)) {
		throw "The MAUI R2R package '$PackageName' was not present after installation."
	}

	$dumpsysArguments = @('-s', $Serial, 'shell', 'dumpsys', 'package', $PackageName)
	$dumpsysResult = Invoke-Adb $dumpsysArguments -TimeoutSeconds 30 -Quiet
	Write-CommandResult "dumpsys-package-command-$ConfigurationName" $dumpsysArguments $dumpsysResult
	Set-Content -LiteralPath (Join-Path $uploadDirectory "dumpsys-package-$ConfigurationName.log") -Value $dumpsysResult.StandardOutput -Encoding ASCII
	if ($dumpsysResult.ExitCode -ne 0) {
		Write-Warning "Could not capture dumpsys details for installed package '$PackageName'."
	}
}

function Stop-MauiR2RPackage ([string] $Serial)
{
	$forceStopArguments = @('-s', $Serial, 'shell', 'am', 'force-stop', $PackageName)
	$forceStopResult = Invoke-Adb $forceStopArguments -TimeoutSeconds 30 -Quiet
	Write-CommandResult "force-stop-$ConfigurationName-after-run" $forceStopArguments $forceStopResult

	$clearArguments = @('-s', $Serial, 'shell', 'pm', 'clear', $PackageName)
	$clearResult = Invoke-Adb $clearArguments -TimeoutSeconds 60 -Quiet
	Write-CommandResult "clear-package-$ConfigurationName-after-run" $clearArguments $clearResult

	if ($forceStopResult.ExitCode -ne 0 -or $clearResult.ExitCode -ne 0) {
		Write-Warning "Best-effort package cleanup failed for '$PackageName'; the package was intentionally not uninstalled to avoid Android UID churn."
	}
}

try {
	if (-not (Test-Path -LiteralPath $AdbPath)) {
		throw "Could not find adb at '$AdbPath'."
	}
	if (-not (Test-Path -LiteralPath $ApkPath)) {
		throw "Could not find the MAUI R2R APK at '$ApkPath'."
	}
	if ($PackageName -notin $allowedPackages) {
		throw "Refusing to operate on unexpected package '$PackageName'."
	}

	New-Item -ItemType Directory -Force -Path $uploadDirectory | Out-Null
	$scenarioPath = Join-Path $uploadDirectory "scenario-$ConfigurationName.txt"
	@(
		"Scenario: $ScenarioName",
		"Configuration: $ConfigurationName",
		"RuntimeIdentifier: $RuntimeIdentifier",
		"PackageName: $PackageName",
		"APK: $ApkPath",
		"OverallTimeoutSeconds: $OverallTimeoutSeconds",
		"Machine: $env:COMPUTERNAME"
	) | Set-Content -LiteralPath $scenarioPath -Encoding ASCII

	$versionArguments = @('version')
	$versionResult = Invoke-Adb $versionArguments
	Write-CommandResult "adb-version-$ConfigurationName" $versionArguments $versionResult
	if ($versionResult.ExitCode -ne 0) {
		throw "adb version failed with exit code $($versionResult.ExitCode)."
	}

	$selectedSerial = Resolve-HelixDevice
	Write-Host "Using Android device '$selectedSerial'."
	if (-not (Wait-ForAndroidReady $selectedSerial 'initial')) {
		throw "MAUI R2R Helix infrastructure failure: Android device '$selectedSerial' did not report boot completion with a responsive package manager within $AndroidReadyTimeoutSeconds seconds."
	}

	Capture-DeviceDiagnostics $selectedSerial 'initial'
	Install-MauiR2RApk $selectedSerial
	$applicationInstalled = $true
	Reset-MauiR2RPackage $selectedSerial
	Verify-MauiR2RPackage $selectedSerial

	$clearLogcatArguments = @('-s', $selectedSerial, 'logcat', '-c')
	$clearLogcatResult = Invoke-Adb $clearLogcatArguments -TimeoutSeconds 30
	Write-CommandResult "logcat-clear-$ConfigurationName" $clearLogcatArguments $clearLogcatResult

	$launchArguments = @('-s', $selectedSerial, 'shell', 'monkey', '-p', $PackageName, '-c', 'android.intent.category.LAUNCHER', '1')
	$launchResult = Invoke-Adb $launchArguments -TimeoutSeconds 60
	Write-CommandResult "launch-$ConfigurationName" $launchArguments $launchResult
	if ($launchResult.ExitCode -ne 0) {
		throw "Launching the MAUI R2R APK failed with exit code $($launchResult.ExitCode)."
	}

	Start-BoundedSleep $LaunchWaitMilliseconds 'waiting for the launched MAUI R2R application'
	$pidArguments = @('-s', $selectedSerial, 'shell', 'pidof', $PackageName)
	$pidResult = Invoke-Adb $pidArguments -TimeoutSeconds 30
	Write-CommandResult "pidof-$ConfigurationName" $pidArguments $pidResult

	$logcatArguments = @('-s', $selectedSerial, 'logcat', '-d', '-b', 'all')
	$logcatResult = Invoke-Adb $logcatArguments -TimeoutSeconds 60 -Quiet
	Write-CommandResult "logcat-command-$ConfigurationName" $logcatArguments $logcatResult
	Set-Content -LiteralPath (Join-Path $uploadDirectory "logcat-$ConfigurationName.log") -Value $logcatResult.StandardOutput -Encoding ASCII

	if ($pidResult.ExitCode -ne 0 -or -not $pidResult.StandardOutput.Trim()) {
		throw "The MAUI R2R APK did not remain running after launch."
	}

	Write-Host "MAUI R2R smoke test passed for $ScenarioName."
} catch {
	$exitCode = 1
	Write-Error $_.Exception.Message -ErrorAction Continue
	if ($selectedSerial -and -not $failureDiagnosticsCaptured) {
		try {
			Capture-DeviceDiagnostics $selectedSerial 'final-failure'
		} catch {
			Write-Warning "Could not capture final Android diagnostics: $($_.Exception.Message)"
		}
	}
} finally {
	if ($applicationInstalled -and $selectedSerial) {
		try {
			Stop-MauiR2RPackage $selectedSerial
		} catch {
			Write-Warning "Could not complete best-effort MAUI R2R package cleanup: $($_.Exception.Message)"
		}
	}
}

exit $exitCode
