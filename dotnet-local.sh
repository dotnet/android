#!/bin/bash
ROOT="$(dirname "${BASH_SOURCE}")"
FULLROOT="$(cd "${ROOT}"; pwd)"
sdk_version="$(sed -n 's|.*<MicrosoftNETSdkPackageVersion>\([^<]*\)</MicrosoftNETSdkPackageVersion>.*|\1|p' "${FULLROOT}/eng/Versions.props" | head -n 1)"
for config in Release Debug ; do
    XA_USING_SHARED_DOTNET=false
    install_location="${FULLROOT}/bin/${config}/dotnet-install-location.txt"
    if [[ -f "${install_location}" ]] ; then
        IFS= read -r XA_DOTNET_ROOT < "${install_location}"
        if [[ -x "${XA_DOTNET_ROOT}/dotnet" && -d "${XA_DOTNET_ROOT}/sdk/${sdk_version}" ]] ; then
            XA_USING_SHARED_DOTNET=true
        fi
    fi
    if [[ "$XA_USING_SHARED_DOTNET" != true ]] ; then
        XA_DOTNET_ROOT="${FULLROOT}/bin/${config}/dotnet"
    fi
    if [[ ! -x "${XA_DOTNET_ROOT}/dotnet" ]] ; then
        continue
    fi
    export PATH="${XA_DOTNET_ROOT}:${PATH}"
    if [[ "$XA_USING_SHARED_DOTNET" == true ]] ; then
        NUGET_PACKAGES="${NUGET_PACKAGES:-${HOME}/.nuget/packages}"
        export NUGET_PACKAGES="${NUGET_PACKAGES%/}/"
        export DOTNET_CLI_HOME="${FULLROOT}/bin/${config}/dotnet-home"
    fi
    export DOTNETSDK_WORKLOAD_MANIFEST_ROOTS="${FULLROOT}/bin/${config}/lib/sdk-manifests"
    export DOTNETSDK_WORKLOAD_PACK_ROOTS="${FULLROOT}/bin/${config}/lib"
    exec "${XA_DOTNET_ROOT}/dotnet" "$@"
done

echo "You need to run 'make prepare' first." >&2
exit 1
