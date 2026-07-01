#!/usr/bin/env bash
#
# Provisions the .NET SDK into bin/$Configuration/dotnet/.
#
# The SDK version is read from eng/Versions.props (single source of truth
# kept up to date by darc when Microsoft.NET.Sdk flows from dotnet/dotnet),
# so global.json does not need a 'tools.dotnet' pin.
#
# Inputs (env vars):
#   CONFIGURATION   - Debug (default) or Release; controls install path.
#

set -euo pipefail

scriptroot="$( cd -P "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
repo_root="$( cd -P "$scriptroot/.." && pwd )"

configuration="${CONFIGURATION:-Debug}"

versions_props="$repo_root/eng/Versions.props"
sdk_version="$(sed -n 's|.*<MicrosoftNETSdkPackageVersion>\([^<]*\)</MicrosoftNETSdkPackageVersion>.*|\1|p' "$versions_props" | head -n 1)"
if [[ -z "$sdk_version" ]]; then
  echo "error: could not read <MicrosoftNETSdkPackageVersion> from $versions_props" >&2
  exit 1
fi

install_dir="$repo_root/bin/$configuration/dotnet"
mkdir -p "$install_dir"

# Download Microsoft's official dotnet-install.sh (cached under
# $install_dir to avoid hitting the CDN on idempotent re-runs). Download
# to a temp file and atomically `mv` into place so a failed/interrupted
# download cannot poison the cache. Invoke via `bash` so the executable
# bit isn't needed (Windows clones often strip it).
install_script="$install_dir/dotnet-install.sh"
if [[ ! -f "$install_script" ]]; then
  install_script_tmp="$install_script.tmp.$$"
  curl -fsSL "https://builds.dotnet.microsoft.com/dotnet/scripts/v1/dotnet-install.sh" -o "$install_script_tmp"
  mv "$install_script_tmp" "$install_script"
fi

echo "Installing .NET SDK $sdk_version into $install_dir"
bash "$install_script" --version "$sdk_version" --install-dir "$install_dir" --no-path
