#!/bin/bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")"; pwd)"
export DOTNETSDK_WORKLOAD_PACK_ROOTS="${ROOT}/bin/${CONFIGURATION:-Debug}/lib"
if [[ -d "${ROOT}/bin/${CONFIGURATION:-Debug}/workload/sdk-manifests" ]]; then
    export DOTNETSDK_WORKLOAD_PACK_ROOTS="${ROOT}/bin/${CONFIGURATION:-Debug}/workload"
fi
export DOTNETSDK_WORKLOAD_MANIFEST_ROOTS="${DOTNETSDK_WORKLOAD_PACK_ROOTS}/sdk-manifests"
export DOTNET_CLI_USE_MSBUILD_SERVER=0
[[ -d "${DOTNETSDK_WORKLOAD_MANIFEST_ROOTS}" ]] || { echo "Build the local workload first: dotnetup dotnet build Microsoft.Android.slnx -c ${CONFIGURATION:-Debug}" >&2; exit 1; }
exec dotnetup dotnet -- "$@"
