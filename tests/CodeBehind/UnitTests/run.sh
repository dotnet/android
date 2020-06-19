#!/bin/bash -e
export MONO_ENV_OPTIONS="--debug"
export USE_MSBUILD=1
export MSBUILD=msbuild
msbuild CodeBehindUnitTests.csproj
cd ../../../
exec mono --debug packages/nunit.consolerunner/3.11.1/tools/nunit3-console.exe bin/TestDebug/CodeBehind/CodeBehindUnitTests.dll
