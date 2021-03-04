# Run BuildReleaseArm64 tests and update the apkdesc reference files

if (-not (Test-Path bin/Release)) {
    Write-Output "bin/Release doesn't exists, please build XA in release configuration, before running this script"
    exit 1
}

Write-Output "Building xabuild"
msbuild /p:Configuration=Release /restore .\tools\xabuild\xabuild.csproj
Write-Output "Building legacy BuildReleaseArm64 tests"
msbuild /p:Configuration=Release Xamarin.Android.sln /t:RunNunitTests /p:TEST="Xamarin.Android.Build.Tests.BuildTest.BuildReleaseArm64"
Write-Output "Building DotNet BuildReleaseArm64 tests"
~\android-toolchain\dotnet\dotnet test -p:Configuration=Release --filter BuildTest.BuildReleaseArm64 .\bin\TestRelease\netcoreapp3.1\Xamarin.Android.Build.Tests.dll
Write-Output "Updating reference files"
Copy-Item -Verbose bin\TestRelease\BuildReleaseArm64*.apkdesc -Destination src\Xamarin.Android.Build.Tasks\Tests\Xamarin.ProjectTools\Resources\Base\
