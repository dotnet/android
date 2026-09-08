param (
	[AllowNull()]
	[string] $CacheState = $env:ANDROID_ARCHIVES_CACHE_RESTORED,
	[string] $CachePath = $env:ANDROID_ARCHIVES_DIR,
	[string] $RecoveredOptionalTaskRefs = $env:RECOVERED_OPTIONAL_TASK_REFS
)

$ErrorActionPreference = 'Stop'
$validCacheStates = @('true', 'inexact', 'false')
$recoveredTasks = [System.Collections.Generic.List[string]]::new()
foreach ($taskReferenceName in $RecoveredOptionalTaskRefs -split ';') {
	if (-not [string]::IsNullOrWhiteSpace($taskReferenceName)) {
		$recoveredTasks.Add($taskReferenceName)
	}
}

if ($validCacheStates -contains $CacheState) {
	Write-Host "Android archive cache state: $CacheState"
} else {
	if ([string]::IsNullOrWhiteSpace($CachePath)) {
		throw 'Android archive cache path is not set.'
	}

	Write-Host 'Android archive cache restore did not complete; using an empty cache directory for verified source downloads.'
	if (Test-Path -LiteralPath $CachePath) {
		Remove-Item -LiteralPath $CachePath -Recurse -Force
	}
	New-Item -ItemType Directory -Path $CachePath -Force | Out-Null
	if (-not (Test-Path -LiteralPath $CachePath -PathType Container) -or
			@(Get-ChildItem -LiteralPath $CachePath -Force).Count -ne 0) {
		throw 'Android archive cache path could not be reset.'
	}
	if (-not $recoveredTasks.Contains('androidArchivesCache')) {
		$recoveredTasks.Add('androidArchivesCache')
	}
}

Write-Host "##vso[task.setvariable variable=RECOVERED_OPTIONAL_TASK_REFS]$($recoveredTasks -join ';')"
