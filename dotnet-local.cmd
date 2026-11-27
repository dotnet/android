@echo off
SETLOCAL

IF NOT DEFINED CONFIGURATION SET CONFIGURATION=Debug
SET "DOTNETSDK_WORKLOAD_PACK_ROOTS=%~dp0bin\%CONFIGURATION%\lib"
IF EXIST "%~dp0bin\%CONFIGURATION%\workload\sdk-manifests" SET "DOTNETSDK_WORKLOAD_PACK_ROOTS=%~dp0bin\%CONFIGURATION%\workload"
SET "DOTNETSDK_WORKLOAD_MANIFEST_ROOTS=%DOTNETSDK_WORKLOAD_PACK_ROOTS%\sdk-manifests"
SET DOTNET_CLI_USE_MSBUILD_SERVER=0
IF NOT EXIST "%DOTNETSDK_WORKLOAD_MANIFEST_ROOTS%" (
    echo Build the local workload first: dotnetup dotnet build Microsoft.Android.slnx -c %CONFIGURATION% 1>&2
    EXIT /B 1
)
FOR %%c IN (exec --list-sdks --list-runtimes --additionalprobingpath --additional-deps --depsfile --fx-version --roll-forward --runtimeconfig) DO IF /I "%~1"=="%%c" GOTO run_host
IF /I "%~x1"==".dll" GOTO run_host
IF /I "%~x1"==".exe" GOTO run_host
REM Keep the root SDK pin while preserving the caller's global.json test settings.
SET "SDK_DIRECTORY="
PUSHD "%~dp0" || EXIT /B 1
FOR /F "delims=" %%i IN ('dotnetup dotnet -- msbuild build-tools/workload-tools/workload-tools.csproj -nologo -getProperty:MSBuildBinPath') DO SET "SDK_DIRECTORY=%%i"
POPD
IF NOT DEFINED SDK_DIRECTORY GOTO sdk_error
IF NOT EXIST "%SDK_DIRECTORY%\dotnet.dll" GOTO sdk_error
dotnetup dotnet -- exec "%SDK_DIRECTORY%\dotnet.dll" %*
EXIT /B %ERRORLEVEL%

:sdk_error
echo Failed to locate the repository-pinned .NET SDK in "%SDK_DIRECTORY%". 1>&2
EXIT /B 1

:run_host
dotnetup dotnet -- %*
EXIT /B %ERRORLEVEL%
