#requires -Version 7.3
param (
    [Parameter(Mandatory)][ValidateSet('Configure', 'Capture', 'ObserveRoot')][string] $Phase,
    [string] $Destination
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = [IO.Path]::GetFullPath("$PSScriptRoot/../..")
if (-not $IsWindows) { throw 'Guest readiness Roslyn routing is Windows-only.' }
function Assert-NoReparsePointAncestor ([string] $Path) {
    # A missing leaf can still traverse an existing junction; inspect its complete ancestor chain.
    $ancestor = [IO.DirectoryInfo]::new($Path)
    while ($null -ne $ancestor) {
        if (Test-Path -LiteralPath $ancestor.FullName -ErrorAction Stop) {
            $item = Get-Item -LiteralPath $ancestor.FullName -Force -ErrorAction Stop
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw [InvalidOperationException]::new('Roslyn evidence paths must not traverse reparse points.')
            }
        }
        $ancestor = $ancestor.Parent
    }
}
if ($Phase -eq 'Configure') {
    if (-not [string]::IsNullOrEmpty($env:CustomAfterMicrosoftCommonTargets)) {
        throw 'An existing CustomAfterMicrosoftCommonTargets must not be overwritten.'
    }
    $target = Join-Path $PSScriptRoot 'GuestReadinessRoslynOutput.targets'
    if (-not (Test-Path -LiteralPath $target -PathType Leaf)) { throw 'Guest readiness Roslyn target is missing.' }
    Write-Output "##vso[task.setvariable variable=CustomAfterMicrosoftCommonTargets]$target"
    exit 0
}
$rootSarif = Join-Path $root '.sarif'
if ($Phase -eq 'ObserveRoot') {
    $observation = [ordered]@{
        schemaVersion = 1; kind = 'android-diagnostic-root-sarif-metadata'
        phase = 'post-sdl-analysis-pre-clean'; status = 'unavailable'; present = $null
    }
    $exitCode = 1
    $reason = 'path-validation-failed'
    try {
        Assert-NoReparsePointAncestor $rootSarif
        $reason = 'read-unavailable'
        if (-not (Test-Path -LiteralPath $rootSarif -ErrorAction Stop)) {
            $observation.status = 'observed'
            $observation.present = $false
            $exitCode = 0
        } else {
            $file = Get-Item -LiteralPath $rootSarif -Force -ErrorAction Stop
            $reason = 'not-regular-file'
            if ($file.PSIsContainer) { throw [InvalidOperationException]::new('Not a regular file.') }
            $reason = 'too-large'
            if ($file.Length -gt 67108864) { throw [InvalidOperationException]::new('File exceeds observation bound.') }
            $reason = 'read-unavailable'
            $source = [IO.File]::Open($rootSarif, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
            try {
                $reason = 'path-validation-failed'
                Assert-NoReparsePointAncestor $rootSarif
                $reason = 'read-unavailable'
                $file.Refresh()
                $length = $source.Length
                $created = $file.CreationTimeUtc
                $written = $file.LastWriteTimeUtc
                $reason = 'too-large'
                if ($length -gt 67108864) { throw [InvalidOperationException]::new('File exceeds observation bound.') }
                $reason = 'changed-during-read'
                if (-not $file.Exists -or $file.Length -ne $length) { throw [InvalidOperationException]::new('File metadata changed.') }
                $reason = 'read-unavailable'
                $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($source)).ToLowerInvariant()
                $file.Refresh()
                $reason = 'path-validation-failed'
                Assert-NoReparsePointAncestor $rootSarif
                $reason = 'changed-during-read'
                if (-not $file.Exists -or $source.Position -ne $length -or $source.Length -ne $length -or
                    $file.Length -ne $length -or $file.CreationTimeUtc -ne $created -or $file.LastWriteTimeUtc -ne $written) {
                    throw [InvalidOperationException]::new('File metadata changed.')
                }
                $observation.status = 'observed'
                $observation.present = $true
                $observation.sizeBytes = $length
                $observation.sha256 = $hash
                $observation.creationTimeUtc = $created.ToString('O')
                $observation.lastWriteTimeUtc = $written.ToString('O')
                $exitCode = 0
            } finally { $source.Dispose() }
        }
    } catch [IO.IOException], [UnauthorizedAccessException], [InvalidOperationException], [System.Management.Automation.ItemNotFoundException] {
        # Fixed failure metadata only; the new CI task alone continues so the clean gate still runs.
        $observation.status = 'unavailable'
        $observation.present = $null
        foreach ($key in @('sizeBytes', 'sha256', 'creationTimeUtc', 'lastWriteTimeUtc')) { $observation.Remove($key) }
        $observation.reason = $reason
        $exitCode = 1
    }
    Write-Output ($observation | ConvertTo-Json -Compress)
    exit $exitCode
}
if ([string]::IsNullOrWhiteSpace($Destination)) { throw 'Roslyn evidence destination is required.' }
$Destination = [IO.Path]::GetFullPath($Destination)
$sourceDirectory = Join-Path $root 'bin/guest-readiness-roslyn'
foreach ($path in @($Destination, $sourceDirectory, $rootSarif)) {
    Assert-NoReparsePointAncestor $path
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
