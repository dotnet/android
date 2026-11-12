# Run BuildReleaseArm64 tests and update the apkdesc reference files

if (-not (Test-Path bin/Release)) {
    Write-Output "bin/Release doesn't exists, please build XA in release configuration, before running this script"
    exit 1
}

Write-Output "Building BuildReleaseArm64 tests"
bin\Release\dotnet\dotnet test .\bin\TestRelease\net11.0\Xamarin.Android.Build.Tests.dll --filter Name~BuildReleaseArm64
Write-Output "Updating reference files"
Copy-Item -Verbose bin\TestRelease\BuildReleaseArm64*.apkdesc -Destination src\Xamarin.Android.Build.Tasks\Tests\Xamarin.ProjectTools\Resources\Base\
