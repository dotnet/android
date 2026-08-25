#!/usr/bin/env bash
#
# Provisions the .NET SDK into bin/$Configuration/dotnet/ by default.
#
# The SDK version is read from eng/Versions.props (single source of truth
# kept up to date by darc when Microsoft.NET.Sdk flows from dotnet/dotnet),
# so global.json does not need a 'tools.dotnet' pin.
#
# Inputs (env vars):
#   CONFIGURATION        - Debug (default) or Release; controls checkout output.
#   XA_DOTNET_SHARED_INSTALL_BASE - Optional shared base directory. The SDK is
#                                   installed under <base>/<sdk-version>/.
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

sdk_version_core="${sdk_version%%-*}"
IFS=. read -r sdk_major sdk_minor sdk_patch _ <<< "$sdk_version_core"
if [[ ! "$sdk_major" =~ ^[0-9]+$ || ! "$sdk_minor" =~ ^[0-9]+$ || ! "$sdk_patch" =~ ^[0-9]+$ ]]; then
  echo "error: could not determine the SDK feature band from '$sdk_version'" >&2
  exit 1
fi
sdk_feature_band="$sdk_major.$sdk_minor.$((10#$sdk_patch / 100 * 100))"

use_shared_install=false
if [[ -n "${XA_DOTNET_SHARED_INSTALL_BASE:-}" && -z "${TF_BUILD:-}" && -z "${GITHUB_ACTIONS:-}" && -z "${CI:-}" ]]; then
  use_shared_install=true
  if [[ "$XA_DOTNET_SHARED_INSTALL_BASE" = /* ]]; then
    install_base="$XA_DOTNET_SHARED_INSTALL_BASE"
  else
    install_base="$repo_root/$XA_DOTNET_SHARED_INSTALL_BASE"
  fi
  mkdir -p "$install_base"
  install_base="$(cd -P "$install_base" && pwd)"
  install_dir="$install_base/$sdk_version"
else
  install_dir="$repo_root/bin/$configuration/dotnet"
fi
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

install_location_file="$repo_root/bin/$configuration/dotnet-install-location.txt"
mkdir -p "$(dirname "$install_location_file")"
if [[ "$use_shared_install" == true ]]; then
  # Keep workload packs, manifests, and installation records outside the shared SDK.
  userlocal_marker="$install_dir/metadata/workloads/$sdk_feature_band/userlocal"
  if [[ ! -f "$userlocal_marker" ]]; then
    mkdir -p "$(dirname "$userlocal_marker")"
    : > "$userlocal_marker"
  fi
  printf '%s/\n' "${install_dir%/}" > "$install_location_file"
else
  rm -f "$install_location_file"
fi
