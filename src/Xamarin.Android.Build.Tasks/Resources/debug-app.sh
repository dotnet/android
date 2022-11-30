#!env bash
PACKAGE_NAME="@PACKAGE_NAME@"
OUTPUT_DIR="@OUTPUT_DIR@"
SUPPORTED_ABIS_ARRAY=(@SUPPORTED_ABIS@)
APP_LIBS_DIR="@APP_LIBS_DIR@"
NDK_DIR="@NDK_DIR@"
CONFIG_SCRIPT_NAME="@CONFIG_SCRIPT_NAME@"
LLDB_SCRIPT_NAME="@LLDB_SCRIPT_NAME@"
ADB_PATH="@ADB_PATH@"
DEBUG_SESSION_PREP_PATH="@DEBUG_SESSION_PREP_PATH@"

function die()
{
	echo "$@"
	exit 1
}

function die_if_failed()
{
	if [ $? -ne 0 ]; then
		die "$@"
	fi
}

#TODO: APP_LIBS_DIR needs to be appended the abi-specific subdir
#TODO: make NDK_DIR overridable via a parameter
#TOOD: add a parameter to specify the Android device to target
#TODO: add a parameter to specify the arch to use, verify against both SUPPORTED_ABIS_ARRAY and the device ABIs

SUPPORTED_ABIS_ARG=""
for sa in "${SUPPORTED_ABIS_ARRAY[@]}"; do
	if [ -z "${SUPPORTED_ABIS_ARG}" ]; then
		SUPPORTED_ABIS_ARG="${sa}"
	else
		SUPPORTED_ABIS_ARG="${SUPPORTED_ABIS_ARG},${sa}"
	fi
done

"${DEBUG_SESSION_PREP_PATH} -s "${SUPPORTED_ABIS_ARG}" \
							-p "${PACKAGE_NAME}" \
							-n "${NDK_DIR}" \
							-o "${OUTPUT_DIR}" \
							-l "${APP_LIBS_DIR}" \
							-c "${CONFIG_SCRIPT_NAME}" \
							-g "${LLDB_SCRIPT_NAME}"

die_if_failed Debug preparation app failed

CONFIG_SCRIPT_PATH="${OUTPUT_DIR}/${CONFIG_SCRIPT_NAME}"
if [ ! -f "${CONFIG_SCRIPT_PATH}" ]; then
   die Config script ${CONFIG_SCRIPT_PATH} not found
fi

source "${CONFIG_SCRIPT_PATH}"
