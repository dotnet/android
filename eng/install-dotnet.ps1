<#
.SYNOPSIS
  Provisions the .NET SDK into bin\$Configuration\dotnet\.

.DESCRIPTION
  The SDK version is read from eng\Versions.props (single source of truth
  kept up to date by darc when Microsoft.NET.Sdk flows from dotnet/dotnet),
  so global.json does not need a 'tools.dotnet' pin.
#>
[CmdletBinding(PositionalBinding=$false)]
param(
  [string][Alias('c')] $configuration = $(if ($env:CONFIGURATION) { $env:CONFIGURATION } else { 'Debug' })
)

$ErrorActionPreference = 'Stop'

$scriptroot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptroot '..')).Path

$versionsProps = Join-Path $repoRoot 'eng\Versions.props'
[xml] $versionsXml = Get-Content -LiteralPath $versionsProps
$sdkVersion = $versionsXml.SelectSingleNode('//MicrosoftNETSdkPackageVersion').InnerText
if ([string]::IsNullOrWhiteSpace($sdkVersion)) {
  Write-Error "Could not read <MicrosoftNETSdkPackageVersion> from $versionsProps"
  exit 1
}

$installDir = Join-Path $repoRoot "bin\$configuration\dotnet"
New-Item -ItemType Directory -Force -Path $installDir | Out-Null

# Download Microsoft's official dotnet-install.ps1 (cached under $installDir
# to avoid hitting the CDN on idempotent re-runs).
$installScript = Join-Path $installDir 'dotnet-install.ps1'
if (-not (Test-Path $installScript)) {
  [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
  Invoke-WebRequest -Uri 'https://builds.dotnet.microsoft.com/dotnet/scripts/v1/dotnet-install.ps1' -OutFile $installScript -UseBasicParsing
}

Write-Host "Installing .NET SDK $sdkVersion into $installDir"
& $installScript -Version $sdkVersion -InstallDir $installDir -NoPath
if ($LASTEXITCODE -ne 0) {
  exit $LASTEXITCODE
}
