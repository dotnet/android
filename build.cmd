@echo off

FOR /F "tokens=1 delims=" %%F IN ('.\build-tools\scripts\vswhere.cmd') DO SET result=%%F
2>NUL CALL "%result%\Common7\Tools\VsDevCmd.bat"
IF ERRORLEVEL 1 CALL:FAILED_CASE

2>NUL CALL :%1_CASE
IF ERRORLEVEL 1 CALL :DEFAULT_CASE

:Prepare_CASE
    msbuild Xamarin.Android.sln /t:Prepare
    GOTO END_CASE
:PrepareExternal_CASE
    msbuild Xamarin.Android.sln /t:PrepareExternal
    GOTO END_CASE
:Build_CASE
    msbuild Xamarin.Android.sln
    msbuild tools/xabuild/xabuild.csproj
    GOTO END_CASE
:Pack_CASE
    msbuild Xamarin.Android.sln /t:PackDotNet
    GOTO END_CASE
:DEFAULT_CASE
    msbuild Xamarin.Android.sln /t:Prepare
    msbuild Xamarin.Android.sln
    msbuild tools/xabuild/xabuild.csproj
    msbuild Xamarin.Android.sln /t:PackDotNet
    GOTO END_CASE
:FAILED_CASE
    echo "Failed to find an instance of Visual Studio. Please check it is correctly installed."
    GOTO END_CASE
:END_CASE
    GOTO :EOF