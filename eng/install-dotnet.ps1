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
$sdkNode = $versionsXml.SelectSingleNode('//MicrosoftNETSdkPackageVersion')
if ($null -eq $sdkNode -or [string]::IsNullOrWhiteSpace($sdkNode.InnerText)) {
  Write-Error "Could not read <MicrosoftNETSdkPackageVersion> from $versionsProps"
  exit 1
}
$sdkVersion = $sdkNode.InnerText

$installDir = Join-Path $repoRoot "bin\$configuration\dotnet"
New-Item -ItemType Directory -Force -Path $installDir | Out-Null

# Retry a script block up to 5 times with exponential backoff (2s, 4s, 8s,
# 16s). CI regularly hits transient network failures when reaching the .NET
# CDN (e.g. connection reset by peer).
function Invoke-WithRetry {
  param(
    [Parameter(Mandatory)][scriptblock] $Action,
    [int] $MaxAttempts = 5
  )
  $attempt = 1
  $delay = 2
  while ($true) {
    try {
      & $Action
      return
    } catch {
      if ($attempt -ge $MaxAttempts) {
        throw
      }
      Write-Warning "Command failed (attempt $attempt/$MaxAttempts), retrying in ${delay}s: $($_.Exception.Message)"
      Start-Sleep -Seconds $delay
      $attempt++
      $delay *= 2
    }
  }
}

# Download Microsoft's official dotnet-install.ps1 (cached under $installDir
# to avoid hitting the CDN on idempotent re-runs). Download to a temp file
# and atomically rename into place so a failed/interrupted download cannot
# poison the cache.
$installScript = Join-Path $installDir 'dotnet-install.ps1'
if (-not (Test-Path $installScript)) {
  [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
  $installScriptTmp = "$installScript.tmp.$PID"
  try {
  # -UseBasicParsing is required on Windows PowerShell (Desktop) to avoid
  # the Internet Explorer engine; on PowerShell 7+ (Core) it is the only
  # mode and the parameter is unnecessary, so gate it by edition.
  $iwrArgs = @{
    Uri     = 'https://builds.dotnet.microsoft.com/dotnet/scripts/v1/dotnet-install.ps1'
    OutFile = $installScriptTmp
  }
  if ($PSVersionTable.PSEdition -eq 'Desktop') {
    $iwrArgs['UseBasicParsing'] = $true
  }
  Invoke-WithRetry {
    Invoke-WebRequest @iwrArgs
  }
    Move-Item -LiteralPath $installScriptTmp -Destination $installScript
  } finally {
    if (Test-Path $installScriptTmp) {
      Remove-Item -LiteralPath $installScriptTmp -Force
    }
  }
}

Write-Host "Installing .NET SDK $sdkVersion into $installDir"
Invoke-WithRetry {
  & $installScript -Version $sdkVersion -InstallDir $installDir -NoPath
  if ($LASTEXITCODE -ne 0) {
    throw "dotnet-install.ps1 exited with code $LASTEXITCODE"
  }
}
