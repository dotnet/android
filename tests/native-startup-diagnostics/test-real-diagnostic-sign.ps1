#requires -Version 7.3
param ([string] $OriginalPackDirectory = '')
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath("$PSScriptRoot/../..")
$scratch = Join-Path $root "bin/real-sign-test-$([guid]::NewGuid().ToString('N'))"
$script = Join-Path $root 'build-tools/scripts/guest-readiness-real-sign.ps1'
$env:DIAGNOSTIC_BUILD_REASON = 'Manual'
$env:DIAGNOSTIC_REPOSITORY = 'dotnet/android'
$env:DIAGNOSTIC_SOURCE_BRANCH = 'refs/heads/diagnostic-real-sign-test'
$env:DIAGNOSTIC_SOURCE_VERSION = (& git -C $root rev-parse HEAD)
$env:DIAGNOSTIC_SIGNING_TEMPLATE_VERSION = '19fa9b5addc2c2d99a6e09492a60b3b2f01e799b'
$env:DIAGNOSTIC_1ES_VERSION = '76ca628b0f2f914dd9a2fa37b54bffe2778f7f81'
if ($LASTEXITCODE -ne 0) { throw 'Git source identity is unavailable.' }
try {
    foreach ($scenario in @(
        [pscustomobject]@{ name = 'failed-signer'; status = 'Failed'; signedDirectory = $false },
        [pscustomobject]@{ name = 'missing-input'; status = 'Succeeded'; signedDirectory = $true }
    )) {
        $case = Join-Path $scratch $scenario.name
        $signed = Join-Path $case 'signed'
        $evidence = Join-Path $case 'evidence'
        if ($scenario.signedDirectory) { New-Item -ItemType Directory -Path $signed -Force | Out-Null }
        $env:DIAGNOSTIC_SIGNING_JOB_STATUS = $scenario.status
        $rejected = $false
        try {
            & $script -Phase Output -SourceDirectory $signed -DestinationDirectory (Join-Path $case 'unsigned') `
                -EvidenceDirectory $evidence | Out-Null
        } catch {
            $rejected = $true
        }
        if (-not $rejected) { throw "$($scenario.name) accepted incomplete signing output." }
        $failure = Get-Content (Join-Path $evidence 'output-failure.json') -Raw | ConvertFrom-Json
        if ($failure.status -cne 'failed' -or $failure.packageAdmission -ne $false -or
            (Test-Path (Join-Path $evidence 'output-receipt.json')) -or
            -not (Test-Path (Join-Path $case 'status.failed.json'))) {
            throw "$($scenario.name) did not retain only explicit failure evidence."
        }
    }
    if ($OriginalPackDirectory) {
        $case = Join-Path $scratch 'original'
        $working = Join-Path $case 'working'
        & $script -Phase Input -SourceDirectory $OriginalPackDirectory -DestinationDirectory (Join-Path $case 'unsigned') `
            -EvidenceDirectory (Join-Path $case 'evidence') -WorkingDirectory $working
        if (-not $?) { throw 'Original frozen unsigned input was rejected.' }
        $receipt = Get-Content (Join-Path $case 'evidence/input-receipt.json') -Raw | ConvertFrom-Json
        if ($receipt.status -cne 'staged-unadmitted' -or @($receipt.packages).Count -ne 2 -or
            -not (Test-Path -LiteralPath $working -PathType Container) -or
            @(Get-ChildItem -LiteralPath $working -Force).Count -ne 0) {
            throw 'Original input receipt omitted a frozen archive.'
        }
        $collision = Join-Path $scratch 'existing-working'
        New-Item -ItemType Directory -Path $collision | Out-Null
        $rejected = $false
        try {
            & $script -Phase Input -SourceDirectory $OriginalPackDirectory -DestinationDirectory (Join-Path $scratch 'never-staged') `
                -EvidenceDirectory (Join-Path $scratch 'never-evidence') -WorkingDirectory $collision | Out-Null
        } catch {
            $rejected = $true
        }
        if (-not $rejected -or (Test-Path (Join-Path $scratch 'never-staged'))) {
            throw 'Pre-existing working directory was not rejected before staging.'
        }
    }
    'PASS: failed signing and missing input remain unadmitted with publishable failure evidence.'
} finally {
    if (Test-Path -LiteralPath $scratch) { Remove-Item -LiteralPath $scratch -Recurse -Force }
}
