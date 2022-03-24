@echo off

IF EXIST "bin\Release\dotnet\dotnet.exe" (
    call "bin\Release\dotnet\dotnet.exe" %*
) ELSE IF EXIST "bin\Debug\dotnet\dotnet.exe" (
    call "bin\Debug\dotnet\dotnet.exe" %*
) ELSE (
    echo "You need to run 'msbuild Xamarin.Android.sln /t:Prepare' first."
)