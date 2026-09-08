[CmdletBinding()]
param (
	[Parameter(Mandatory = $true)]
	[string] $Destination,
	[string] $AdbPath = 'adb',
	[int] $DeviceTimeoutSeconds = 10,
	[int] $LogcatTimeoutSeconds = 45,
	[int] $TerminationTimeoutSeconds = 2,
	[int] $OutputDrainTimeoutSeconds = 5
)

$ErrorActionPreference = 'Stop'

function Write-CaptureWarning {
	param (
		[Parameter(Mandatory = $true)]
		[string] $Message
	)

	$escapedMessage = $Message.Replace('%', '%AZP25').Replace("`r", '%0D').Replace("`n", '%0A')
	Write-Host "##vso[task.logissue type=warning]$escapedMessage"
}

function Read-ProcessOutput {
	param (
		[Parameter(Mandatory = $true)]
		[string] $Path
	)

	if (-not (Test-Path -LiteralPath $Path)) {
		return ''
	}

	$content = Get-Content -LiteralPath $Path -Raw
	if ($null -eq $content) {
		return ''
	}

	return $content.TrimEnd()
}

function Wait-ForOutputDrain {
	param (
		[Parameter(Mandatory = $true)]
		[Threading.Tasks.Task[]] $Tasks,
		[Parameter(Mandatory = $true)]
		[int] $TimeoutSeconds
	)

	$drainTask = [Threading.Tasks.Task]::WhenAll($Tasks)
	try {
		if (-not $drainTask.Wait([int] [TimeSpan]::FromSeconds($TimeoutSeconds).TotalMilliseconds)) {
			return [PSCustomObject] @{
				TimedOut = $true
				Error = ''
			}
		}
	} catch [AggregateException] {
		$errorMessage = ($_.Exception.Flatten().InnerExceptions | ForEach-Object { $_.Message }) -join '; '
		return [PSCustomObject] @{
			TimedOut = $false
			Error = $errorMessage
		}
	}

	return [PSCustomObject] @{
		TimedOut = $false
		Error = ''
	}
}

function Invoke-BoundedProcess {
	param (
		[Parameter(Mandatory = $true)]
		[string] $FilePath,
		[Parameter(Mandatory = $true)]
		[string[]] $Arguments,
		[Parameter(Mandatory = $true)]
		[string] $StandardOutputPath,
		[Parameter(Mandatory = $true)]
		[string] $StandardErrorPath,
		[Parameter(Mandatory = $true)]
		[int] $TimeoutSeconds,
		[Parameter(Mandatory = $true)]
		[int] $TerminationTimeoutSeconds,
		[Parameter(Mandatory = $true)]
		[int] $OutputDrainTimeoutSeconds
	)

	$process = $null
	$standardOutput = $null
	$standardError = $null
	try {
		$startInfo = [Diagnostics.ProcessStartInfo]::new()
		$startInfo.FileName = $FilePath
		$startInfo.UseShellExecute = $false
		$startInfo.CreateNoWindow = $true
		$startInfo.RedirectStandardOutput = $true
		$startInfo.RedirectStandardError = $true
		foreach ($argument in $Arguments) {
			$startInfo.ArgumentList.Add($argument)
		}

		$standardOutput = [IO.File]::Open($StandardOutputPath, [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::Read)
		$standardError = [IO.File]::Open($StandardErrorPath, [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::Read)
		$process = [Diagnostics.Process]::new()
		$process.StartInfo = $startInfo
		if (-not $process.Start()) {
			throw "Failed to start '$FilePath'."
		}

		$standardOutputTask = $process.StandardOutput.BaseStream.CopyToAsync($standardOutput)
		$standardErrorTask = $process.StandardError.BaseStream.CopyToAsync($standardError)

		$exited = $process.WaitForExit([int] [TimeSpan]::FromSeconds($TimeoutSeconds).TotalMilliseconds)
		$killError = ''
		$terminationTimedOut = $false
		if (-not $exited) {
			try {
				$process.Kill($true)
			} catch [InvalidOperationException] {
				# The process exited between the timeout and the kill request.
			} catch {
				$killError = $_.Exception.Message
			}

			$terminationTimedOut = -not $process.WaitForExit([int] [TimeSpan]::FromSeconds($TerminationTimeoutSeconds).TotalMilliseconds)
		}

		$outputDrain = Wait-ForOutputDrain `
			-Tasks @($standardOutputTask, $standardErrorTask) `
			-TimeoutSeconds $OutputDrainTimeoutSeconds
		return [PSCustomObject] @{
			ExitCode = if ($exited) { $process.ExitCode } else { $null }
			TimedOut = -not $exited
			TerminationTimedOut = $terminationTimedOut
			KillError = $killError
			OutputDrainTimedOut = $outputDrain.TimedOut
			OutputDrainError = $outputDrain.Error
		}
	} finally {
		if ($null -ne $standardOutput) {
			$standardOutput.Dispose()
		}
		if ($null -ne $standardError) {
			$standardError.Dispose()
		}
		if ($null -ne $process) {
			$process.Dispose()
		}
	}
}

function Get-FailureDetails {
	param (
		[string] $StandardError,
		[string] $KillError,
		[bool] $TerminationTimedOut,
		[bool] $OutputDrainTimedOut,
		[string] $OutputDrainError
	)

	$details = @()
	if (-not [string]::IsNullOrWhiteSpace($StandardError)) {
		$details += "stderr: $StandardError"
	}
	if (-not [string]::IsNullOrWhiteSpace($KillError)) {
		$details += "kill failed: $KillError"
	}
	if ($TerminationTimedOut) {
		$details += "process did not exit within $TerminationTimeoutSeconds seconds after termination"
	}
	if ($OutputDrainTimedOut) {
		$details += "output did not finish draining within $OutputDrainTimeoutSeconds seconds"
	}
	if (-not [string]::IsNullOrWhiteSpace($OutputDrainError)) {
		$details += "output drain failed: $OutputDrainError"
	}

	if ($details.Count -eq 0) {
		return ''
	}

	return '; ' + ($details -join '; ')
}

$temporaryPaths = @()

try {
	$devicesOutputPath = [IO.Path]::GetTempFileName()
	$devicesErrorPath = [IO.Path]::GetTempFileName()
	$logcatErrorPath = [IO.Path]::GetTempFileName()
	$temporaryPaths = @($devicesOutputPath, $devicesErrorPath, $logcatErrorPath)

	$destinationDirectory = Split-Path -Parent $Destination
	if ([string]::IsNullOrEmpty($destinationDirectory)) {
		$destinationDirectory = (Get-Location).Path
	}
	New-Item -ItemType Directory -Force -Path $destinationDirectory | Out-Null

	$devicesResult = Invoke-BoundedProcess `
		-FilePath $AdbPath `
		-Arguments @('devices') `
		-StandardOutputPath $devicesOutputPath `
		-StandardErrorPath $devicesErrorPath `
		-TimeoutSeconds $DeviceTimeoutSeconds `
		-TerminationTimeoutSeconds $TerminationTimeoutSeconds `
		-OutputDrainTimeoutSeconds $OutputDrainTimeoutSeconds
	$devicesOutput = Read-ProcessOutput -Path $devicesOutputPath
	$devicesError = Read-ProcessOutput -Path $devicesErrorPath

	if (-not [string]::IsNullOrWhiteSpace($devicesOutput)) {
		Write-Host $devicesOutput
	}
	if ($devicesResult.TimedOut) {
		$details = Get-FailureDetails -StandardError $devicesError -KillError $devicesResult.KillError -TerminationTimedOut $devicesResult.TerminationTimedOut -OutputDrainTimedOut $devicesResult.OutputDrainTimedOut -OutputDrainError $devicesResult.OutputDrainError
		Write-CaptureWarning "logcat capture skipped: adb devices timed out after $DeviceTimeoutSeconds seconds$details"
		exit 0
	}
	if ($devicesResult.ExitCode -ne 0) {
		$details = Get-FailureDetails -StandardError $devicesError -KillError '' -TerminationTimedOut $false -OutputDrainTimedOut $devicesResult.OutputDrainTimedOut -OutputDrainError $devicesResult.OutputDrainError
		Write-CaptureWarning "logcat capture skipped: adb devices exited with code $($devicesResult.ExitCode)$details"
		exit 0
	}
	if ($devicesResult.OutputDrainTimedOut -or -not [string]::IsNullOrWhiteSpace($devicesResult.OutputDrainError)) {
		$details = Get-FailureDetails -StandardError $devicesError -KillError '' -TerminationTimedOut $false -OutputDrainTimedOut $devicesResult.OutputDrainTimedOut -OutputDrainError $devicesResult.OutputDrainError
		Write-CaptureWarning "logcat capture skipped: adb devices output was incomplete$details"
		exit 0
	}
	if (-not [string]::IsNullOrWhiteSpace($devicesError)) {
		Write-Host $devicesError
	}

	$connectedDevice = $devicesOutput -split '\r?\n' | Where-Object { $_ -match '^\S+\s+device(?:\s|$)' } | Select-Object -First 1
	if ($null -eq $connectedDevice) {
		Write-Host 'logcat capture skipped: no connected device'
		exit 0
	}

	$logcatResult = Invoke-BoundedProcess `
		-FilePath $AdbPath `
		-Arguments @('logcat', '-d') `
		-StandardOutputPath $Destination `
		-StandardErrorPath $logcatErrorPath `
		-TimeoutSeconds $LogcatTimeoutSeconds `
		-TerminationTimeoutSeconds $TerminationTimeoutSeconds `
		-OutputDrainTimeoutSeconds $OutputDrainTimeoutSeconds
	$logcatError = Read-ProcessOutput -Path $logcatErrorPath

	if ($logcatResult.TimedOut) {
		$details = Get-FailureDetails -StandardError $logcatError -KillError $logcatResult.KillError -TerminationTimedOut $logcatResult.TerminationTimedOut -OutputDrainTimedOut $logcatResult.OutputDrainTimedOut -OutputDrainError $logcatResult.OutputDrainError
		Write-CaptureWarning "logcat capture timed out after $LogcatTimeoutSeconds seconds; partial output was retained at $Destination$details"
		exit 0
	}
	if ($logcatResult.ExitCode -ne 0) {
		$details = Get-FailureDetails -StandardError $logcatError -KillError '' -TerminationTimedOut $false -OutputDrainTimedOut $logcatResult.OutputDrainTimedOut -OutputDrainError $logcatResult.OutputDrainError
		Write-CaptureWarning "logcat capture exited with code $($logcatResult.ExitCode); partial output was retained at $Destination$details"
		exit 0
	}
	if ($logcatResult.OutputDrainTimedOut -or -not [string]::IsNullOrWhiteSpace($logcatResult.OutputDrainError)) {
		$details = Get-FailureDetails -StandardError $logcatError -KillError '' -TerminationTimedOut $false -OutputDrainTimedOut $logcatResult.OutputDrainTimedOut -OutputDrainError $logcatResult.OutputDrainError
		Write-CaptureWarning "logcat capture output was incomplete; partial output was retained at $Destination$details"
		exit 0
	}
	if (-not [string]::IsNullOrWhiteSpace($logcatError)) {
		Write-Host $logcatError
	}

	Write-Host "logcat capture completed: $Destination"
} catch {
	Write-CaptureWarning "logcat capture failed: $($_.Exception.Message)"
} finally {
	if ($temporaryPaths.Count -gt 0) {
		Remove-Item -LiteralPath $temporaryPaths -Force -ErrorAction Ignore
	}
}

exit 0
