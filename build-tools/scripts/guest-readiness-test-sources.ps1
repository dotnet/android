#requires -Version 7.3
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $IsWindows) { throw 'Diagnostic test acquisition configuration is Windows-only.' }
if (-not [string]::IsNullOrEmpty($env:NUNIT_MSBUILD_ARGS)) {
    throw 'Existing NUNIT_MSBUILD_ARGS must not be overwritten.'
}
if (-not [string]::IsNullOrEmpty($env:ANDROID_GUEST_READINESS_TEST_ACQUISITION) -and
    $env:ANDROID_GUEST_READINESS_TEST_ACQUISITION -cne '1') {
    throw 'ANDROID_GUEST_READINESS_TEST_ACQUISITION must be absent or exactly 1.'
}
$root = [IO.Path]::GetFullPath("$PSScriptRoot/../..")
$config = Join-Path $root 'NuGet.config'
if (-not (Test-Path -LiteralPath $config -PathType Leaf) -or
    [string]::IsNullOrEmpty($env:RESTORECONFIGFILE) -or
    -not [IO.Path]::IsPathFullyQualified($env:RESTORECONFIGFILE) -or
    [IO.Path]::GetFullPath($env:RESTORECONFIGFILE) -ine $config -or
    $config.IndexOfAny([char[]] "`r`n%;") -ge 0) {
    throw 'Diagnostic test acquisition requires the unchanged repository-root NuGet.config.'
}
Write-Output "##vso[task.setvariable variable=NUNIT_MSBUILD_ARGS]/p:RestoreConfigFile=`"$config`""
Write-Output '##vso[task.setvariable variable=ANDROID_GUEST_READINESS_TEST_ACQUISITION]1'
