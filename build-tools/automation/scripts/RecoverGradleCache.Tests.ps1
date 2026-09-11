$ErrorActionPreference = 'Stop'
$recoveryScript = Join-Path $PSScriptRoot 'RecoverGradleCache.ps1'
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) "xa-gradle-cache-tests-$([Guid]::NewGuid())"

function Assert-True ([bool] $condition, [string] $message) {
	if (-not $condition) {
		throw $message
	}
}

function New-CacheDirectory ([string] $name) {
	$path = Join-Path $tempRoot $name
	New-Item -ItemType Directory -Path $path -Force | Out-Null
	Set-Content -LiteralPath (Join-Path $path 'existing') -Value 'cached' -Encoding ASCII
	return $path
}

try {
	$dependencyCache = New-CacheDirectory 'valid-dependency'
	$distributionCache = New-CacheDirectory 'valid-distribution'
	$output = & $recoveryScript `
		-DependencyCacheState 'false' `
		-DistributionCacheState 'inexact' `
		-DependencyCachePath $dependencyCache `
		-DistributionCachePath $distributionCache 6>&1

	Assert-True (Test-Path (Join-Path $dependencyCache 'existing')) 'A valid dependency cache state was cleared.'
	Assert-True (Test-Path (Join-Path $distributionCache 'existing')) 'A valid distribution cache state was cleared.'
	Assert-True (($output -join "`n").Contains('variable=RECOVERED_OPTIONAL_TASK_REFS]')) 'The empty recovery marker was not emitted.'
	Assert-True (-not ($output -join "`n").Contains('gradleDependenciesCache')) 'A valid dependency cache was marked as recovered.'

	$dependencyCache = New-CacheDirectory 'unset-dependency'
	$distributionCache = New-CacheDirectory 'true-distribution'
	$env:GRADLE_DEPENDENCY_CACHE_RESTORED = $null
	$env:GRADLE_DISTS_RESTORED = 'true'
	$env:GRADLE_DEPENDENCY_CACHE_DIR = $dependencyCache
	$env:GRADLE_DISTS_DIR = $distributionCache
	$output = & $recoveryScript 6>&1

	Assert-True (-not (Test-Path (Join-Path $dependencyCache 'existing'))) 'An unset dependency cache state was not reset.'
	Assert-True (Test-Path (Join-Path $distributionCache 'existing')) 'A successful distribution cache was cleared.'
	Assert-True (($output -join "`n").Contains('variable=RECOVERED_OPTIONAL_TASK_REFS]gradleDependenciesCache')) 'The recovered dependency task was not recorded.'

	$dependencyCache = New-CacheDirectory 'unset-both-dependency'
	$distributionCache = New-CacheDirectory 'unset-both-distribution'
	$output = & $recoveryScript `
		-DependencyCacheState $null `
		-DistributionCacheState $null `
		-DependencyCachePath $dependencyCache `
		-DistributionCachePath $distributionCache 6>&1

	Assert-True (-not (Test-Path (Join-Path $dependencyCache 'existing'))) 'The failed dependency cache was not reset.'
	Assert-True (-not (Test-Path (Join-Path $distributionCache 'existing'))) 'The failed distribution cache was not reset.'
	Assert-True (($output -join "`n").Contains('variable=RECOVERED_OPTIONAL_TASK_REFS]gradleDependenciesCache;gradleDistributionsCache')) 'Both recovered cache tasks were not recorded.'

	$dependencyCache = New-CacheDirectory 'append-dependency'
	$distributionCache = New-CacheDirectory 'append-distribution'
	$output = & $recoveryScript `
		-DependencyCacheState $null `
		-DistributionCacheState 'true' `
		-DependencyCachePath $dependencyCache `
		-DistributionCachePath $distributionCache `
		-RecoveredOptionalTaskRefs 'androidArchivesCache' 6>&1
	Assert-True (($output -join "`n").Contains('variable=RECOVERED_OPTIONAL_TASK_REFS]androidArchivesCache;gradleDependenciesCache')) 'The existing recovered-task allowlist was not preserved.'

	$blockingFile = Join-Path $tempRoot 'blocking-file'
	Set-Content -LiteralPath $blockingFile -Value 'not a directory' -Encoding ASCII
	$recoveryFailed = $false
	try {
		& $recoveryScript `
			-DependencyCacheState $null `
			-DistributionCacheState 'true' `
			-DependencyCachePath (Join-Path $blockingFile 'cache') `
			-DistributionCachePath $distributionCache 6>&1 | Out-Null
	} catch {
		$recoveryFailed = $true
	}
	Assert-True $recoveryFailed 'A cache reset failure was incorrectly reported as recovered.'
} finally {
	Remove-Item Env:GRADLE_DEPENDENCY_CACHE_RESTORED -ErrorAction SilentlyContinue
	Remove-Item Env:GRADLE_DISTS_RESTORED -ErrorAction SilentlyContinue
	Remove-Item Env:GRADLE_DEPENDENCY_CACHE_DIR -ErrorAction SilentlyContinue
	Remove-Item Env:GRADLE_DISTS_DIR -ErrorAction SilentlyContinue
	Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}
