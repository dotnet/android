param (
	[AllowNull()]
	[string] $DependencyCacheState = $env:GRADLE_DEPENDENCY_CACHE_RESTORED,
	[AllowNull()]
	[string] $DistributionCacheState = $env:GRADLE_DISTS_RESTORED,
	[string] $DependencyCachePath = $env:GRADLE_DEPENDENCY_CACHE_DIR,
	[string] $DistributionCachePath = $env:GRADLE_DISTS_DIR,
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

function Reset-FailedRestore ([string] $name, [AllowNull()] [string] $state, [string] $path, [string] $taskReferenceName) {
	if ($validCacheStates -contains $state) {
		Write-Host "$name cache state: $state"
		return
	}

	if ([string]::IsNullOrWhiteSpace($path)) {
		throw "$name cache path is not set."
	}

	Write-Host "$name cache restore did not complete; using an empty job-local cache."
	if (Test-Path -LiteralPath $path) {
		Remove-Item -LiteralPath $path -Recurse -Force
	}
	New-Item -ItemType Directory -Path $path -Force | Out-Null
	if (-not (Test-Path -LiteralPath $path -PathType Container) -or
			@(Get-ChildItem -LiteralPath $path -Force).Count -ne 0) {
		throw "$name cache path could not be reset."
	}
	if (-not $recoveredTasks.Contains($taskReferenceName)) {
		$recoveredTasks.Add($taskReferenceName)
	}
}

Reset-FailedRestore 'Gradle dependency' $DependencyCacheState $DependencyCachePath 'gradleDependenciesCache'
Reset-FailedRestore 'Gradle distribution' $DistributionCacheState $DistributionCachePath 'gradleDistributionsCache'

Write-Host "##vso[task.setvariable variable=RECOVERED_OPTIONAL_TASK_REFS]$($recoveredTasks -join ';')"
