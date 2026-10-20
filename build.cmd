@echo off

FOR /F "tokens=1 delims=" %%F IN ('.\build-tools\scripts\vswhere.cmd') DO SET result=%%F
2>NUL CALL "%result%\Common7\Tools\VsDevCmd.bat"
IF ERRORLEVEL 1 CALL:FAILED_CASE

2>NUL CALL :%1_CASE
IF ERRORLEVEL 1 CALL :DEFAULT_CASE

:Prepare_CASE
    pwsh -NoProfile -ExecutionPolicy Bypass -File eng\install-dotnet.ps1
    IF ERRORLEVEL 1 GOTO END_CASE
    CALL dotnet-local.cmd build Microsoft.Android.slnx -t:Prepare -nodeReuse:false
    GOTO END_CASE
:Build_CASE
    dotnet-local.cmd build Microsoft.Android.slnx  -nodeReuse:false
    GOTO END_CASE
:Pack_CASE
    dotnet-local.cmd build  Microsoft.Android.slnx -t:PackDotNet -nodeReuse:false
    GOTO END_CASE
:DEFAULT_CASE
    pwsh -NoProfile -ExecutionPolicy Bypass -File eng\install-dotnet.ps1
    IF ERRORLEVEL 1 GOTO END_CASE
    CALL dotnet-local.cmd build Microsoft.Android.slnx -t:Prepare -nodeReuse:false
    IF ERRORLEVEL 1 GOTO END_CASE
    CALL dotnet-local.cmd build Microsoft.Android.slnx -nodeReuse:false
    IF ERRORLEVEL 1 GOTO END_CASE
    CALL dotnet-local.cmd build Microsoft.Android.slnx -t:PackDotNet -nodeReuse:false
    GOTO END_CASE
:FAILED_CASE
    echo "Failed to find an instance of Visual Studio. Please check it is correctly installed."
    GOTO END_CASE
:END_CASE
    GOTO :EOF
