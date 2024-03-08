#!/bin/bash
ROOT="$(dirname "${BASH_SOURCE}")"
FULLROOT="$(cd "${ROOT}"; pwd)"
for config in Release Debug ; do
    if [[ ! -x "${ROOT}/bin/${config}/dotnet/dotnet" ]] ; then
        continue
    fi
    XA_DOTNET_ROOT="${FULLROOT}/bin/${config}/dotnet"
    export PATH="${XA_DOTNET_ROOT}:${PATH}"
    export DOTNETSDK_WORKLOAD_MANIFEST_ROOTS="${FULLROOT}/bin/${config}/lib/sdk-manifests"
    export DOTNETSDK_WORKLOAD_PACK_ROOTS="${FULLROOT}/bin/${config}/lib"
    exec "${ROOT}/bin/${config}/dotnet/dotnet" "$@"
done

echo "You need to run 'make prepare' first."
