$ErrorActionPreference = 'Stop'
$recoveryScript = Join-Path $PSScriptRoot 'RecoverAndroidArchiveCache.ps1'
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) "xa-android-cache-tests-$([Guid]::NewGuid())"

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
	$cache = New-CacheDirectory 'valid'
	$output = & $recoveryScript -CacheState 'false' -CachePath $cache 6>&1
	Assert-True (Test-Path (Join-Path $cache 'existing')) 'A valid cache miss was cleared.'
	Assert-True (-not ($output -join "`n").Contains('androidArchivesCache')) 'A valid cache state was marked as recovered.'

	$cache = New-CacheDirectory 'unset'
	$env:ANDROID_ARCHIVES_CACHE_RESTORED = $null
	$env:ANDROID_ARCHIVES_DIR = $cache
	$output = & $recoveryScript 6>&1
	Assert-True (-not (Test-Path (Join-Path $cache 'existing'))) 'An unset cache state was not reset.'
	Assert-True (($output -join "`n").Contains('variable=RECOVERED_OPTIONAL_TASK_REFS]androidArchivesCache')) 'The recovered cache task was not recorded.'

	$cache = New-CacheDirectory 'append'
	$output = & $recoveryScript `
		-CacheState $null `
		-CachePath $cache `
		-RecoveredOptionalTaskRefs 'gradleDependenciesCache' 6>&1
	Assert-True (($output -join "`n").Contains('variable=RECOVERED_OPTIONAL_TASK_REFS]gradleDependenciesCache;androidArchivesCache')) 'The existing recovered-task allowlist was not preserved.'

	$blockingFile = Join-Path $tempRoot 'blocking-file'
	Set-Content -LiteralPath $blockingFile -Value 'not a directory' -Encoding ASCII
	$recoveryFailed = $false
	try {
		& $recoveryScript `
			-CacheState $null `
			-CachePath (Join-Path $blockingFile 'cache') 6>&1 | Out-Null
	} catch {
		$recoveryFailed = $true
	}
	Assert-True $recoveryFailed 'A cache reset failure was incorrectly reported as recovered.'
} finally {
	Remove-Item Env:ANDROID_ARCHIVES_CACHE_RESTORED -ErrorAction SilentlyContinue
	Remove-Item Env:ANDROID_ARCHIVES_DIR -ErrorAction SilentlyContinue
	Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}
