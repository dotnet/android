#!/bin/bash -e
msbuild /r
exec mono --debug ../../bin/Debug/bin/xat.exe "$@"
#exec dotnet run -- "$@"
