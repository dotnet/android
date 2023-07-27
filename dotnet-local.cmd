@echo off
SET ROOT=%~dp0
IF EXIST "%ROOT%\bin\Release\dotnet\dotnet.exe" (
    SET DOTNET_ROOT=%ROOT%\bin\Release\dotnet
    SET DOTNETSDK_WORKLOAD_MANIFEST_ROOTS=%ROOT%\bin\Release\lib\sdk-manifests
    SET DOTNETSDK_WORKLOAD_PACK_ROOTS=%ROOT%\bin\Release\lib
    SET PATH=%DOTNET_ROOT%;%PATH%
    call "%ROOT%\bin\Release\dotnet\dotnet.exe" %*
) ELSE IF EXIST "%ROOT%\bin\Debug\dotnet\dotnet.exe" (
    SET DOTNET_ROOT=%ROOT%\bin\Debug\dotnet
    SET DOTNETSDK_WORKLOAD_MANIFEST_ROOTS=%ROOT%\bin\Debug\lib\sdk-manifests
    SET DOTNETSDK_WORKLOAD_PACK_ROOTS=%ROOT%\bin\Debug\lib
    SET PATH=%DOTNET_ROOT%;%PATH%
    call "%ROOT%\bin\Debug\dotnet\dotnet.exe" %*
) ELSE (
    echo "You need to run 'msbuild Xamarin.Android.sln /t:Prepare' first."
)