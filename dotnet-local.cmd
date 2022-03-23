@echo off

IF EXIST "bin\Release\dotnet\dotnet.exe" (
    call "bin\Release\dotnet\dotnet.exe" %*
) ELSE (
    call "bin\Debug\dotnet\dotnet.exe" %*
)