#!/bin/bash
if [ -d "bin/Release/dotnet" ]; then
    ./bin/Release/dotnet/dotnet $@
else
    ./bin/Debug/dotnet/dotnet $@
fi