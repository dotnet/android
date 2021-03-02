Write-Output "Building xabuild"
msbuild /restore .\tools\xabuild\xabuild.csproj
Write-Output "Building legacy BuildReleaseArm64 tests"
msbuild Xamarin.Android.sln /t:RunNunitTests /p:TEST="Xamarin.Android.Build.Tests.BuildTest.BuildReleaseArm64"
Write-Output "Building DotNet BuildReleaseArm64 tests"
~\android-toolchain\dotnet\dotnet test -v diag --filter BuildTest.BuildReleaseArm64 .\bin\TestDebug\netcoreapp3.1\Xamarin.Android.Build.Tests.dll
Write-Output "Updating reference files"
Copy-Item -Verbose bin\TestDebug\BuildReleaseArm64*.apkdesc -Destination src\Xamarin.Android.Build.Tasks\Tests\Xamarin.ProjectTools\Resources\Base\
