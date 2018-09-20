#!/bin/bash -e
export MONO_ENV_OPTIONS="--debug"
export USE_MSBUILD=1
export MSBUILD=msbuild
msbuild EmbeddedDSO-UnitTests.csproj
cd ../../../
exec mono --debug packages/NUnit.ConsoleRunner.3.7.0/tools/nunit3-console.exe bin/TestDebug/EmbeddedDSO/EmbeddedDSOUnitTests.dll
