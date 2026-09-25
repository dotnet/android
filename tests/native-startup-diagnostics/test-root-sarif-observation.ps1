#requires -Version 7.3
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $IsWindows) { throw 'Root SARIF observation regression requires Windows.' }
function Assert ($Condition, [string] $Message) {
    if (-not $Condition) { throw $Message }
}
$root = [IO.Path]::GetFullPath("$PSScriptRoot/../..")
$out = Join-Path $root ('bin/root-sarif-observation/' + [Guid]::NewGuid().ToString('N'))
$fixture = Join-Path $out 'source with spaces'
$scripts = Join-Path $fixture 'build-tools/scripts'
New-Item -ItemType Directory $scripts -Force | Out-Null
Copy-Item "$root/build-tools/scripts/guest-readiness-roslyn.ps1" $scripts
$script = Join-Path $scripts 'guest-readiness-roslyn.ps1'
$file = Join-Path $fixture '.sarif'
$results = [Collections.Generic.List[object]]::new()

function Observe ([string] $ExpectedStatus, [int] $ExpectedExit, [string] $Reason = '', [string] $ScriptPath = $script) {
    $beforeFiles = @(Get-ChildItem -LiteralPath $fixture -Recurse -Force -File | ForEach-Object FullName)
    $output = @(& pwsh -NoProfile -File $ScriptPath -Phase ObserveRoot 2>&1)
    $code = $LASTEXITCODE
    Assert ($code -eq $ExpectedExit) 'Unexpected actual observer process exit code.'
    Assert ($output.Count -eq 1) 'Observer must emit exactly one fixed JSON record, with no content or exception dump.'
    $record = $output[0].ToString() | ConvertFrom-Json
    Assert ($record.schemaVersion -eq 1 -and $record.kind -ceq 'android-diagnostic-root-sarif-metadata') 'Wrong metadata schema.'
    Assert ($record.phase -ceq 'post-sdl-analysis-pre-clean' -and $record.status -ceq $ExpectedStatus) 'Wrong phase or health.'
    $keys = @('schemaVersion', 'kind', 'phase', 'status', 'present')
    if ($ExpectedStatus -eq 'unavailable') {
        Assert ($null -eq $record.present -and $record.reason -ceq $Reason) 'Unavailable must not assert presence or success.'
        $keys += 'reason'
    } elseif ($record.present) {
        $keys += @('sizeBytes', 'sha256', 'creationTimeUtc', 'lastWriteTimeUtc')
    }
    Assert (@(Compare-Object ($keys | Sort-Object) ($record.PSObject.Properties.Name | Sort-Object)).Count -eq 0) 'Unexpected metadata fields.'
    Assert (-not $output[0].ToString().Contains('fixture-private-content')) 'File content leaked.'
    Assert (-not $output[0].ToString().Contains($fixture)) 'Local path leaked.'
    $afterFiles = @(Get-ChildItem -LiteralPath $fixture -Recurse -Force -File | ForEach-Object FullName)
    Assert (@(Compare-Object $beforeFiles $afterFiles).Count -eq 0) 'Observer created or removed a file.'
    $results.Add(@{ exitCode=$code; observation=$record })
    return $record
}

$absent = Observe 'observed' 0
Assert ($absent.present -eq $false) 'Absent file must be an explicit point-in-time absence.'
[IO.File]::WriteAllBytes($file, [byte[]]@())
$empty = Observe 'observed' 0
Assert ($empty.present -and $empty.sizeBytes -eq 0 -and
    $empty.sha256 -ceq (Get-FileHash -LiteralPath $file).Hash.ToLowerInvariant()) 'Empty regular file must have its actual hash.'
[IO.File]::WriteAllText($file, 'fixture-private-content: not necessarily valid SARIF')
$before = Get-Item -LiteralPath $file -Force
$created = $before.CreationTimeUtc
$written = $before.LastWriteTimeUtc
$hash = (Get-FileHash -LiteralPath $file).Hash.ToLowerInvariant()
$present = Observe 'observed' 0
Assert ($present.present -and $present.sizeBytes -eq $before.Length -and $present.sha256 -ceq $hash) 'Regular file metadata/hash mismatch.'
Assert ([DateTime]$present.creationTimeUtc -eq $created -and [DateTime]$present.lastWriteTimeUtc -eq $written) 'File timestamps mismatch.'
Assert ((Get-FileHash -LiteralPath $file).Hash.ToLowerInvariant() -ceq $hash) 'Observer modified original bytes.'

$writer = [IO.File]::Open($file, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::ReadWrite)
try { $null = Observe 'unavailable' 1 'read-unavailable' } finally { $writer.Dispose() }
Assert ((Get-FileHash -LiteralPath $file).Hash.ToLowerInvariant() -ceq $hash) 'Writer rejection altered the source.'
$stream = [IO.File]::Open($file, [IO.FileMode]::Open, [IO.FileAccess]::Write)
try { $stream.SetLength(67108864) } finally { $stream.Dispose() }
$atCap = Observe 'observed' 0
Assert ($atCap.sizeBytes -eq 67108864 -and $atCap.sha256 -ceq (Get-FileHash -LiteralPath $file).Hash.ToLowerInvariant()) 'Exact 64MiB bound must remain readable.'
$stream = [IO.File]::Open($file, [IO.FileMode]::Open, [IO.FileAccess]::Write)
try { $stream.SetLength(67108865) } finally { $stream.Dispose() }
$null = Observe 'unavailable' 1 'too-large'
Assert ((Get-Item -LiteralPath $file -Force).Length -eq 67108865) 'Oversized source changed.'
[IO.File]::Delete($file)
New-Item -ItemType Directory $file | Out-Null
$null = Observe 'unavailable' 1 'not-regular-file'
[IO.Directory]::Delete($file)
$target = Join-Path $out 'junction target'
New-Item -ItemType Directory $target | Out-Null
New-Item -ItemType Junction -Path $file -Target $target | Out-Null
$null = Observe 'unavailable' 1 'path-validation-failed'
[IO.Directory]::Delete($file)
$alias = Join-Path $out 'source ancestor junction'
New-Item -ItemType Junction -Path $alias -Target $fixture | Out-Null
$null = Observe 'unavailable' 1 'path-validation-failed' (Join-Path $alias 'build-tools/scripts/guest-readiness-roslyn.ps1')
Assert (-not (Test-Path -LiteralPath $file)) 'Missing leaf under ancestor junction must remain absent.'
$results | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $out 'observations.json')
Write-Output "PASS: actual observer exit codes, exact metadata schema, regular/absent/64MiB/writer/leaf and ancestor junction controls; no original bytes changed by observation. Evidence: $out"
