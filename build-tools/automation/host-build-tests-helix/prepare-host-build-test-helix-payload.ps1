[CmdletBinding()]
param (
	[Parameter(Mandatory)]
	[ValidateSet('windows', 'linux')]
	[string] $Platform,

	[Parameter(Mandatory)]
	[string] $RepositoryRoot,

	[Parameter(Mandatory)]
	[string] $CorrelationPayloadDirectory,

	[Parameter(Mandatory)]
	[string] $WorkItemsDirectory,

	[Parameter(Mandatory)]
	[string] $WorkItemsPropsPath,

	[Parameter(Mandatory)]
	[string] $ResultsDirectory,

	[Parameter(Mandatory)]
	[string] $TestAssemblyRelativePath,

	[Parameter(Mandatory)]
	[string] $TestSlicerPath,

	[string] $TestFilter = '',

	[string] $BalanceFile = '',

	[ValidateRange(1, 1440)]
	[int] $TargetMinutes = 15,

	[ValidateRange(0.1, 128)]
	[double] $DurationParallelism = 2.5,

	[ValidateRange(1, 86400)]
	[int] $FallbackTestDurationSeconds = 60,

	[string] $NUnitWorkers = '-1',

	[string] $Configuration = 'Release',

	[Parameter(Mandatory)]
	[string] $AndroidHome,

	[Parameter(Mandatory)]
	[string] $JavaHome,

	[string] $NuGetPackages = '',

	[string] $DotNetToolsDirectory = '',

	[string] $GradleUserHome = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'HostBuildTestsHelix.psm1') -Force

function ConvertTo-NativeRelativePath
{
	param (
		[Parameter(Mandatory)]
		[string] $Path
	)

	$separator = [string] [IO.Path]::DirectorySeparatorChar
	return $Path.Replace('\', $separator).Replace('/', $separator)
}

function Copy-PayloadDirectory
{
	param (
		[Parameter(Mandatory)]
		[string] $Source,

		[Parameter(Mandatory)]
		[string] $Destination,

		[switch] $Optional
	)

	if (-not (Test-Path -LiteralPath $Source -PathType Container)) {
		if ($Optional) {
			Write-Warning "Optional payload directory '$Source' does not exist."
			return
		}
		throw "Required payload directory '$Source' does not exist."
	}

	Write-Host "Staging payload directory '$Source'."
	New-Item -ItemType Directory -Force -Path $Destination | Out-Null
	if ($IsWindows) {
		& robocopy $Source $Destination /E /COPY:DAT /DCOPY:DAT /R:2 /W:1 /NFL /NDL /NJH /NJS /NP
		if ($LASTEXITCODE -gt 7) {
			throw "robocopy failed with exit code $LASTEXITCODE while copying '$Source'."
		}
	} else {
		& cp -a "$Source/." "$Destination/"
		if ($LASTEXITCODE -ne 0) {
			throw "cp failed with exit code $LASTEXITCODE while copying '$Source'."
		}
	}
}

$repositoryRootFullPath = [IO.Path]::GetFullPath($RepositoryRoot)
$correlationPayloadFullPath = [IO.Path]::GetFullPath($CorrelationPayloadDirectory)
$workItemsFullPath = [IO.Path]::GetFullPath($WorkItemsDirectory)
$workItemsPropsFullPath = [IO.Path]::GetFullPath($WorkItemsPropsPath)
$resultsFullPath = [IO.Path]::GetFullPath($ResultsDirectory)

Remove-Item -LiteralPath $correlationPayloadFullPath -Recurse -Force -ErrorAction Ignore
Remove-Item -LiteralPath $workItemsFullPath -Recurse -Force -ErrorAction Ignore
New-Item -ItemType Directory -Force -Path $correlationPayloadFullPath, $workItemsFullPath, $resultsFullPath | Out-Null

$payloadRepository = Join-Path $correlationPayloadFullPath 'repo'
New-Item -ItemType Directory -Force -Path $payloadRepository | Out-Null
Get-ChildItem -LiteralPath $repositoryRootFullPath -File | Copy-Item -Destination $payloadRepository

$payloadDirectories = @(
	@("bin\$Configuration", "bin\$Configuration", $false),
	@("bin\Test$Configuration", "bin\Test$Configuration", $false),
	@('build-tools', 'build-tools', $false),
	@('eng', 'eng', $false),
	@('external', 'external', $false),
	@('.github', '.github', $true),
	@('samples', 'samples', $true),
	@('src-ThirdParty', 'src-ThirdParty', $true),
	@('tests', 'tests', $false),
	@('tools', 'tools', $true)
)
foreach ($directory in $payloadDirectories) {
	Copy-PayloadDirectory `
		-Source (Join-Path $repositoryRootFullPath (ConvertTo-NativeRelativePath $directory[0])) `
		-Destination (Join-Path $payloadRepository (ConvertTo-NativeRelativePath $directory[1])) `
		-Optional:([bool] $directory[2])
}

$sourceRoot = Join-Path $repositoryRootFullPath 'src'
foreach ($sourceDirectory in Get-ChildItem -LiteralPath $sourceRoot -Directory | Where-Object Name -ne 'Mono.Android') {
	Copy-PayloadDirectory `
		-Source $sourceDirectory.FullName `
		-Destination (Join-Path (Join-Path $payloadRepository 'src') $sourceDirectory.Name)
}

Copy-PayloadDirectory -Source $AndroidHome -Destination (Join-Path $correlationPayloadFullPath (ConvertTo-NativeRelativePath 'android-toolchain/sdk'))
Copy-PayloadDirectory -Source $JavaHome -Destination (Join-Path $correlationPayloadFullPath 'jdk')

if ([string]::IsNullOrWhiteSpace($NuGetPackages)) {
	$NuGetPackages = Join-Path ([Environment]::GetFolderPath('UserProfile')) (ConvertTo-NativeRelativePath '.nuget/packages')
}
Copy-PayloadDirectory -Source $NuGetPackages -Destination (Join-Path $correlationPayloadFullPath 'nuget-packages')

if ([string]::IsNullOrWhiteSpace($GradleUserHome)) {
	$GradleUserHome = Join-Path ([Environment]::GetFolderPath('UserProfile')) '.gradle'
}
foreach ($gradleDirectory in @('caches', 'wrapper')) {
	Copy-PayloadDirectory `
		-Source (Join-Path $GradleUserHome $gradleDirectory) `
		-Destination (Join-Path $correlationPayloadFullPath (ConvertTo-NativeRelativePath "gradle/$gradleDirectory")) `
		-Optional
}

if (-not [string]::IsNullOrWhiteSpace($DotNetToolsDirectory)) {
	$payloadTools = Join-Path $correlationPayloadFullPath 'dotnet-tools'
	New-Item -ItemType Directory -Force -Path $payloadTools | Out-Null
	$apkDiffShim = if ($Platform -eq 'windows') { 'apkdiff.exe' } else { 'apkdiff' }
	$apkDiffShimPath = Join-Path $DotNetToolsDirectory $apkDiffShim
	if (Test-Path -LiteralPath $apkDiffShimPath -PathType Leaf) {
		Copy-Item -LiteralPath $apkDiffShimPath -Destination $payloadTools
	}
	Copy-PayloadDirectory `
		-Source (Join-Path $DotNetToolsDirectory (ConvertTo-NativeRelativePath '.store/apkdiff')) `
		-Destination (Join-Path $payloadTools (ConvertTo-NativeRelativePath '.store/apkdiff')) `
		-Optional
}

$stagedTestAssembly = Join-Path $payloadRepository (ConvertTo-NativeRelativePath $TestAssemblyRelativePath)
if (-not (Test-Path -LiteralPath $stagedTestAssembly -PathType Leaf)) {
	throw "Test assembly '$stagedTestAssembly' was not staged."
}
if (-not (Test-Path -LiteralPath $TestSlicerPath -PathType Leaf)) {
	throw "dotnet-test-slicer '$TestSlicerPath' does not exist."
}

$discoveryRunSettings = Join-Path $workItemsFullPath 'all-tests.runsettings'
$sliceArguments = @(
	'slice',
	"--test-assembly=$stagedTestAssembly",
	'--slice-number=1',
	'--total-slices=1',
	"--outfile=$discoveryRunSettings"
)
if (-not [string]::IsNullOrWhiteSpace($TestFilter)) {
	$sliceArguments += "--test-filter=$TestFilter"
}

& $TestSlicerPath @sliceArguments
if ($LASTEXITCODE -ne 0) {
	throw "dotnet-test-slicer discovery failed with exit code $LASTEXITCODE."
}

$testNames = Get-NUnitRunSettingsTestNames -Path $discoveryRunSettings
$timingHistory = Get-HostBuildTestTimingHistory -Path $BalanceFile
$plan = New-DurationBalancedWorkItems `
	-TestNames $testNames `
	-TimingHistory $timingHistory `
	-TargetMinutes $TargetMinutes `
	-DurationParallelism $DurationParallelism `
	-FallbackTestDurationSeconds $FallbackTestDurationSeconds

Write-HostBuildTestWorkItemPayloads `
	-Platform $Platform `
	-Plan $plan `
	-WorkItemsDirectory $workItemsFullPath `
	-WorkItemsPropsPath $workItemsPropsFullPath `
	-TestAssemblyRelativePath $TestAssemblyRelativePath `
	-NUnitWorkers $NUnitWorkers `
	-Configuration $Configuration

$summaryPath = Join-Path $resultsFullPath 'work-item-generation.json'
[pscustomobject] @{
	platform = $Platform
	testFilter = $TestFilter
	durationMode = $plan.DurationMode
	targetMinutes = $TargetMinutes
	durationParallelism = $DurationParallelism
	fallbackTestDurationSeconds = $FallbackTestDurationSeconds
	testCount = $testNames.Count
	knownTestCount = $plan.KnownTestCount
	estimatedTestCount = $plan.EstimatedTestCount
	workItemCount = $plan.WorkItems.Count
	totalDurationLoadMs = $plan.TotalDurationMs
	workItems = @($plan.WorkItems | ForEach-Object {
		[pscustomobject] @{
			id = $_.Id
			testCount = $_.Tests.Count
			durationLoadMs = $_.DurationMs
			estimatedDurationMs = $_.EstimatedDurationMs
			oversized = $_.Oversized
		}
	})
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $summaryPath -Encoding utf8NoBOM

Write-Host "Generated $($plan.WorkItems.Count) $Platform Helix work items for $($testNames.Count) tests."
Write-Host "Duration mode: $($plan.DurationMode); known timings: $($plan.KnownTestCount); estimated timings: $($plan.EstimatedTestCount)."
Write-Host "Target: $TargetMinutes minute(s); duration parallelism: $DurationParallelism."
if ($plan.DurationMode -eq 'count-fallback') {
	Write-Warning "No matching timing history was available. Work items use an explicit count fallback of $FallbackTestDurationSeconds second(s) per test."
}

Write-Host "##vso[task.setvariable variable=HostBuildTestsHelixCorrelationPayload]$correlationPayloadFullPath"
Write-Host "##vso[task.setvariable variable=HostBuildTestsHelixWorkItemsProps]$workItemsPropsFullPath"
Write-Host "##vso[task.setvariable variable=HostBuildTestsHelixWorkItemCount]$($plan.WorkItems.Count)"
