$ErrorActionPreference = 'Stop'
$restoreScript = Join-Path $PSScriptRoot 'RestoreMauiTemplate.ps1'
. $restoreScript -DefineOnly

function Assert-Equal
{
	param (
		[object] $Expected,
		[object] $Actual,
		[string] $Message
	)

	if (-not [object]::Equals($Expected, $Actual)) {
		throw "$Message Expected '$Expected', actual '$Actual'."
	}
}

function Assert-True
{
	param (
		[bool] $Condition,
		[string] $Message
	)

	if (-not $Condition) {
		throw $Message
	}
}

function New-CommandResult
{
	param (
		[int] $ExitCode,
		[string []] $Output
	)

	return [pscustomobject] @{
		ExitCode = $ExitCode
		Output = $Output
	}
}

function Invoke-TestRestore
{
	param (
		[object []] $Responses,
		[int] $MaxAttempts = 4
	)

	$responseQueue = [Collections.Generic.Queue[object]]::new()
	foreach ($response in $Responses) {
		$responseQueue.Enqueue($response)
	}
	$invocations = [Collections.Generic.List[object]]::new()
	$delays = [Collections.Generic.List[int]]::new()

	$commandInvoker = {
		param ([string []] $CommandArguments)

		$invocations.Add([string []] @($CommandArguments))
		if ($responseQueue.Count -eq 0) {
			throw "No command result was queued for '$($CommandArguments -join ' ')'."
		}
		return $responseQueue.Dequeue()
	}.GetNewClosure()
	$sleepAction = {
		param ([int] $Seconds)
		$delays.Add($Seconds)
	}.GetNewClosure()

	$exitCode = Invoke-MauiTemplateRestore `
		-DotNetPath 'dotnet' `
		-Project 'MauiTestProj.csproj' `
		-NuGetConfig 'NuGet.config' `
		-MaxAttempts $MaxAttempts `
		-InitialRetryDelaySeconds 1 `
		-MaxRetryDelaySeconds 2 `
		-CommandInvoker $commandInvoker `
		-SleepAction $sleepAction

	return [pscustomobject] @{
		ExitCode = $exitCode
		Invocations = $invocations
		Delays = $delays
	}
}

$success = Invoke-TestRestore -Responses @(
	(New-CommandResult -ExitCode 0 -Output @('Restore succeeded.'))
	(New-CommandResult -ExitCode 0 -Output @('Cleared NuGet HTTP cache.'))
)
Assert-Equal 0 $success.ExitCode 'An immediate restore success should succeed.'
Assert-Equal 2 $success.Invocations.Count 'An immediate restore success should clear stale metadata for later restore graphs.'
Assert-True ($success.Invocations[0] -contains '--no-http-cache') 'Restore must bypass the NuGet HTTP cache.'
Assert-True ($success.Invocations[0] -contains '--force-evaluate') 'Restore must force dependency reevaluation.'
Assert-Equal 'nuget locals http-cache --clear' ($success.Invocations[1] -join ' ') 'A successful restore should clear stale metadata for subsequent restores.'
Assert-Equal 0 $success.Delays.Count 'An immediate restore success should not wait.'

$retryableOutput = @(
	'MauiTestProj.csproj : error NU1102: Unable to find package Microsoft.Extensions.Logging with version (>= 11.0.0-rc.2.26453.114)'
	'MauiTestProj.csproj : error NU1102:   - Found 606 version(s) in dotnet11 [ Nearest version: 11.0.0-rc.2.26453.107 ]'
)
$recovered = Invoke-TestRestore -Responses @(
	(New-CommandResult -ExitCode 1 -Output $retryableOutput)
	(New-CommandResult -ExitCode 0 -Output @('Cleared NuGet HTTP cache.'))
	(New-CommandResult -ExitCode 0 -Output @('Restore succeeded.'))
)
Assert-Equal 0 $recovered.ExitCode 'A publication-skew restore should recover.'
Assert-Equal 3 $recovered.Invocations.Count 'A recovered restore should clear the cache and retry once.'
Assert-Equal 'nuget locals http-cache --clear' ($recovered.Invocations[1] -join ' ') 'The retry should clear only the NuGet HTTP cache.'
Assert-Equal 1 $recovered.Delays.Count 'A recovered restore should wait once.'

$unrelatedFailure = Invoke-TestRestore -Responses @(
	(New-CommandResult -ExitCode 1 -Output @('error NU1301: Unable to load the service index.'))
)
Assert-Equal 1 $unrelatedFailure.ExitCode 'An unrelated restore failure should fail.'
Assert-Equal 1 $unrelatedFailure.Invocations.Count 'An unrelated restore failure should not retry.'
Assert-Equal 0 $unrelatedFailure.Delays.Count 'An unrelated restore failure should not wait.'

$mixedFailure = Invoke-TestRestore -Responses @(
	(New-CommandResult -ExitCode 1 -Output ($retryableOutput + 'error NU1301: Unable to load the service index.'))
)
Assert-Equal 1 $mixedFailure.ExitCode 'A mixed restore failure should fail.'
Assert-Equal 1 $mixedFailure.Invocations.Count 'A mixed restore failure should not retry.'
Assert-Equal 0 $mixedFailure.Delays.Count 'A mixed restore failure should not wait.'

$differentPackageFailure = Invoke-TestRestore -Responses @(
	(New-CommandResult -ExitCode 1 -Output @(
		'MauiTestProj.csproj : error NU1102: Unable to find package Unrelated.Package with version (>= 11.0.0-rc.2.26453.114)'
		'MauiTestProj.csproj : error NU1102:   - Found 10 version(s) in dotnet11 [ Nearest version: 11.0.0-rc.2.26453.107 ]'
	))
)
Assert-Equal 1 $differentPackageFailure.ExitCode 'An unrelated missing package should fail.'
Assert-Equal 1 $differentPackageFailure.Invocations.Count 'An unrelated missing package should not retry.'

$partiallyRetryableFailure = Invoke-TestRestore -Responses @(
	(New-CommandResult -ExitCode 1 -Output ($retryableOutput + @(
		'MauiTestProj.csproj : error NU1102: Unable to find package Microsoft.Extensions.Logging.Abstractions with version (>= 11.0.0-rc.2.26453.114)'
		'MauiTestProj.csproj : error NU1102:   - Found 0 version(s) in dotnet11'
	)))
)
Assert-Equal 1 $partiallyRetryableFailure.ExitCode 'Every missing Logging package should require evidence of publication skew.'
Assert-Equal 1 $partiallyRetryableFailure.Invocations.Count 'A partially retryable failure should not retry.'

$persistentFailure = Invoke-TestRestore -MaxAttempts 3 -Responses @(
	(New-CommandResult -ExitCode 1 -Output $retryableOutput)
	(New-CommandResult -ExitCode 0 -Output @('Cleared NuGet HTTP cache.'))
	(New-CommandResult -ExitCode 1 -Output $retryableOutput)
	(New-CommandResult -ExitCode 0 -Output @('Cleared NuGet HTTP cache.'))
	(New-CommandResult -ExitCode 1 -Output $retryableOutput)
)
Assert-Equal 1 $persistentFailure.ExitCode 'A persistently missing version should remain a hard failure.'
Assert-Equal 5 $persistentFailure.Invocations.Count 'A persistent failure should stop after the bounded attempts.'
Assert-Equal 2 $persistentFailure.Delays.Count 'A persistent failure should wait only between attempts.'
Assert-Equal 1 $persistentFailure.Delays[0] 'The first retry should use the initial delay.'
Assert-Equal 2 $persistentFailure.Delays[1] 'The second retry should use the capped backoff.'

$cacheClearFailure = Invoke-TestRestore -Responses @(
	(New-CommandResult -ExitCode 1 -Output $retryableOutput)
	(New-CommandResult -ExitCode 7 -Output @('Cache clear failed.'))
)
Assert-Equal 7 $cacheClearFailure.ExitCode 'A cache-clear failure should be surfaced.'
Assert-Equal 2 $cacheClearFailure.Invocations.Count 'A cache-clear failure should stop before another restore.'
Assert-Equal 0 $cacheClearFailure.Delays.Count 'A cache-clear failure should not wait.'

Write-Host 'MAUI template restore retry tests passed.'
