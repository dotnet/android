#!/bin/bash
./dotnet-local.sh test bin/TestRelease/net7.0/Xamarin.Android.Build.Tests.dll --filter=Name~BuildReleaseArm64
cp bin/TestRelease/BuildReleaseArm64*.apkdesc src/Xamarin.Android.Build.Tasks/Tests/Xamarin.ProjectTools/Resources/Base/