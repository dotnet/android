[CmdletBinding()]
param (
	[string] $DotNetPath,
	[string] $Project,
	[string] $NuGetConfig,
	[string] $BinaryLogPath,
	[int] $MaxAttempts = 4,
	[int] $InitialRetryDelaySeconds = 15,
	[int] $MaxRetryDelaySeconds = 60,
	[switch] $DefineOnly
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

function Test-RetryableMauiRestoreFailure
{
	param (
		[Parameter(Mandatory)]
		[string] $Output
	)

	$errorLines = @($Output -split '\r?\n' | Where-Object { $_ -match '(?i)\berror\b' })
	if ($errorLines.Count -eq 0) {
		return $false
	}
	if ($errorLines | Where-Object { $_ -notmatch '(?i)error NU1102:' }) {
		return $false
	}

	$missingPackageCount = 0
	$currentPackageHasOlderDotNet11Version = $false
	foreach ($line in $Output -split '\r?\n') {
		if ($line -match '(?i)error NU1102: Unable to find package') {
			if ($missingPackageCount -gt 0 -and -not $currentPackageHasOlderDotNet11Version) {
				return $false
			}
			if ($line -notmatch '(?i)error NU1102: Unable to find package Microsoft\.Extensions\.Logging(?:\.[A-Za-z0-9.-]+)? with version \(>= \d+\.\d+\.\d+-(preview|rc|alpha|beta)(?:\.[0-9A-Za-z-]+)+\)\s*$') {
				return $false
			}

			$missingPackageCount++
			$currentPackageHasOlderDotNet11Version = $false
			continue
		}
		if ($missingPackageCount -gt 0 -and $line -match '(?i)Found \d+ version\(s\) in dotnet11 \[ Nearest version: \d+\.\d+\.\d+[^]]* \]') {
			$currentPackageHasOlderDotNet11Version = $true
		}
	}

	return $missingPackageCount -gt 0 -and $currentPackageHasOlderDotNet11Version
}

function Clear-NuGetHttpCache
{
	param (
		[Parameter(Mandatory)]
		[scriptblock] $CommandInvoker
	)

	$clearResult = & $CommandInvoker @('nuget', 'locals', 'http-cache', '--clear')
	@($clearResult.Output) | ForEach-Object { Write-Host "$_" }
	return [int] $clearResult.ExitCode
}

function Invoke-MauiTemplateRestore
{
	param (
		[Parameter(Mandatory)]
		[string] $DotNetPath,
		[Parameter(Mandatory)]
		[string] $Project,
		[Parameter(Mandatory)]
		[string] $NuGetConfig,
		[string] $BinaryLogPath,
		[int] $MaxAttempts = 4,
		[int] $InitialRetryDelaySeconds = 15,
		[int] $MaxRetryDelaySeconds = 60,
		[scriptblock] $CommandInvoker,
		[scriptblock] $SleepAction
	)

	if ($MaxAttempts -lt 1) {
		throw "MaxAttempts must be at least 1."
	}
	if ($InitialRetryDelaySeconds -lt 0) {
		throw "InitialRetryDelaySeconds cannot be negative."
	}
	if ($MaxRetryDelaySeconds -lt $InitialRetryDelaySeconds) {
		throw "MaxRetryDelaySeconds cannot be less than InitialRetryDelaySeconds."
	}

	if ($null -eq $CommandInvoker) {
		$CommandInvoker = {
			param ([string []] $CommandArguments)

			$output = @(& $DotNetPath @CommandArguments 2>&1)
			return [pscustomobject] @{
				ExitCode = $LASTEXITCODE
				Output = $output
			}
		}.GetNewClosure()
	}
	if ($null -eq $SleepAction) {
		$SleepAction = {
			param ([int] $Seconds)
			Start-Sleep -Seconds $Seconds
		}
	}

	$restoreArguments = @(
		'restore'
		$Project
		'--configfile'
		$NuGetConfig
		'--no-http-cache'
		'--force-evaluate'
	)

	$lastExitCode = 1
	$httpCacheCleared = $false
	for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
		$attemptArguments = @($restoreArguments)
		if ($BinaryLogPath) {
			$binaryLogDirectory = Split-Path -Parent $BinaryLogPath
			if (-not $binaryLogDirectory) {
				$binaryLogDirectory = (Get-Location).Path
			}
			if ($binaryLogDirectory -and -not (Test-Path -LiteralPath $binaryLogDirectory)) {
				New-Item -ItemType Directory -Path $binaryLogDirectory -Force | Out-Null
			}

			$extension = [IO.Path]::GetExtension($BinaryLogPath)
			$fileName = [IO.Path]::GetFileNameWithoutExtension($BinaryLogPath)
			$attemptBinaryLog = Join-Path $binaryLogDirectory "$fileName-attempt-$attempt$extension"
			$attemptArguments += "-bl:$attemptBinaryLog"
		}

		Write-Host "Restoring MAUI template dependencies (attempt $attempt of $MaxAttempts)..."
		$result = & $CommandInvoker $attemptArguments
		$output = @($result.Output)
		$output | ForEach-Object { Write-Host "$_" }
		$lastExitCode = [int] $result.ExitCode
		if ($lastExitCode -eq 0) {
			if (-not $httpCacheCleared) {
				Write-Host "Clearing NuGet's HTTP cache so subsequent MAUI restore graphs cannot reuse stale package metadata."
				$clearExitCode = Clear-NuGetHttpCache -CommandInvoker $CommandInvoker
				if ($clearExitCode -ne 0) {
					Write-Host "NuGet HTTP cache clearing failed with exit code $clearExitCode."
					return $clearExitCode
				}
			}
			return 0
		}

		$outputText = $output -join [Environment]::NewLine
		if (-not (Test-RetryableMauiRestoreFailure -Output $outputText) -or $attempt -eq $MaxAttempts) {
			return $lastExitCode
		}
		Write-Host "The dotnet11 feed has an older version than the requested prerelease package. Clearing NuGet's HTTP cache before retrying."
		$clearExitCode = Clear-NuGetHttpCache -CommandInvoker $CommandInvoker
		if ($clearExitCode -ne 0) {
			Write-Host "NuGet HTTP cache clearing failed with exit code $clearExitCode."
			return $clearExitCode
		}
		$httpCacheCleared = $true

		$retryDelaySeconds = [Math]::Min(
			$MaxRetryDelaySeconds,
			$InitialRetryDelaySeconds * [Math]::Pow(2, $attempt - 1)
		)
		Write-Host "Retrying MAUI template restore after $retryDelaySeconds seconds..."
		& $SleepAction ([int] $retryDelaySeconds)
	}

	return $lastExitCode
}

if (-not $DefineOnly) {
	$exitCode = Invoke-MauiTemplateRestore `
		-DotNetPath $DotNetPath `
		-Project $Project `
		-NuGetConfig $NuGetConfig `
		-BinaryLogPath $BinaryLogPath `
		-MaxAttempts $MaxAttempts `
		-InitialRetryDelaySeconds $InitialRetryDelaySeconds `
		-MaxRetryDelaySeconds $MaxRetryDelaySeconds
	if ($exitCode -ne 0) {
		throw "MAUI template restore failed with exit code $exitCode."
	}
}
