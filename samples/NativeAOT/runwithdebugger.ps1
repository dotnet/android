# Set error handling
$ErrorActionPreference = "Stop"

# Get the directory where the script is located and repo root
$SCRIPT_DIR = Split-Path -Parent $MyInvocation.MyCommand.Path
$REPO_ROOT = (Get-Item "$SCRIPT_DIR/../../").FullName

# Use full path to dotnet-local.cmd and ensure it exists
$DOTNET_LOCAL = "$REPO_ROOT\dotnet-local.cmd"
if (-not (Test-Path $DOTNET_LOCAL)) {
    Write-Error "Error: dotnet-local.cmd not found at $DOTNET_LOCAL"
    exit 1
}

$ADB = "$env:ANDROID_HOME\platform-tools\adb.exe"
if (-not (Test-Path $ADB)) {
    Write-Error "Could not find adb.exe in any of the expected SDK locations"
    exit 1
}

$null = & $ADB devices

$_DEF_NDK_HOME= & $DOTNET_LOCAL build -getProperty:AndroidNdkFullPath "$REPO_ROOT\build-tools\scripts\Paths.targets"
if (-not $_DEF_NDK_HOME) {
    Write-Error "Error: Could not find Android NDK path"
    exit 1
}

$env:ANDROID_NDK_HOME = $_DEF_NDK_HOME

# Build and install
& $DOTNET_LOCAL build $SCRIPT_DIR\NativeAOT.csproj -c Debug -t:Install -tl:off

# This script is used to run the NativeAOT sample with the debugger attached.
# It is used by the CI system to verify that the debugger works with NativeAOT.

# Kill any existing lldb-server processes
$null = & $ADB shell "run-as net.dot.hellonativeaot killall -q -9 lldb-server" 

$DEVICE_ARCH = & $ADB shell uname -m
if ( $DEVICE_ARCH -match "aarch64" ) {
    $CLANG_PREFIX=aarch64-linux-android21
} elseif ( $DEVICE_ARCH -match "x86_64" ) {
    $CLANG_PREFIX=x86_64-linux-android21
} else {
    Write-Error "Error: unsupported device architecture $DEVICE_ARCH"
    exit 1
}

# Get the appropriate path for Windows NDK
$NDK_CLANG_PATH = "$env:ANDROID_NDK_HOME\toolchains\llvm\prebuilt\windows-x86_64\bin\$CLANG_PREFIX-clang"
if (-not (Test-Path $NDK_CLANG_PATH)) {
    $NDK_CLANG_PATH = "$env:ANDROID_NDK_HOME\toolchains\llvm\prebuilt\linux-x86_64\bin\$CLANG_PREFIX-clang"
    if (-not (Test-Path $NDK_CLANG_PATH)) {
        $NDK_CLANG_PATH = "$env:ANDROID_NDK_HOME\toolchains\llvm\prebuilt\darwin-x86_64\bin\$CLANG_PREFIX-clang"
        if (-not (Test-Path $NDK_CLANG_PATH)) {
            Write-Error "Could not find clang in any of the expected NDK locations"
            exit 1
        }
    }
}

$NDK_LLDB_PATH= & $NDK_CLANG_PATH -print-file-name=lldb-server
# Push lldb-server to device
& $ADB push "$NDK_LLDB_PATH" /data/local/tmp/lldb-server
& $ADB shell run-as net.dot.hellonativeaot cp /data/local/tmp/lldb-server .
& $ADB forward tcp:5039 tcp:5039

# Start lldb-server in background
$job = Start-Job -ScriptBlock {
    adb shell run-as net.dot.hellonativeaot ./lldb-server platform --listen "*:5039" --server
}

# Launch the app with debug flag
& $ADB shell am start -S --user "0" -a "android.intent.action.MAIN" -c "android.intent.category.LAUNCHER" -n "net.dot.hellonativeaot/my.MainActivity" -D
Write-Host "Waiting for the app to start..."
Start-Sleep -Seconds 2

# Get process info and extract the PID
$APP_PROCESS_INFO = & $ADB shell ps | Select-String "net.dot.hellonativeaot"
if (-not $APP_PROCESS_INFO) {
    Write-Error "Error: Could not find a running process for net.dot.hellonativeaot"
    exit 1
}
Write-Host "Process info: $APP_PROCESS_INFO"
$APP_PID = [regex]::Match($APP_PROCESS_INFO, "\s+(\d+)\s+").Groups[1].Value

# Verify we got a valid PID
if (-not $APP_PID) {
    Write-Error "Error: Could not find process ID for net.dot.hellonativeaot output was not correct"
    exit 1
}
Write-Host "Found process ID: $APP_PID"

$FORWARDS = & $ADB forward --list
if ($FORWARDS -match "tcp:8700") {
    $null = & $ADB forward --remove tcp:8700
}

# Set up JDWP forwarding using the extracted PID
$null = & $ADB forward tcp:8700 jdwp:$APP_PID

# Create the lldbattach file with the process attach command
$lldbattachPath = Join-Path -Path $SCRIPT_DIR -ChildPath "obj/Debug/lldbattach"
$attachCommand = "process attach --pid $APP_PID"
Set-Content -Path $lldbattachPath -Value $attachCommand

# Connect with JDB and send quit command
#$null = New-Item -ItemType File -Path "$env:TEMP\jdb_commands.txt" -Value "quit" -Force
#Get-Content "$env:TEMP\jdb_commands.txt" | jdb -attach localhost:8700

# Clean up
#Remove-Item "$env:TEMP\jdb_commands.txt" -ErrorAction SilentlyContinue
#Stop-Job -Job $job
#Remove-Job -Job $job
