#!/bin/bash -e
export MONO_ENV_OPTIONS="--debug"
export USE_MSBUILD=1
export MSBUILD=msbuild
CONFIGURATION=${1:-Debug}
msbuild /p:Configuration=${CONFIGURATION} EmbeddedDSO-UnitTests.csproj
cd ../../../
exec mono --debug packages/NUnit.ConsoleRunner.3.9.0/tools/nunit3-console.exe bin/Test${CONFIGURATION}/EmbeddedDSO/EmbeddedDSOUnitTests.dll
