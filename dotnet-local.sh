#!/bin/bash
ROOT="$(dirname "${BASH_SOURCE}")"
FULLROOT="$(cd "${ROOT}"; pwd)"
if [[ -x "${ROOT}/bin/Release/dotnet/dotnet" ]]; then
    DOTNET_ROOT=${FULLROOT}/bin/Release/dotnet
    PATH=${DOTNET_ROOT}:PATH DOTNETSDK_WORKLOAD_MANIFEST_ROOTS=${FULLROOT}/bin/Release/lib/sdk-manifests DOTNETSDK_WORKLOAD_PACK_ROOTS=${FULLROOT}/bin/Release/lib exec ${ROOT}/bin/Release/dotnet/dotnet "$@"
elif [[ -x "${ROOT}/bin/Debug/dotnet/dotnet" ]]; then
    DOTNET_ROOT=${FULLROOT}/bin/Debug/dotnet
    PATH=${DOTNET_ROOT}:PATH DOTNETSDK_WORKLOAD_MANIFEST_ROOTS=${FULLROOT}/bin/Debug/lib/sdk-manifests DOTNETSDK_WORKLOAD_PACK_ROOTS=${FULLROOT}/bin/Debug/lib exec ${ROOT}/bin/Debug/dotnet/dotnet "$@"
else
    echo "You need to run 'make prepare' first."
fi