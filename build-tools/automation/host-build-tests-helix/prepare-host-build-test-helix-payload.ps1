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
	[string] $CorrelationPayloadsPropsPath,

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
	[string] $AndroidNdkHome,

	[Parameter(Mandatory)]
	[string] $JavaHome,

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

function Get-DirectorySize
{
	param (
		[Parameter(Mandatory)]
		[string] $Path
	)

	$size = 0L
	foreach ($file in Get-ChildItem -LiteralPath $Path -Recurse -File) {
		$size += $file.Length
	}
	return $size
}

$correlationPayloads = [System.Collections.Generic.List[object]]::new()
$filePayloadIndex = 0
$maximumPayloadBytes = 1GB

function Add-CorrelationPayload
{
	param (
		[Parameter(Mandatory)]
		[string] $Source,

		[Parameter(Mandatory)]
		[string] $Destination,

		[Parameter(Mandatory)]
		[long] $SizeBytes
	)

	$script:correlationPayloads.Add([pscustomobject] @{
		Source = [IO.Path]::GetFullPath($Source)
		Destination = $Destination.Replace('\', '/').Trim('/')
		SizeBytes = $SizeBytes
	})
}

function Add-FileCorrelationPayloads
{
	param (
		[Parameter(Mandatory)]
		[IO.FileInfo[]] $Files,

		[Parameter(Mandatory)]
		[string] $Destination
	)

	$currentFiles = [System.Collections.Generic.List[IO.FileInfo]]::new()
	$currentSize = 0L
	foreach ($file in $Files | Sort-Object Name) {
		if ($file.Length -gt $script:maximumPayloadBytes) {
			throw "File '$($file.FullName)' exceeds the maximum correlation payload size."
		}
		if ($currentFiles.Count -gt 0 -and $currentSize + $file.Length -gt $script:maximumPayloadBytes) {
			New-FileCorrelationPayload -Files $currentFiles.ToArray() -Destination $Destination -SizeBytes $currentSize
			$currentFiles.Clear()
			$currentSize = 0L
		}
		$currentFiles.Add($file)
		$currentSize += $file.Length
	}
	if ($currentFiles.Count -gt 0) {
		New-FileCorrelationPayload -Files $currentFiles.ToArray() -Destination $Destination -SizeBytes $currentSize
	}
}

function New-FileCorrelationPayload
{
	param (
		[Parameter(Mandatory)]
		[IO.FileInfo[]] $Files,

		[Parameter(Mandatory)]
		[string] $Destination,

		[Parameter(Mandatory)]
		[long] $SizeBytes
	)

	$script:filePayloadIndex++
	$payloadDirectory = Join-Path $script:correlationPayloadFullPath ('files-{0:d3}' -f $script:filePayloadIndex)
	New-Item -ItemType Directory -Force -Path $payloadDirectory | Out-Null
	foreach ($file in $Files) {
		$target = Join-Path $payloadDirectory $file.Name
		try {
			New-Item -ItemType HardLink -Path $target -Target $file.FullName | Out-Null
		} catch {
			Copy-Item -LiteralPath $file.FullName -Destination $target
		}
	}
	Add-CorrelationPayload -Source $payloadDirectory -Destination $Destination -SizeBytes $SizeBytes
}

function Add-DirectoryCorrelationPayloads
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
			Write-Warning "Optional correlation payload directory '$Source' does not exist."
			return
		}
		throw "Required correlation payload directory '$Source' does not exist."
	}

	$size = Get-DirectorySize -Path $Source
	if ($size -le $script:maximumPayloadBytes) {
		Add-CorrelationPayload -Source $Source -Destination $Destination -SizeBytes $size
		return
	}

	$files = @(Get-ChildItem -LiteralPath $Source -File)
	if ($files.Count -gt 0) {
		Add-FileCorrelationPayloads -Files $files -Destination $Destination
	}
	foreach ($directory in Get-ChildItem -LiteralPath $Source -Directory | Sort-Object Name) {
		Add-DirectoryCorrelationPayloads `
			-Source $directory.FullName `
			-Destination "$($Destination.TrimEnd('/'))/$($directory.Name)"
	}
}

function Write-CorrelationPayloadProps
{
	param (
		[Parameter(Mandatory)]
		[string] $Path
	)

	$settings = [Xml.XmlWriterSettings]::new()
	$settings.Indent = $true
	$settings.Encoding = [Text.UTF8Encoding]::new($false)
	$writer = [Xml.XmlWriter]::Create($Path, $settings)
	try {
		$writer.WriteStartElement('Project')
		$writer.WriteStartElement('ItemGroup')
		foreach ($payload in $script:correlationPayloads) {
			$writer.WriteStartElement('_HostBuildTestHelixCorrelationPayload')
			$writer.WriteAttributeString('Include', $payload.Source)
			$writer.WriteElementString('Destination', $payload.Destination)
			$writer.WriteEndElement()
		}
		$writer.WriteEndElement()
		$writer.WriteEndElement()
	} finally {
		$writer.Dispose()
	}
}

$repositoryRootFullPath = [IO.Path]::GetFullPath($RepositoryRoot)
$correlationPayloadFullPath = [IO.Path]::GetFullPath($CorrelationPayloadDirectory)
$correlationPayloadsPropsFullPath = [IO.Path]::GetFullPath($CorrelationPayloadsPropsPath)
$workItemsFullPath = [IO.Path]::GetFullPath($WorkItemsDirectory)
$workItemsPropsFullPath = [IO.Path]::GetFullPath($WorkItemsPropsPath)
$resultsFullPath = [IO.Path]::GetFullPath($ResultsDirectory)

Remove-Item -LiteralPath $correlationPayloadFullPath -Recurse -Force -ErrorAction Ignore
Remove-Item -LiteralPath $workItemsFullPath -Recurse -Force -ErrorAction Ignore
New-Item -ItemType Directory -Force -Path $correlationPayloadFullPath, $workItemsFullPath, $resultsFullPath | Out-Null

$payloadRepository = Join-Path $correlationPayloadFullPath 'repository'
New-Item -ItemType Directory -Force -Path $payloadRepository | Out-Null
Get-ChildItem -LiteralPath $repositoryRootFullPath -File | Copy-Item -Destination $payloadRepository

$payloadDirectories = @(
	@('build-tools', 'build-tools', $false),
	@('eng', 'eng', $false),
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

$payloadTools = Join-Path $correlationPayloadFullPath 'dotnet-tools'
if (-not [string]::IsNullOrWhiteSpace($DotNetToolsDirectory)) {
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

Add-DirectoryCorrelationPayloads -Source $payloadRepository -Destination 'repo'
Add-DirectoryCorrelationPayloads `
	-Source (Join-Path $repositoryRootFullPath (ConvertTo-NativeRelativePath "bin/$Configuration")) `
	-Destination "repo/bin/$Configuration"
Add-DirectoryCorrelationPayloads `
	-Source (Join-Path $repositoryRootFullPath (ConvertTo-NativeRelativePath "bin/Build$Configuration/nuget-unsigned")) `
	-Destination "repo/bin/Build$Configuration/nuget-unsigned"

$testOutputRoot = Join-Path $repositoryRootFullPath (ConvertTo-NativeRelativePath "bin/Test$Configuration")
$testAssembly = Join-Path $repositoryRootFullPath (ConvertTo-NativeRelativePath $TestAssemblyRelativePath)
$testAssemblyDirectory = Split-Path -Parent $testAssembly
$testAssemblyDestination = "repo/bin/Test$Configuration/$([IO.Path]::GetRelativePath($testOutputRoot, $testAssemblyDirectory).Replace('\', '/'))"
Add-DirectoryCorrelationPayloads -Source $testAssemblyDirectory -Destination $testAssemblyDestination
$testOutputFiles = @(Get-ChildItem -LiteralPath $testOutputRoot -File)
if ($testOutputFiles.Count -gt 0) {
	Add-FileCorrelationPayloads -Files $testOutputFiles -Destination "repo/bin/Test$Configuration"
}
foreach ($testDirectory in Get-ChildItem -LiteralPath $testOutputRoot -Directory |
	Where-Object { $_.Name -eq 'Expected' -or $_.Name -like 'android-*' }) {
	Add-DirectoryCorrelationPayloads `
		-Source $testDirectory.FullName `
		-Destination "repo/bin/Test$Configuration/$($testDirectory.Name)"
}

$androidRootFiles = @(Get-ChildItem -LiteralPath $AndroidHome -File)
if ($androidRootFiles.Count -gt 0) {
	Add-FileCorrelationPayloads -Files $androidRootFiles -Destination 'android-toolchain/sdk'
}
$excludedAndroidDirectories = @('docs', 'emulator', 'extras', 'skins', 'sources', 'system-images')
foreach ($androidDirectory in Get-ChildItem -LiteralPath $AndroidHome -Directory | Sort-Object Name) {
	if ($androidDirectory.Name -notin $excludedAndroidDirectories) {
		Add-DirectoryCorrelationPayloads `
			-Source $androidDirectory.FullName `
			-Destination "android-toolchain/sdk/$($androidDirectory.Name)"
	}
}

Add-DirectoryCorrelationPayloads -Source $AndroidNdkHome -Destination 'android-toolchain/ndk'
Add-DirectoryCorrelationPayloads -Source $JavaHome -Destination 'jdk'

if ([string]::IsNullOrWhiteSpace($GradleUserHome)) {
	$GradleUserHome = Join-Path ([Environment]::GetFolderPath('UserProfile')) '.gradle'
}
$gradleDistsDirectory = Join-Path $GradleUserHome (ConvertTo-NativeRelativePath 'wrapper/dists')
New-Item -ItemType Directory -Force -Path $gradleDistsDirectory | Out-Null
Add-DirectoryCorrelationPayloads `
	-Source $gradleDistsDirectory `
	-Destination 'gradle/wrapper/dists'

Add-DirectoryCorrelationPayloads `
	-Source (Join-Path $GradleUserHome (ConvertTo-NativeRelativePath 'caches/modules-2')) `
	-Destination 'gradle/caches/modules-2' `
	-Optional
if (Test-Path -LiteralPath $payloadTools -PathType Container) {
	Add-DirectoryCorrelationPayloads -Source $payloadTools -Destination 'dotnet-tools'
}

Write-CorrelationPayloadProps -Path $correlationPayloadsPropsFullPath
$payloadBytes = ($correlationPayloads | Measure-Object -Property SizeBytes -Sum).Sum
$largestPayloadBytes = ($correlationPayloads | Measure-Object -Property SizeBytes -Maximum).Maximum
Write-Host ('Prepared {0} correlation payloads totaling {1:N2} GiB; largest payload is {2:N2} GiB.' -f `
	$correlationPayloads.Count, ($payloadBytes / 1GB), ($largestPayloadBytes / 1GB))
Write-Host 'Largest correlation payload components:'
$correlationPayloads |
	Sort-Object SizeBytes -Descending |
	Select-Object -First 20 |
	ForEach-Object {
		Write-Host ('- {0:N2} GiB -> {1}' -f ($_.SizeBytes / 1GB), $_.Destination)
	}

if (-not (Test-Path -LiteralPath $testAssembly -PathType Leaf)) {
	throw "Test assembly '$testAssembly' does not exist."
}
if (-not (Test-Path -LiteralPath $TestSlicerPath -PathType Leaf)) {
	throw "dotnet-test-slicer '$TestSlicerPath' does not exist."
}

$discoveryRunSettings = Join-Path $workItemsFullPath 'all-tests.runsettings'
$sliceArguments = @(
	'slice',
	"--test-assembly=$testAssembly",
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
	correlationPayloadCount = $correlationPayloads.Count
	correlationPayloadBytes = $payloadBytes
	correlationPayloads = @($correlationPayloads | ForEach-Object {
		[pscustomobject] @{
			destination = $_.Destination
			sizeBytes = $_.SizeBytes
		}
	})
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

Write-Host "##vso[task.setvariable variable=HostBuildTestsHelixCorrelationPayloadsProps]$correlationPayloadsPropsFullPath"
Write-Host "##vso[task.setvariable variable=HostBuildTestsHelixWorkItemsProps]$workItemsPropsFullPath"
Write-Host "##vso[task.setvariable variable=HostBuildTestsHelixWorkItemCount]$($plan.WorkItems.Count)"
