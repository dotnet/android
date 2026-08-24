#!/bin/bash
ROOT="$(dirname "${BASH_SOURCE}")"
FULLROOT="$(cd "${ROOT}"; pwd)"
for config in Release Debug ; do
    install_location="${FULLROOT}/bin/${config}/dotnet-install-location.txt"
    if [[ -f "${install_location}" ]] ; then
        IFS= read -r XA_DOTNET_ROOT < "${install_location}"
    else
        XA_DOTNET_ROOT="${FULLROOT}/bin/${config}/dotnet"
    fi
    if [[ ! -x "${XA_DOTNET_ROOT}/dotnet" ]] ; then
        continue
    fi
    export PATH="${XA_DOTNET_ROOT}:${PATH}"
    export DOTNETSDK_WORKLOAD_MANIFEST_ROOTS="${FULLROOT}/bin/${config}/lib/sdk-manifests"
    export DOTNETSDK_WORKLOAD_PACK_ROOTS="${FULLROOT}/bin/${config}/lib"
    exec "${XA_DOTNET_ROOT}/dotnet" "$@"
done

echo "You need to run 'make prepare' first." >&2
exit 1
