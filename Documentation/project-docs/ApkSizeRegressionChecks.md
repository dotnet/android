# apk size regression checks

We are checking the apk sizes for regression in CI PR builds.
The `BuildReleaseArm64` test is used to collect apk size data.

The test builds simple Xamarin Android and simple Xamarin Forms
on Xamarin Android apps. We build it targeting legacy and NET6
framworks, so this get us 4 variations to check.

The measurements and checks for size regression are done
with `apkdiff` tool. https://www.nuget.org/packages/apkdiff/

When we detect regression, the test fails in CI build and
the result file contains details about apk size and apk entries
size differences.

Note that the size decrease is also reported as regression. We
do that to keep the reference files up-to-date.

# How to resolve regression

* Check whether the size change is result of unwanted changes and
in such case fix the source of the regression.

* If the size change is intended (for example size decrease as
result of optimization or reasonable increase after runtime
update/bump), the reference files need to be updated.

The new reference files can be obtained from the test results
archive - artifact of the CI build. Or they can be obtained
from local build using the `build-tools/scripts/UpdateApkSizeReference.ps1`
script.

The reference files itself are located
in `src\Xamarin.Android.Build.Tasks\Tests\Xamarin.ProjectTools\Resources\Base`
directory. During the test run, we save `.apkdesc` files, with
current sizes. These files can be used a new reference. The 4 files
are named like this:

    .../Base/BuildReleaseArm64SimpleDotNet.apkdesc
    .../Base/BuildReleaseArm64SimpleLegacy.apkdesc
    .../Base/BuildReleaseArm64XFormsDotNet.apkdesc
    .../Base/BuildReleaseArm64XFormsLegacy.apkdesc

Note that the new reference files need to be obtained
from Xamarin Android build, built with Release configuration.
