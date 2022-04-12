@echo off
SET ROOT=%~dp0
IF EXIST "%ROOT%\bin\Release\dotnet\dotnet.exe" (
    call "%ROOT%\bin\Release\dotnet\dotnet.exe" %*
) ELSE IF EXIST "%ROOT%\bin\Debug\dotnet\dotnet.exe" (
    call "%ROOT%\bin\Debug\dotnet\dotnet.exe" %*
) ELSE (
    echo "You need to run 'msbuild Xamarin.Android.sln /t:Prepare' first."
)