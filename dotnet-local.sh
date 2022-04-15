#!/bin/bash
ROOT=$(dirname "${BASH_SOURCE}")
if [[ -x "${ROOT}/bin/Release/dotnet/dotnet" ]]; then
    exec ${ROOT}/bin/Release/dotnet/dotnet "$@"
elif [[ -x "${ROOT}/bin/Debug/dotnet/dotnet" ]]; then
    exec ${ROOT}/bin/Debug/dotnet/dotnet "$@"
else
    echo "You need to run 'make prepare' first."
fi