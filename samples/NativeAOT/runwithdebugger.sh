#!/bin/bash

# Set error handling
set -e

# Get the directory where the script is located
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../../" && pwd)"

# Use full path to dotnet-local.sh and ensure it's executable
DOTNET_LOCAL="${REPO_ROOT}/dotnet-local.sh"
if [ ! -f "${DOTNET_LOCAL}" ]; then
    echo "Error: dotnet-local.sh not found at ${DOTNET_LOCAL}"
    exit 1
fi
chmod +x "${DOTNET_LOCAL}"

# Build and install
"${DOTNET_LOCAL}" build ${SCRIPT_DIR}/NativeAOT.csproj -c Debug -t:Install -tl:off

# This script is used to run the NativeAOT sample with the debugger attached.
# It is used by the CI system to verify that the debugger works with NativeAOT.
adb shell run-as net.dot.hellonativeaot killall -9 lldb-server > /dev/null 2>&1 || true
adb push $ANDROID_NDK_HOME/toolchains/llvm/prebuilt/darwin-x86_64/lib/clang/18/lib/linux/aarch64/lldb-server /data/local/tmp/lldb-server
adb shell run-as net.dot.hellonativeaot cp /data/local/tmp/lldb-server .
adb forward tcp:5039 tcp:5039

# Use nohup to make the process ignore hangup signals and disown to remove it from job control
nohup adb shell run-as net.dot.hellonativeaot ./lldb-server platform --listen "*:5039" --server > /dev/null 2>&1 &
disown

adb shell am start -S --user "0" -a "android.intent.action.MAIN" -c "android.intent.category.LAUNCHER" -n "net.dot.hellonativeaot/my.MainActivity" -D
echo "Waiting for the app to start..."
sleep 2
# Get process info and extract the PID
APP_PROCESS_INFO=$(adb shell ps | grep net.dot.hellonativeaot)
echo "Process info: $APP_PROCESS_INFO"
APP_PID=$(echo "$APP_PROCESS_INFO" | awk '{print $2}')

# Verify we got a valid PID
if [ -z "$APP_PID" ]; then
    echo "Error: Could not find process ID for net.dot.hellonativeaot"
    exit 1
fi
echo "Found process ID: $APP_PID"#

# Set up JDWP forwarding using the extracted PID
adb forward --remove tcp:8700 || true
adb forward tcp:8700 jdwp:$APP_PID

echo "process attach --pid $APP_PID" > ${SCRIPT_DIR}/obj/Debug/lldbattach

# Connect with JDB and send quit command
#echo "quit" | jdb -attach localhost:8700