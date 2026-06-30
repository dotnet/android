#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Mirrors a gradle project's dependencies into the dnceng dotnet-public-maven
    Azure Artifacts feed so CI can resolve them anonymously.

.DESCRIPTION
    When Dependabot bumps a gradle dependency (or its transitive graph changes),
    CI fails with 401 errors because the new package(s) haven't been pulled
    from upstream into the dnceng feed yet. CI agents only do anonymous reads,
    so a developer has to authenticate locally once to seed the feed.

    This script does that by running the requested gradle build in a loop:
      1. Run gradle with RunningOnCI=true so it points at the dnceng feed.
      2. Parse any 'Could not GET' URLs out of the build log.
      3. Re-fetch each failing URL with an Azure DevOps OAuth bearer token
         (obtained via `az account get-access-token`). The feed's upstream
         connector then pulls the package and caches it for anonymous reads.
      4. Repeat until the build succeeds or no more 401s appear.

    After the loop converges, no PR edits are needed — just re-run the failing
    CI job, since the packages are now anonymous-readable.

.PARAMETER ProjectDir
    Path to the gradle project (the one containing the failing dependency).
    Mirroring must run in the project that actually requires the package;
    a sibling project's build won't trigger a mirror for someone else's deps.

.PARAMETER Task
    Gradle task(s) to run. Should be one that resolves the new dependency
    graph (e.g. 'assembleDebug', 'build', 'extractProguardFiles').

.PARAMETER AndroidHome
    Optional path to the Android SDK. Required when the gradle build needs it
    (any project using the com.android.* plugins). Defaults to the value of
    `$env:ANDROID_HOME` if set.

.PARAMETER MaxIterations
    Cap on build/mirror cycles. Default 15. Typical convergence is 2-5
    iterations as the resolver walks the dep graph breadth-first.

.EXAMPLE
    pwsh ./eng/gradle/mirror-dependencies.ps1 `
        -ProjectDir tests/CodeGen-Binding/Xamarin.Android.LibraryProjectZip-LibBinding/java/JavaLib `
        -Task assembleDebug `
        -AndroidHome D:\android-toolchain\sdk

.EXAMPLE
    pwsh ./eng/gradle/mirror-dependencies.ps1 -ProjectDir src/proguard-android -Task extractProguardFiles
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string] $ProjectDir,

    [Parameter(Mandatory=$true)]
    [string] $Task,

    [string] $AndroidHome = $env:ANDROID_HOME,

    [int] $MaxIterations = 15
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '../..') | Select-Object -ExpandProperty Path
$projectDirAbs = Resolve-Path (Join-Path $repoRoot $ProjectDir) -ErrorAction Stop | Select-Object -ExpandProperty Path
$gradlew = if ($IsWindows -or $env:OS -eq 'Windows_NT') {
    Join-Path $repoRoot 'build-tools/gradle/gradlew.bat'
} else {
    Join-Path $repoRoot 'build-tools/gradle/gradlew'
}
if (-not (Test-Path $gradlew)) { throw "gradlew not found at $gradlew" }

# Azure DevOps resource id — same for every AzDO tenant.
$azDevOpsResource = '499b84ac-1321-427f-aa17-267ca6975798'

function Get-AzDevOpsToken {
    $token = az account get-access-token --resource $azDevOpsResource --query accessToken -o tsv 2>$null
    if ([string]::IsNullOrEmpty($token)) {
        throw "Could not get an Azure DevOps access token. Run 'az login' first."
    }
    return $token
}

function Invoke-Mirror($logPath) {
    $urls = Select-String -Path $logPath -Pattern "Could not GET 'https://pkgs\.dev\.azure\.com/dnceng/[^']+'" -AllMatches |
        ForEach-Object { $_.Matches } |
        ForEach-Object { $_.Value -replace "^Could not GET '", "" -replace "'$", "" } |
        Sort-Object -Unique
    if ($urls.Count -eq 0) { return 0 }
    $token = Get-AzDevOpsToken
    $headers = @{ Authorization = "Bearer $token" }
    $ok = 0; $fail = 0
    foreach ($u in $urls) {
        try {
            $r = Invoke-WebRequest -Uri $u -Headers $headers -SkipHttpErrorCheck -ErrorAction Stop
            if ($r.StatusCode -eq 200) { $ok++ } else { $fail++; Write-Host "  $($r.StatusCode) $u" -ForegroundColor Yellow }
        } catch {
            $fail++
            Write-Host "  ERR $u : $_" -ForegroundColor Yellow
        }
    }
    Write-Host "  -> mirrored OK=$ok, not-found=$fail (of $($urls.Count))" -ForegroundColor Cyan
    return $urls.Count
}

Write-Host "Repo root:    $repoRoot"
Write-Host "Project:      $projectDirAbs"
Write-Host "Task:         $Task"
if ($AndroidHome) { Write-Host "ANDROID_HOME: $AndroidHome" }

# Verify az is available and authenticated up front so we fail fast.
Get-AzDevOpsToken | Out-Null

if ($AndroidHome) { $env:ANDROID_HOME = $AndroidHome }
$env:RunningOnCI = 'true'

Push-Location $projectDirAbs
try {
    for ($i = 1; $i -le $MaxIterations; $i++) {
        Write-Host "`n=== iteration $i ===" -ForegroundColor Green
        $log = Join-Path ([IO.Path]::GetTempPath()) "gradle-mirror-iter-$i.log"
        & $gradlew $Task --no-daemon --refresh-dependencies *>&1 | Tee-Object -FilePath $log | Out-Null
        if (Select-String -Path $log -Pattern 'BUILD SUCCESSFUL' -SimpleMatch -Quiet) {
            Write-Host "`nBUILD SUCCESSFUL after $i iteration(s). The feed now has the packages CI needs." -ForegroundColor Green
            return
        }
        $count = Invoke-Mirror $log
        if ($count -eq 0) {
            Write-Host "`nGradle failed but no 401s to mirror — see $log" -ForegroundColor Red
            Get-Content $log -Tail 30
            exit 1
        }
    }
    Write-Host "`nExhausted $MaxIterations iterations without success. Last log:" -ForegroundColor Red
    Get-Content $log -Tail 30
    exit 1
}
finally {
    Pop-Location
}
