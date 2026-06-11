<#
.SYNOPSIS
  Provisions the .NET SDK pinned in global.json (tools.dotnet) into
  bin\$Configuration\dotnet\ via Arcade's eng\common\tools.ps1 helpers.

.DESCRIPTION
  Thin wrapper around InitializeDotNetCli so callers that only want SDK
  provisioning don't have to invoke eng\common\build.ps1, which also
  restores the Arcade toolset MSBuild project.
#>
[CmdletBinding(PositionalBinding=$false)]
param(
  [string][Alias('c')] $configuration = $(if ($env:CONFIGURATION) { $env:CONFIGURATION } else { 'Debug' })
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$scriptroot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptroot '..')).Path

# Pin the SDK install location to bin\$Configuration\dotnet\. Arcade
# reads DOTNET_INSTALL_DIR first (use existing SDK if present); when
# nothing is found there, it installs into DOTNET_GLOBAL_INSTALL_DIR.
# Setting both to the same path makes the install idempotent.
$env:DOTNET_INSTALL_DIR = Join-Path $repoRoot "bin\$configuration\dotnet"
$env:DOTNET_GLOBAL_INSTALL_DIR = $env:DOTNET_INSTALL_DIR
New-Item -ItemType Directory -Force -Path $env:DOTNET_INSTALL_DIR | Out-Null

# Don't fall back to a system dotnet that happens to match the pinned
# version; we always want the SDK in our own bin\ folder so the rest of
# the build picks it up via dotnet-local.{cmd,sh}.
$useInstalledDotNetCli = $false

. (Join-Path $scriptroot 'common\tools.ps1')

InitializeDotNetCli -install $true | Out-Null
