# apk size regression checks

We are checking the apk sizes for regression in CI PR builds.
The `BuildReleaseArm64` test is used as the builds target both
legacy monodroid and NET6 frameworks. It also builds simple XA
app and XForms XA app. These are 4 important variations we
measure and check for size regression, with `apkdiff` tool.
https://www.nuget.org/packages/apkdiff/

When we detect regression, the test fails in CI build. The test
result file contains details about apk size and apk entries size
differences.

Note that the size decrease is also reported as regression. We
do that to keep the reference files up-to-date.

# How to resolve regression

* Check whether the size change is result of unwanted changes and
in this case fix the source of the regression.

* If the size change is intended (for example size decrease as
result of optimization or reasonable increase after runtime
update/bump), the reference files need to be updated.

The reference files are located
in `src\Xamarin.Android.Build.Tasks\Tests\Xamarin.ProjectTools\Resources\Base`
directory. During the test run, we save `.apkdesc` files, with
current sizes. Copy these files to the above mentioned directory
to become new reference.

This can be done for example in the powershell:

    Get-ChildItem -r -Filter Build*apkdesc .\bin\TestDebug\temp\BuildReleaseArm64* | Copy-Item -Destination src\Xamarin.Android.Build.Tasks\Tests\Xamarin.ProjectTols\Resources\Base\
