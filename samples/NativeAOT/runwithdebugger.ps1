
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

# Build and install
# & $DOTNET_LOCAL build $SCRIPT_DIR\NativeAOT.csproj -c Debug -t:Install -tl:off

# This script is used to run the NativeAOT sample with the debugger attached.
# It is used by the CI system to verify that the debugger works with NativeAOT.

# Kill any existing lldb-server processes
adb shell "run-as net.dot.hellonativeaot killall -9 lldb-server" 2>$null
if ($LASTEXITCODE -ne 0) { 
    Write-Host "No existing lldb-server process found or couldn't kill it. Continuing..."
}

# Get the appropriate path for Windows NDK
$NDK_LLDB_PATH = "$env:ANDROID_NDK_HOME\toolchains\llvm\prebuilt\windows-x86_64\lib\clang\18\lib\linux\aarch64\lldb-server"
if (-not (Test-Path $NDK_LLDB_PATH)) {
    $NDK_LLDB_PATH = "$env:ANDROID_NDK_HOME\toolchains\llvm\prebuilt\linux-x86_64\lib\clang\18\lib\linux\aarch64\lldb-server"
    if (-not (Test-Path $NDK_LLDB_PATH)) {
        $NDK_LLDB_PATH = "$env:ANDROID_NDK_HOME\toolchains\llvm\prebuilt\darwin-x86_64\lib\clang\18\lib\linux\aarch64\lldb-server"
        if (-not (Test-Path $NDK_LLDB_PATH)) {
            Write-Error "Could not find lldb-server in any of the expected NDK locations"
            exit 1
        }
    }
}

# Push lldb-server to device
adb push $NDK_LLDB_PATH /data/local/tmp/lldb-server
adb shell run-as net.dot.hellonativeaot cp /data/local/tmp/lldb-server .
adb forward tcp:5039 tcp:5039

# Start lldb-server in background
$job = Start-Job -ScriptBlock {
    adb shell run-as net.dot.hellonativeaot ./lldb-server platform --listen "*:5039" --server
}

# Launch the app with debug flag
adb shell am start -S --user "0" -a "android.intent.action.MAIN" -c "android.intent.category.LAUNCHER" -n "net.dot.hellonativeaot/my.MainActivity" -D
Write-Host "Waiting for the app to start..."
Start-Sleep -Seconds 2

# Get process info and extract the PID
$APP_PROCESS_INFO = adb shell ps | Select-String "net.dot.hellonativeaot"
Write-Host "Process info: $APP_PROCESS_INFO"
$APP_PID = [regex]::Match($APP_PROCESS_INFO, "\s+(\d+)\s+").Groups[1].Value

# Verify we got a valid PID
if (-not $APP_PID) {
    Write-Error "Error: Could not find process ID for net.dot.hellonativeaot"
    exit 1
}
Write-Host "Found process ID: $APP_PID"

# Set up JDWP forwarding using the extracted PID
adb forward --remove tcp:8700 2>$null
adb forward tcp:8700 jdwp:$APP_PID

# Connect with JDB and send quit command
$null = New-Item -ItemType File -Path "$env:TEMP\jdb_commands.txt" -Value "quit" -Force
Get-Content "$env:TEMP\jdb_commands.txt" | jdb -attach localhost:8700

# Clean up
Remove-Item "$env:TEMP\jdb_commands.txt" -ErrorAction SilentlyContinue
Stop-Job -Job $job
Remove-Job -Job $job
