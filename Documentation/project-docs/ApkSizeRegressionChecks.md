# apk size regression checks

We are checking the apk sizes for regression during CI builds.

The apk size information is collected by MSBuild tests. It is then
compared to reference `.apkdesc` files with `apkdiff` tool,
https://www.nuget.org/packages/apkdiff/. It compares
the size differences against reference sizes and fails when
they are larger than given thresholds. The test result file contains
details about apk size and apk entries size differences.

Note that the size decrease is also reported as regression. We
do that to keep the reference files up-to-date.

Also note that the new reference files need to be obtained
using Xamarin Android build, built with Release configuration.
The packages that are built with Xamarin Android built in Debug
configuration are bigger. They contain additional code
and some files are built with different optimizations.

# MSBuild tests

The `BuildReleaseArm64` test is used to collect apk size data.

The test builds a simple .NET for Android app and a simple .NET MAUI
app, for each supported runtime, so this gets us several variations
to check.

The reference files are located
in `src\Xamarin.Android.Build.Tasks\Tests\Xamarin.ProjectTools\Resources\Base`
directory. During the test run, we save `.apkdesc` files with
current sizes. These files can be used as a new reference. They
are named like this:

    .../Base/BuildReleaseArm64SimpleDotNet.CoreCLR.apkdesc
    .../Base/BuildReleaseArm64SimpleDotNet.MonoVM.apkdesc
    .../Base/BuildReleaseArm64SimpleDotNet.NativeAOT.apkdesc
    .../Base/BuildReleaseArm64XFormsDotNet.CoreCLR.apkdesc
    .../Base/BuildReleaseArm64XFormsDotNet.MonoVM.apkdesc
    .../Base/BuildReleaseArm64XFormsDotNet.NativeAOT.apkdesc

The new reference files can be obtained from the test results
archive - artifact of the given CI build (preferred method).
Or they can be obtained from local build using
the `build-tools/scripts/UpdateApkSizeReference.ps1` script
or the `build-tools/scripts/UpdateApkSizeReference.sh` script
if you are on MacOS or *nix.

The thresholds for these checks are set
in `src/Xamarin.Android.Build.Tasks/Tests/Xamarin.Android.Build.Tests/BuildTest2.cs`
in the `BuildReleaseArm64` method.

# How to resolve regression

* Check whether the size change is result of unwanted changes and
in such case fix the source of the regression. The test results
file contains `apkdiff` output with information about package and
entries size differences. That might help you locate the source
of the regression.

* If the size change is intended (for example size decrease as
result of optimization or reasonable increase after runtime
update/bump), the reference files need to be updated. The files
with current sizes are part of tests results archive in the artifacts
of the CI build.
