#!/usr/bin/env bash
#
# Provisions the .NET SDK pinned in global.json (tools.dotnet) into
# bin/$Configuration/dotnet/ via Arcade's eng/common/tools.sh helpers.
#
# This is a thin wrapper around InitializeDotNetCli so that callers who
# only want SDK provisioning don't have to invoke eng/common/build.sh,
# which also restores the Arcade toolset MSBuild project.
#
# Inputs (env vars):
#   CONFIGURATION   - Debug (default) or Release; controls install path.
#   ci              - 'true' on CI; disables telemetry, etc. (Arcade convention)
#

set -euo pipefail

scriptroot="$( cd -P "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
repo_root="$( cd -P "$scriptroot/.." && pwd )"

configuration="${CONFIGURATION:-Debug}"

# Pin the SDK install location to bin/$Configuration/dotnet/. Arcade
# reads DOTNET_INSTALL_DIR first (use existing SDK if present); when
# nothing is found there, it installs into DOTNET_GLOBAL_INSTALL_DIR.
# Setting both to the same path makes the install idempotent.
export DOTNET_INSTALL_DIR="$repo_root/bin/$configuration/dotnet"
export DOTNET_GLOBAL_INSTALL_DIR="$DOTNET_INSTALL_DIR"
mkdir -p "$DOTNET_INSTALL_DIR"

# Don't fall back to a system dotnet that happens to match the pinned
# version; we always want the SDK in our own bin/ folder so the rest of
# the build picks it up via dotnet-local.{cmd,sh}.
use_installed_dotnet_cli=false

. "$scriptroot/common/tools.sh"

InitializeDotNetCli true
