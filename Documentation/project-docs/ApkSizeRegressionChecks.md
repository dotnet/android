# apk size regression checks

We are checking the apk sizes for regression during CI builds.

The apk size information is collected in 2 places, in APK
instrumentation tests and MSBuild tests. It is then compared to
reference `.apkdesc` files with `apkdiff` tool,
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

The test builds simple Xamarin Android and simple Xamarin Forms
on Xamarin Android apps. We build it targeting legacy and NET6
framworks, so this get us 4 variations to check.

The reference files are located
in `src\Xamarin.Android.Build.Tasks\Tests\Xamarin.ProjectTools\Resources\Base`
directory. During the test run, we save `.apkdesc` files, with
current sizes. These files can be used a new reference. The 4 files
are named like this:

    .../Base/BuildReleaseArm64SimpleDotNet.apkdesc
    .../Base/BuildReleaseArm64SimpleLegacy.apkdesc
    .../Base/BuildReleaseArm64XFormsDotNet.apkdesc
    .../Base/BuildReleaseArm64XFormsLegacy.apkdesc

The new reference files can be obtained from the test results
archive - artifact of the given CI build (preferred method).
Or they can be obtained from local build using
the `build-tools/scripts/UpdateApkSizeReference.ps1` script
or the `build-tools/scripts/UpdateApkSizeReference.sh` script
if you are on MacOS or *nix.

The thresholds for these checks are set
in `src/Xamarin.Android.Build.Tasks/Tests/Xamarin.Android.Build.Tests/BuildTest.cs`
in `BuildReleaseArm64` method.

# APK instrumentation tests

2 instrumentation tests are used to collect apk size data,
`tests\Xamarin.Forms-Performance-Integration` and
`samples\VSAndroidApp` test apps.

The reference file are located in `tests/apk-sizes-reference` directory.

    com.companyname.vsandroidapp-Signed-Release.apkdesc
    Xamarin.Forms_Performance_Integration-Signed-Release.apkdesc
    Xamarin.Forms_Performance_Integration-Signed-Release-Aot.apkdesc
    Xamarin.Forms_Performance_Integration-Signed-Release-Bundle.apkdesc
    Xamarin.Forms_Performance_Integration-Signed-Release-Profiled-Aot.apkdesc

The thresholds for these checks are set
in `build-tools/Xamarin.Android.Tools.BootstrapTasks/Xamarin.Android.Tools.BootstrapTasks/ApkDiffCheckRegression.cs`
in fields of `ApkDiffCheckRegression` class.

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
