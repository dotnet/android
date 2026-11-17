#!/bin/bash
./dotnet-local.sh test --test-modules bin/TestRelease/net11.0/Xamarin.Android.Build.Tests.dll --filter Name~BuildReleaseArm64
cp bin/TestRelease/BuildReleaseArm64*.apkdesc src/Xamarin.Android.Build.Tasks/Tests/Xamarin.ProjectTools/Resources/Base/