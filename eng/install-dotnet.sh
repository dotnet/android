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

# Retry a command up to 5 times with exponential backoff (2s, 4s, 8s, 16s).
# CI regularly hits transient network failures (e.g. "curl: (56) Recv
# failure: Connection reset by peer") when reaching the .NET CDN.
retry() {
  local attempt=1
  local max_attempts=5
  local delay=2
  while true; do
    if "$@"; then
      return 0
    fi
    if [[ $attempt -ge $max_attempts ]]; then
      echo "error: command failed after $attempt attempts: $*" >&2
      return 1
    fi
    echo "warning: command failed (attempt $attempt/$max_attempts), retrying in ${delay}s: $*" >&2
    sleep "$delay"
    attempt=$((attempt + 1))
    delay=$((delay * 2))
  done
}

# Download Microsoft's official dotnet-install.sh (cached under
# $install_dir to avoid hitting the CDN on idempotent re-runs). Download
# to a temp file and atomically `mv` into place so a failed/interrupted
# download cannot poison the cache. Invoke via `bash` so the executable
# bit isn't needed (Windows clones often strip it).
install_script="$install_dir/dotnet-install.sh"
if [[ ! -f "$install_script" ]]; then
  install_script_tmp="$install_script.tmp.$$"
  trap 'rm -f "$install_script_tmp"' EXIT
  retry curl -fSL --retry 5 --retry-delay 1 --retry-all-errors \
    "https://builds.dotnet.microsoft.com/dotnet/scripts/v1/dotnet-install.sh" -o "$install_script_tmp"
  mv "$install_script_tmp" "$install_script"
  trap - EXIT
fi

echo "Installing .NET SDK $sdk_version into $install_dir"
retry bash "$install_script" --version "$sdk_version" --install-dir "$install_dir" --no-path
