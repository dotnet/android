#!/bin/bash -e
CONFIGURATION="${1:-Release}"
XA_ROOT=~/xamarin-android/
XA_GIT_HASH="$(cd $XA_ROOT; git log --pretty=%H -1)"
XA_GIT_BRANCH="$(cd $XA_ROOT; git branch --show-current)"
DEVICE="$(adb shell getprop ro.product.model)"
ARCH="$(adb shell getprop ro.product.cpu.abi)"
SDK_VER="$(adb shell getprop ro.build.version.sdk)"
RUNS=10
CSV_FILE="performance-report.csv"
TABLE_FILE="performance-table.md"
ACTIVITY_NAME="Xamarin.Forms_Performance_Integration/xamarin.forms.performance.integration.MainActivity"
SLEEP_TIME=5

function build_and_install_app()
{
    local count=$1
    local tag="$2"
    local build_properties="$3"

    pushd Droid > /dev/null
    rm -rf bin obj

    echo
    echo "Build ${GLOBAL_RUN_COUNTER} of ${TOTAL_RUNS}: restoring packages..."
    xabuild /noConsoleLogger /bl:restore-${XA_GIT_HASH}-${XA_GIT_BRANCH}-${tag}-${count}.binlog /t:Restore /p:Configuration=${CONFIGURATION} ${build_properties}

    echo
    echo "Build ${GLOBAL_RUN_COUNTER} of ${TOTAL_RUNS}: building..."
    xabuild /noConsoleLogger /bl:build-${XA_GIT_HASH}-${XA_GIT_BRANCH}-${tag}-${count}.binlog /t:Install /p:Configuration=${CONFIGURATION} ${build_properties}

    popd > /dev/null
}

function _do_convert_to_ns()
{
    local sec="$1"
    local ms="$2"
    local ns="$3"

    echo $((($sec * 1000000000) + ($ms * 1000000) + $ns))
}

function convert_to_ns()
{
    local raw="$1"
    _do_convert_to_ns $(echo $raw | tr ':' ' ' | tr -d 's' | tr -s ' ')
}

function convert_to_ms()
{
    local ns="$1"

    echo "scale=3;$1 / 1000000" | bc -l
}

function update_averages()
{
    local native_to_managed=$1
    local total_init=$2
    local displayed=$3
    local scale="scale=2;("
    local first="no"

    if [ -z "$NATIVE_TO_MANAGED_AVERAGE" ]; then
        NATIVE_TO_MANAGED_AVERAGE="${scale}${native_to_managed}"
        TOTAL_INIT_AVERAGE="${scale}${total_init}"
        DISPLAYED_AVERAGE="${scale}${displayed}"
        first="yes"
    fi

    if [ "$first" = "yes" ]; then
        return
    fi

    NATIVE_TO_MANAGED_AVERAGE="${NATIVE_TO_MANAGED_AVERAGE} + ${native_to_managed}"
    TOTAL_INIT_AVERAGE="${TOTAL_INIT_AVERAGE} + ${total_init}"
    DISPLAYED_AVERAGE="${DISPLAYED_AVERAGE} + ${displayed}"
}

declare -a RUN_NUMBERS

function run()
{
    local logcat_tag="$1"
    local note="$2"
    local build_properties="$3"
    local logcat_name

    NATIVE_TO_MANAGED_AVERAGE=
    TOTAL_INIT_AVERAGE=
    DISPLAYED_AVERAGE=
    RUN_COUNTER=1

    while [ ${RUN_COUNTER} -le ${RUNS} ]; do
        clear
        build_and_install_app ${RUN_COUNTER} "${logcat_tag}" "${build_properties}"
        adb shell setprop debug.mono.log default,timing=bare
        adb logcat -G 16M
        adb logcat -c

        NATIVE_TO_MANAGED=
        TOTAL_INIT=
        DISPLAYED=
        while [ -z "$NATIVE_TO_MANAGED" -o -z "$TOTAL_INIT" -o -z "$DISPLAYED" ]; do
            echo
            echo "Run: ${GLOBAL_RUN_COUNTER} of ${TOTAL_RUNS} (${note})"
            echo "Build complete."

            echo Starting application
            adb shell am start -n "${ACTIVITY_NAME}"

            echo Sleeping for ${SLEEP_TIME}s
            sleep ${SLEEP_TIME}

            echo Recording statistics
            logcat_name=logcat-${XA_GIT_HASH}-${XA_GIT_BRANCH}-${logcat_tag}-${RUN_COUNTER}.txt
            adb logcat -d > $logcat_name

            NATIVE_TO_MANAGED=$(grep 'Runtime.init: end native-to-managed transition; elapsed:' $logcat_name | sed -n -e 's/^.*Runtime\.init:.*; elapsed: \(.*\)/\1/p')
            TOTAL_INIT=$(grep 'Runtime.init: end, total time; elapsed:' $logcat_name | sed -n -e 's/^.*Runtime\.init:.*; elapsed: \(.*\)/\1/p')
            DISPLAYED=$(grep 'ActivityTaskManager: Displayed' $logcat_name | cut -d ':' -f 5 | tr -d ' a-z+')
        done

        NATIVE_TO_MANAGED=$(convert_to_ns ${NATIVE_TO_MANAGED})
        TOTAL_INIT=$(convert_to_ns ${TOTAL_INIT})

        RUN_NUMBERS[$((${RUN_COUNTER} - 1))]="${NATIVE_TO_MANAGED} ${TOTAL_INIT} ${DISPLAYED}"
        RUN_COUNTER=$((${RUN_COUNTER} + 1))
        GLOBAL_RUN_COUNTER=$((${GLOBAL_RUN_COUNTER} + 1))
    done

    for i in $(seq 1 $((${#RUN_NUMBERS[@]}))); do
        update_averages ${RUN_NUMBERS[$(($i - 1))]}
    done

    NATIVE_TO_MANAGED_AVERAGE=$(echo "${NATIVE_TO_MANAGED_AVERAGE}) / ${RUNS}" | bc -l)
    TOTAL_INIT_AVERAGE=$(echo "${TOTAL_INIT_AVERAGE}) / ${RUNS}" | bc -l)
    DISPLAYED_AVERAGE=$(echo "${DISPLAYED_AVERAGE}) / ${RUNS}" | bc -l)

    if [ ! -f ${CSV_FILE} ]; then
        echo "Xamarin.Android,Number of runs,Native-to-managed (ns),Total init time (ns),Displayed time (ms),Notes,Device,SDK version,Device architecture" > ${CSV_FILE}
    fi

    if [ ! -f ${TABLE_FILE} ]; then
        cat <<EOF > ${TABLE_FILE}
        Device name: **${DEVICE}**
Device architecture: **${ARCH}**
Number of test runs: **${RUNS}**

|                 | **Native to managed**  | **Runtime init** | **Displayed** | **Notes**                      |
|-----------------|------------------------|------------------|---------------|--------------------------------|
EOF
    fi

    echo "${XA_GIT_BRANCH}/${XA_GIT_HASH},${RUNS},${NATIVE_TO_MANAGED_AVERAGE},${TOTAL_INIT_AVERAGE},${DISPLAYED_AVERAGE},${note},${DEVICE},${SDK_VER},${ARCH}" >> ${CSV_FILE}

    NATIVE_TO_MANAGED_MS=$(convert_to_ms ${NATIVE_TO_MANAGED_AVERAGE})
    TOTAL_INIT_MS=$(convert_to_ms ${TOTAL_INIT_AVERAGE})

    echo "| **master**      | XXX.XX                 | XXX.XX           | XXX.XX        | ${note} |" >> ${TABLE_FILE}
    echo "| **this commit** | ${NATIVE_TO_MANAGED_MS}                | ${TOTAL_INIT_MS}          | ${DISPLAYED_AVERAGE}        |  |" >> ${TABLE_FILE}
}

TOTAL_RUNS=$((${RUNS} * 4))
GLOBAL_RUN_COUNTER=1

rm -f ${CSV_FILE}
rm -f ${TABLE_FILE}
run armeabi-v7a_preload " preload enabled; 32-bit build" "/p:AndroidEnablePreloadAssemblies=True"
run armeabi-v7a_nopreload "preload disabled; 32-bit build" "/p:AndroidEnablePreloadAssemblies=False"
run arm64-v8a_preload " preload enabled; 64-bit build" "/p:AndroidEnablePreloadAssemblies=True /p:Enable64BitBuild=True"
run arm64-v8a_nopreload "preload disabled; 64-bit build" "/p:AndroidEnablePreloadAssemblies=False /p:Enable64BitBuild=True"

echo
echo     CSV report file: ${CSV_FILE}
echo Markdown table file: ${TABLE_FILE}
echo
