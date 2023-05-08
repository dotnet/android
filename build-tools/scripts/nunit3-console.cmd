@echo off
SETLOCAL
set NUNIT_VERSION=3.16.3
set PACKAGES_PATH=
set MYDIR=%~dp0

if defined NUGET_PACKAGES (
  set PACKAGES_PATH=%NUGET_PACKAGES%
  goto got_location
)

set NUGET_PATH=dotnet nuget
if exist "%NUGET_PATH%" (
  for /f "tokens=1,2" %%a in ('"%NUGET_PATH%" locals --list global-packages') do set PACKAGES_PATH=%%b
  goto got_location
)

set PACKAGES_PATH="%userprofile%\.nuget\packages"

:got_location
"%PACKAGES_PATH%\nunit.consolerunner\%NUNIT_VERSION%\tools\nunit3-console.exe" %*