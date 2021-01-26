@echo off
set NUNIT_VERSION=3.11.1
set PACKAGES_PATH=
set MYDIR=%~dp0

if defined NUGET_PACKAGES (
  set PACKAGES_PATH=%NUGET_PACKAGES%
  goto got_location
)
 
set NUGET_PATH=%MYDIR%..\..\.nuget\NuGet.exe
if exist "%NUGET_PATH%" (
  for /f "tokens=1,2,3 delims=:" %%a in ('"%NUGET_PATH%" locals -list global-packages') do set drive=%%b&set dir=%%c
  rem %drive% will contain a leading space, get rid of it
  set PACKAGES_PATH=%drive: =%:%dir%
  goto got_location
)

set PACKAGES_PATH="%userprofile%\.nuget\packages"

:got_location
"%PACKAGES_PATH%\nunit.consolerunner\%NUNIT_VERSION%\tools\nunit3-console.exe" %*

rem clean up - in cmd environment variables set in a batch file stay in the environment of the caller
set PACKAGES_PATH=
set MYDIR=
set NUNIT_VERSION=