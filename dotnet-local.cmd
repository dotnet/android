@echo off
SETLOCAL

SET ROOT=%~dp0

IF EXIST "%ROOT%\bin\Release\dotnet\dotnet.exe" (
    SET XA_CONFIG=Release
) ELSE IF EXIST "%ROOT%\bin\Debug\dotnet\dotnet.exe" (
    SET XA_CONFIG=Debug
) ELSE (
    echo "You need to run 'msbuild Xamarin.Android.sln /t:Prepare' first."
    goto :exit
)

SET XA_DOTNET_ROOT=%ROOT%\bin\%XA_CONFIG%\dotnet
SET PATH=%XA_DOTNET_ROOT%;%PATH%
SET DOTNETSDK_WORKLOAD_MANIFEST_ROOTS=%ROOT%\bin\%XA_CONFIG%\lib\sdk-manifests
SET DOTNETSDK_WORKLOAD_PACK_ROOTS=%ROOT%\bin\%XA_CONFIG%\lib

call "%XA_DOTNET_ROOT%\dotnet.exe" %*

:exit
