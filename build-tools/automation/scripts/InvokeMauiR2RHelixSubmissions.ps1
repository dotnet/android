function Stop-MauiR2RHelixSubmission([Diagnostics.Process] $process) {
	try {
		if (-not $process.HasExited) {
			if ([Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT) {
				# Windows PowerShell's Process.Kill() cannot stop the worker's dotnet child.
				$startInfo = New-Object Diagnostics.ProcessStartInfo
				$startInfo.FileName = Join-Path $env:SystemRoot 'System32\taskkill.exe'
				$startInfo.Arguments = "/PID $($process.Id) /T /F"
				$startInfo.UseShellExecute = $false
				$killer = [Diagnostics.Process]::Start($startInfo)
				if (-not $killer) {
					throw "Could not start termination for MAUI R2R Helix worker $($process.Id)."
				}
				try {
					if (-not $killer.WaitForExit(10000)) {
						throw "Termination of MAUI R2R Helix worker $($process.Id) timed out."
					}
					if ($killer.ExitCode -ne 0 -and -not $process.HasExited) {
						throw "Termination of MAUI R2R Helix worker $($process.Id) failed with exit code $($killer.ExitCode)."
					}
				} finally {
					try {
						if (-not $killer.HasExited) {
							$killer.Kill()
						}
					} finally {
						try {
							if (-not $killer.WaitForExit(5000)) {
								throw "The termination command for worker $($process.Id) did not exit."
							}
						} finally {
							$killer.Dispose()
						}
					}
				}
			} else {
				$process.Kill($true)
			}
		}
	} catch [InvalidOperationException] {
		if (-not $process.HasExited) {
			throw
		}
	} finally {
		if (-not $process.WaitForExit(5000)) {
			throw "MAUI R2R Helix worker $($process.Id) did not exit after termination."
		}
	}
}

function Invoke-MauiR2RHelixSubmissions {
	[CmdletBinding()]
	param (
		[Parameter(Mandatory = $true)]
		[AllowEmptyCollection()]
		[object[]] $Submissions,
		[TimeSpan] $Timeout = [TimeSpan]::FromMinutes(60)
	)

	if ($Timeout -le [TimeSpan]::Zero) {
		throw 'The MAUI R2R Helix host submission timeout must be positive.'
	}
	$powerShellExe = (Get-Process -Id $PID).Path
	if (-not $powerShellExe) {
		throw 'Could not locate the current PowerShell executable.'
	}

	$started = New-Object 'Collections.Generic.List[object]'
	$cleanupErrors = New-Object 'Collections.Generic.List[string]'
	$failedNames = New-Object 'Collections.Generic.List[string]'
	$failure = $null
	$timer = [Diagnostics.Stopwatch]::StartNew()
	$lastProgress = [TimeSpan]::FromSeconds(-30)
	try {
		foreach ($submission in $Submissions) {
			if ($timer.Elapsed -ge $Timeout) {
				throw "MAUI R2R Helix host submissions exceeded $Timeout before launching $($submission.DisplayName)."
			}
			Write-Host "Starting MAUI R2R Helix submission: $($submission.DisplayName)"
			$process = Start-Process -FilePath $powerShellExe -ArgumentList @('-NoLogo', '-NoProfile', '-File', "`"$($submission.ScriptPath)`"") `
				-RedirectStandardOutput $submission.OutputPath -RedirectStandardError $submission.ErrorPath -PassThru
			$started.Add([pscustomobject]@{ Submission = $submission; Process = $process })
			# Cache the handle before waiting for redirected output on Windows PowerShell.
			$null = $process.Handle
		}

		while ($true) {
			$running = @($started | Where-Object { -not $_.Process.HasExited })
			if ($running.Count -eq 0) {
				break
			}
			if ($timer.Elapsed -ge $Timeout) {
				$runningNames = $running | ForEach-Object { $_.Submission.DisplayName }
				throw "MAUI R2R Helix host submissions exceeded $Timeout`: $($runningNames -join ', ')"
			}
			if ($timer.Elapsed - $lastProgress -ge [TimeSpan]::FromSeconds(30)) {
				$runningNames = $running | ForEach-Object { $_.Submission.DisplayName }
				Write-Host "Waiting for MAUI R2R Helix submissions: $($runningNames -join ', ')"
				$lastProgress = $timer.Elapsed
			}
			Start-Sleep -Milliseconds 100
		}
	} catch {
		$failure = $_
	} finally {
		foreach ($entry in $started) {
			$submission = $entry.Submission
			$process = $entry.Process
			try {
				Stop-MauiR2RHelixSubmission $process
			} catch {
				$cleanupErrors.Add("$($submission.DisplayName): $_")
			} finally {
				try {
					Write-Host "##[group]MAUI R2R Helix submission log - $($submission.DisplayName)"
					if (Test-Path -LiteralPath $submission.OutputPath) {
						Get-Content -LiteralPath $submission.OutputPath
					}
					if (Test-Path -LiteralPath $submission.ErrorPath) {
						Get-Content -LiteralPath $submission.ErrorPath
					}
					Write-Host '##[endgroup]'

					if ($process.HasExited) {
						$exitCode = $process.ExitCode
						if ($null -eq $exitCode) {
							throw "Could not determine the exit code for MAUI R2R Helix submission $($submission.DisplayName)."
						}
						if ($exitCode -ne 0) {
							Write-Host "##vso[task.logissue type=error]MAUI R2R Helix submission failed for $($submission.DisplayName) with exit code $exitCode."
							$failedNames.Add($submission.DisplayName)
						}
					}
				} catch {
					$cleanupErrors.Add("$($submission.DisplayName): $_")
				} finally {
					$process.Dispose()
				}
			}
		}

		if ($cleanupErrors.Count -ne 0) {
			$message = "MAUI R2R Helix submission cleanup failed: $($cleanupErrors -join '; ')"
			if ($failure) {
				$message = "$($failure.Exception.Message)`n$message"
			}
			throw $message
		}
	}
	if ($failure) {
		throw $failure
	}
	if ($failedNames.Count -ne 0) {
		throw "Failed MAUI R2R Helix submissions: $($failedNames -join ', ')"
	}
}
