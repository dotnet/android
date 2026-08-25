@echo off
SETLOCAL

SET ROOT=%~dp0
SET XA_CONFIG=
SET XA_DOTNET_ROOT=
SET XA_DOTNET_SDK_VERSION=
SET XA_USING_SHARED_DOTNET=
SET XA_RUNNING_ON_CI=

IF DEFINED TF_BUILD SET XA_RUNNING_ON_CI=true
IF DEFINED GITHUB_ACTIONS SET XA_RUNNING_ON_CI=true
IF DEFINED CI SET XA_RUNNING_ON_CI=true

FOR /F "tokens=2 delims=<>" %%V IN ('findstr /C:"<MicrosoftNETSdkPackageVersion>" "%ROOT%\eng\Versions.props"') DO SET XA_DOTNET_SDK_VERSION=%%V

CALL :find_dotnet Release
IF DEFINED XA_CONFIG GOTO :dotnet_found
CALL :find_dotnet Debug
IF DEFINED XA_CONFIG GOTO :dotnet_found

echo "You need to run 'msbuild Xamarin.Android.slnx /t:Prepare' first."
GOTO :exit

:dotnet_found
SET PATH=%XA_DOTNET_ROOT%;%PATH%
IF DEFINED XA_USING_SHARED_DOTNET CALL :configure_shared_dotnet
SET DOTNETSDK_WORKLOAD_MANIFEST_ROOTS=%ROOT%\bin\%XA_CONFIG%\lib\sdk-manifests
SET DOTNETSDK_WORKLOAD_PACK_ROOTS=%ROOT%\bin\%XA_CONFIG%\lib

call "%XA_DOTNET_ROOT%\dotnet.exe" %*
GOTO :exit

:find_dotnet
SET XA_DOTNET_ROOT=
IF NOT DEFINED XA_RUNNING_ON_CI IF EXIST "%ROOT%\bin\%1\dotnet-install-location.txt" SET /P XA_DOTNET_ROOT=<"%ROOT%\bin\%1\dotnet-install-location.txt"
IF EXIST "%XA_DOTNET_ROOT%\dotnet.exe" IF EXIST "%XA_DOTNET_ROOT%\sdk\%XA_DOTNET_SDK_VERSION%\" SET XA_USING_SHARED_DOTNET=true
IF NOT DEFINED XA_USING_SHARED_DOTNET SET XA_DOTNET_ROOT=%ROOT%\bin\%1\dotnet\
IF EXIST "%XA_DOTNET_ROOT%\dotnet.exe" SET XA_CONFIG=%1
EXIT /B

:configure_shared_dotnet
IF NOT DEFINED NUGET_PACKAGES SET NUGET_PACKAGES=%USERPROFILE%\.nuget\packages
IF "%NUGET_PACKAGES:~-1%"=="\" GOTO :shared_dotnet_configured
IF "%NUGET_PACKAGES:~-1%"=="/" GOTO :shared_dotnet_configured
SET NUGET_PACKAGES=%NUGET_PACKAGES%\
:shared_dotnet_configured
SET DOTNET_CLI_HOME=%ROOT%\bin\%XA_CONFIG%\dotnet-home
EXIT /B

:exit
