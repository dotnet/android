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
case "${1:-}" in
    exec|*.dll|*.exe|--list-sdks|--list-runtimes|--additionalprobingpath|--additional-deps|--depsfile|--fx-version|--roll-forward|--runtimeconfig)
        exec dotnetup dotnet -- "$@"
        ;;
esac
# Keep the root SDK pin while preserving the caller's global.json test settings.
SDK_DIRECTORY="$(cd "$ROOT" && dotnetup dotnet -- msbuild build-tools/workload-tools/workload-tools.csproj -nologo -getProperty:MSBuildBinPath)"
[[ -f "${SDK_DIRECTORY}/dotnet.dll" ]] || { echo "Failed to locate the repository-pinned .NET SDK in '${SDK_DIRECTORY}'." >&2; exit 1; }
exec dotnetup dotnet -- exec "${SDK_DIRECTORY}/dotnet.dll" "$@"
