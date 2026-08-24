@echo off
SETLOCAL

SET ROOT=%~dp0
SET XA_CONFIG=
SET XA_DOTNET_ROOT=

CALL :find_dotnet Release
IF DEFINED XA_CONFIG GOTO :dotnet_found
CALL :find_dotnet Debug
IF DEFINED XA_CONFIG GOTO :dotnet_found

echo "You need to run 'msbuild Xamarin.Android.slnx /t:Prepare' first."
GOTO :exit

:dotnet_found
SET PATH=%XA_DOTNET_ROOT%;%PATH%
SET DOTNETSDK_WORKLOAD_MANIFEST_ROOTS=%ROOT%\bin\%XA_CONFIG%\lib\sdk-manifests
SET DOTNETSDK_WORKLOAD_PACK_ROOTS=%ROOT%\bin\%XA_CONFIG%\lib

call "%XA_DOTNET_ROOT%\dotnet.exe" %*
GOTO :exit

:find_dotnet
SET XA_DOTNET_ROOT=
IF EXIST "%ROOT%\bin\%1\dotnet-install-location.txt" SET /P XA_DOTNET_ROOT=<"%ROOT%\bin\%1\dotnet-install-location.txt"
IF NOT DEFINED XA_DOTNET_ROOT SET XA_DOTNET_ROOT=%ROOT%\bin\%1\dotnet\
IF EXIST "%XA_DOTNET_ROOT%\dotnet.exe" SET XA_CONFIG=%1
EXIT /B

:exit
