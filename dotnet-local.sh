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
    # Written when bin/obj were hydrated by `make prepare USE_MAIN_BUILD_CACHE=1`.
    if [[ -z "${CustomAfterMicrosoftCommonTargets+x}" && -f "${FULLROOT}/obj/azdo-main-build-cache/MainBuildCache.targets" ]] ; then
        export CustomAfterMicrosoftCommonTargets="${FULLROOT}/obj/azdo-main-build-cache/MainBuildCache.targets"
    fi
    exec "${ROOT}/bin/${config}/dotnet/dotnet" "$@"
done

echo "You need to run 'make prepare' first." >&2
exit 1
