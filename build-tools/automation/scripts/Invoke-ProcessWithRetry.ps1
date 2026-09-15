param (
	[Parameter(Mandatory = $true)]
	[string] $FilePath,

	[string] $Arguments = "",

	[Parameter(Mandatory = $true)]
	[ValidateRange(1, 2147483)]
	[int] $TimeoutSeconds,

	[ValidateRange(0, 10)]
	[int] $RetryCount = 0,

	[ValidateRange(0, 300)]
	[int] $RetryDelaySeconds = 1
)

$ErrorActionPreference = 'Stop'

function Stop-ProcessTree {
	param (
		[Diagnostics.Process] $Process
	)

	if ($Process.HasExited) {
		return
	}

	if ($env:OS -eq 'Windows_NT') {
		& taskkill.exe /PID $Process.Id /T /F
		if ($LASTEXITCODE -ne 0 -and -not $Process.HasExited) {
			throw "Failed to stop process tree $($Process.Id)."
		}
	} else {
		$Process.Kill($true)
	}

	$Process.WaitForExit()
}

$attemptCount = $RetryCount + 1
for ($attempt = 1; $attempt -le $attemptCount; $attempt++) {
	Write-Host "Starting process attempt $attempt of $attemptCount with a $TimeoutSeconds-second timeout."

	$startInfo = [Diagnostics.ProcessStartInfo]::new()
	$startInfo.FileName = $FilePath
	$startInfo.Arguments = $Arguments
	$startInfo.UseShellExecute = $false

	$process = [Diagnostics.Process]::new()
	$process.StartInfo = $startInfo
	try {
		if (-not $process.Start()) {
			throw "Failed to start '$FilePath'."
		}

		if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
			Stop-ProcessTree $process
			if ($attempt -eq $attemptCount) {
				Write-Error "Process '$FilePath' timed out after $TimeoutSeconds seconds on attempt $attempt of $attemptCount." -ErrorAction Continue
				exit 124
			}

			Write-Warning "Process '$FilePath' timed out after $TimeoutSeconds seconds on attempt $attempt of $attemptCount; retrying."
		} else {
			$exitCode = $process.ExitCode
			if ($exitCode -eq 0) {
				exit 0
			}
			if ($attempt -eq $attemptCount) {
				exit $exitCode
			}

			Write-Warning "Process '$FilePath' exited with code $exitCode on attempt $attempt of $attemptCount; retrying."
		}
	} finally {
		$process.Dispose()
	}

	if ($RetryDelaySeconds -gt 0) {
		Start-Sleep -Seconds $RetryDelaySeconds
	}
}
