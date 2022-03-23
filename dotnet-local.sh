#!/bin/bash
if [[ -x "bin/Release/dotnet/dotnet" ]]; then
    exec ./bin/Release/dotnet/dotnet "$@"
elif [[ -x "bin/Debug/dotnet/dotnet" ]]; then
    exec ./bin/Debug/dotnet/dotnet "$@"
else
    echo "You need to run 'make prepare' first."
fi