@echo off
SET result = ""
FOR /F "tokens=1 delims=" %%F IN ('"C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe" -latest -property installationPath') DO SET result=%%F
if "%result%" == "" (
    FOR /F "tokens=1 delims=" %%F IN ('"C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe" -prerelease -latest -property installationPath') DO SET result=%%F
)
echo %result%