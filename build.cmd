@echo off

FOR /F "tokens=1 delims=" %%F IN ('.\build-tools\scripts\vswhere.cmd') DO SET result=%%F
2>NUL CALL "%result%\Common7\Tools\VsDevCmd.bat"
IF ERRORLEVEL 1 CALL:FAILED_CASE

2>NUL CALL :%1_CASE
IF ERRORLEVEL 1 CALL :DEFAULT_CASE

:Prepare_CASE
    dotnet build Xamarin.Android.sln -t:Prepare -nr:false
    GOTO END_CASE
:PrepareExternal_CASE
    dotnet build Xamarin.Android.sln -t:PrepareExternal -nr:false
    GOTO END_CASE
:Build_CASE
    dotnet-local.cmd build Xamarin.Android.sln  -nr:false
    dotnet-local.cmd build tools/xabuild/xabuild.csproj -nr:false
    GOTO END_CASE
:Pack_CASE
    dotnet-local.cmd build  Xamarin.Android.sln -t:PackDotNet -nr:false
    GOTO END_CASE
:DEFAULT_CASE
    dotnet build Xamarin.Android.sln -t:Prepare -nr:false
    dotnet-local.cmd build Xamarin.Android.sln -nr:false
    dotnet-local.cmd build tools/xabuild/xabuild.csproj -nr:false
    dotnet-local.cmd build Xamarin.Android.sln -t:PackDotNet -nr:false
    GOTO END_CASE
:FAILED_CASE
    echo "Failed to find an instance of Visual Studio. Please check it is correctly installed."
    GOTO END_CASE
:END_CASE
    GOTO :EOF
