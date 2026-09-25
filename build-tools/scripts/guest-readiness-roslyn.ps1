#requires -Version 7.3
param (
    [Parameter(Mandatory)][ValidateSet('Configure', 'Capture')][string] $Phase,
    [string] $Destination
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = [IO.Path]::GetFullPath("$PSScriptRoot/../..")
if (-not $IsWindows) { throw 'Guest readiness Roslyn routing is Windows-only.' }
if ($Phase -eq 'Configure') {
    if (-not [string]::IsNullOrEmpty($env:CustomAfterMicrosoftCommonTargets)) {
        throw 'An existing CustomAfterMicrosoftCommonTargets must not be overwritten.'
    }
    $target = Join-Path $PSScriptRoot 'GuestReadinessRoslynOutput.targets'
    if (-not (Test-Path -LiteralPath $target -PathType Leaf)) { throw 'Guest readiness Roslyn target is missing.' }
    Write-Output "##vso[task.setvariable variable=CustomAfterMicrosoftCommonTargets]$target"
    exit 0
}
if ([string]::IsNullOrWhiteSpace($Destination)) { throw 'Roslyn evidence destination is required.' }
$Destination = [IO.Path]::GetFullPath($Destination)
$sourceDirectory = Join-Path $root 'bin/guest-readiness-roslyn'
$rootSarif = Join-Path $root '.sarif'
foreach ($path in @($Destination, $sourceDirectory, $rootSarif)) {
    # A missing leaf can still traverse an existing junction; inspect its complete ancestor chain.
    $ancestor = [IO.DirectoryInfo]::new($path)
    while ($null -ne $ancestor) {
        if (Test-Path -LiteralPath $ancestor.FullName) {
            $item = Get-Item -LiteralPath $ancestor.FullName -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw 'Roslyn evidence paths must not traverse reparse points.'
            }
        }
        $ancestor = $ancestor.Parent
    }
}
if ($Destination.StartsWith($sourceDirectory, [StringComparison]::OrdinalIgnoreCase) -or
    (Test-Path -LiteralPath $Destination)) {
    throw 'Roslyn evidence destination must be new and outside its source directory.'
}
New-Item -ItemType Directory -Path $Destination | Out-Null
$records = [Collections.Generic.List[object]]::new()
$total = [long] 0
$files = @()
if (Test-Path -LiteralPath $sourceDirectory) {
    $directory = Get-Item -LiteralPath $sourceDirectory
    if (-not $directory.PSIsContainer -or ($directory.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw 'Roslyn output directory must be a regular directory.'
    }
    $files = @(Get-ChildItem -LiteralPath $sourceDirectory -Force)
    if ($files.Count -gt 10000) { throw 'Roslyn evidence exceeds the file-count bound.' }
}
$rootPresent = Test-Path -LiteralPath $rootSarif
if ($rootPresent) { $files += Get-Item -LiteralPath $rootSarif -Force }
foreach ($file in $files) {
    $isRoot = $file.FullName -ceq $rootSarif
    if ($file.PSIsContainer -or ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
        $file.Length -gt 67108864 -or
        (-not $isRoot -and $file.Name -cnotmatch '\A[^\\/]+\.csproj\.[0-9a-f]{32}\.gdn\.sarif\z')) {
        throw 'Unexpected or oversized Roslyn evidence file.'
    }
    $name = if ($isRoot) { 'unexplained-root.sarif' } else { $file.Name }
    # Deny concurrent writers/deletion while bounding and copying the original Windows file.
    $source = [IO.File]::Open($file.FullName, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try {
        $length = $source.Length
        if ($length -gt 67108864) { throw 'Unexpected or oversized Roslyn evidence file.' }
        $total += $length
        if ($total -gt 2147483648) { throw 'Roslyn evidence exceeds the total byte bound.' }
        $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($source)).ToLowerInvariant()
        $source.Position = 0
        $copy = [IO.File]::Open((Join-Path $Destination $name), [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
        try { $source.CopyTo($copy) } finally { $copy.Dispose() }
        if ((Get-FileHash -LiteralPath (Join-Path $Destination $name)).Hash.ToLowerInvariant() -cne $hash) {
            throw 'Roslyn evidence changed during capture.'
        }
        $file.Refresh()
    } finally {
        $source.Dispose()
    }
    $records.Add([pscustomobject]@{
        fileName = $name; sourcePath = $file.FullName; sizeBytes = $length
        lastWriteTimeUtc = $file.LastWriteTimeUtc.ToString('O'); sha256 = $hash
    })
}
Import-Module "$PSScriptRoot/guest-readiness-runtime-pack.psm1" -Force
Write-GuestProducerReceipt ([pscustomobject]@{
    schemaVersion = 1; kind = 'android-diagnostic-roslyn-output-observation'
    observedAtUtc = [DateTime]::UtcNow.ToString('O'); rootSarifPresent = $rootPresent
    files = @($records.ToArray())
    notice = 'Original bytes copied, never moved/deleted. Bare root .sarif has unknown provenance. No scanner processing or policy success is asserted.'
}) (Join-Path $Destination 'capture.json')
