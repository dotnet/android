#requires -Version 7.3
param ([switch] $AllowLinux)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $IsWindows -and -not ($AllowLinux -and $IsLinux)) {
    throw 'Diagnostic test acquisition requires Windows or explicitly enabled Linux.'
}
if (-not [string]::IsNullOrEmpty($env:NUNIT_MSBUILD_ARGS)) {
    throw 'Existing NUNIT_MSBUILD_ARGS must not be overwritten.'
}
if (-not [string]::IsNullOrEmpty($env:ANDROID_GUEST_READINESS_TEST_ACQUISITION) -and
    $env:ANDROID_GUEST_READINESS_TEST_ACQUISITION -cne '1') {
    throw 'ANDROID_GUEST_READINESS_TEST_ACQUISITION must be absent or exactly 1.'
}
$root = [IO.Path]::GetFullPath("$PSScriptRoot/../..")
$config = Join-Path $root 'NuGet.config'
$comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
if (-not (Test-Path -LiteralPath $config -PathType Leaf) -or
    [string]::IsNullOrEmpty($env:RESTORECONFIGFILE) -or
    -not [IO.Path]::IsPathFullyQualified($env:RESTORECONFIGFILE) -or
    -not [string]::Equals([IO.Path]::GetFullPath($env:RESTORECONFIGFILE), $config, $comparison) -or
    $config.IndexOfAny([char[]] "`r`n%;") -ge 0) {
    throw 'Diagnostic test acquisition requires the unchanged repository-root NuGet.config.'
}
Write-Output "##vso[task.setvariable variable=NUNIT_MSBUILD_ARGS]/p:RestoreConfigFile=`"$config`""
Write-Output '##vso[task.setvariable variable=ANDROID_GUEST_READINESS_TEST_ACQUISITION]1'
