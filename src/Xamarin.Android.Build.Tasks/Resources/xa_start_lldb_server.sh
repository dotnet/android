#!/system/bin/sh

#
# A modified version of https://android.googlesource.com/platform/prebuilts/tools/+/e55133323a87d21dc5b73bc5539cc8522dc83f69/common/lldb/android/start_lldb_server.sh
#

# This script launches lldb-server on Android device from application subfolder - /data/data/$packageId/lldb/bin.
# Native run configuration is expected to push this script along with lldb-server to the device prior to its execution.
# Following command arguments are expected to be passed - lldb package directory and lldb-server listen port.

set -x

umask 0002

LLDB_DIR="$1"
LISTENER_SCHEME="$2"
DOMAINSOCKET_DIR="$3"
PLATFORM_SOCKET="$4"
LOG_CHANNELS="$5"
LLDB_ARCH="$6"

BIN_DIR="$LLDB_DIR/bin"
LOG_DIR="$LLDB_DIR/log"
TMP_DIR="$LLDB_DIR/tmp"
PLATFORM_LOG_FILE="$LOG_DIR/xa-${LLDB_ARCH}-platform.log"

export LLDB_DEBUGSERVER_LOG_FILE="$LOG_DIR/xa-${LLDB_ARCH}-lldb-server.log"
export LLDB_SERVER_LOG_CHANNELS="$LOG_CHANNELS"
export LLDB_DEBUGSERVER_DOMAINSOCKET_DIR="$DOMAINSOCKET_DIR"

# This directory already exists. Make sure it has the right permissions.
chmod 0775 "$LLDB_DIR"

rm -r "$TMP_DIR"
mkdir "$TMP_DIR"
export TMPDIR="$TMP_DIR"

rm -r "$LOG_DIR"
mkdir "$LOG_DIR"

# LLDB would create these files with more restrictive permissions than our umask above. Make sure
# it doesn't get a chance.
# "touch" does not exist on pre API-16 devices. This is a poor man's replacement
cat < /dev/null >"$LLDB_DEBUGSERVER_LOG_FILE" 2> "$PLATFORM_LOG_FILE"

cd "$TMP_DIR" # change cwd

exec "$BIN_DIR"/xa-${LLDB_ARCH}-lldb-server platform --server --listen "$LISTENER_SCHEME://$DOMAINSOCKET_DIR/$PLATFORM_SOCKET" --log-file "$PLATFORM_LOG_FILE" --log-channels "$LOG_CHANNELS" < /dev/null > "$LOG_DIR"/xa-${LLDB_ARCH}-platform-stdout.log 2>&1
