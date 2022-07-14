#!/bin/bash
./build-tools/scripts/nunit3-console bin/TestRelease/net472/Xamarin.Android.Build.Tests.dll --test=Xamarin.Android.Build.Tests.BuildTest2.BuildReleaseArm64
./dotnet-local.sh test bin/TestRelease/net6.0/Xamarin.Android.Build.Tests.dll --filter=Name~BuildReleaseArm64
cp bin/TestRelease/BuildReleaseArm64*.apkdesc src/Xamarin.Android.Build.Tasks/Tests/Xamarin.ProjectTools/Resources/Base/