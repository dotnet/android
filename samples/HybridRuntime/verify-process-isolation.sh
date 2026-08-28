#!/bin/bash
set -euo pipefail

package="net.dot.hybridruntime"
adb="${ADB:-adb}"

main_pid="$("${adb}" shell pidof "${package}" | tr -d '\r')"
coreclr_pid="$("${adb}" shell ps -A | awk '$NF == "net.dot.hybridruntime:coreclr" { print $2 }')"

if [[ ! "${main_pid}" =~ ^[0-9]+$ || ! "${coreclr_pid}" =~ ^[0-9]+$ ]]; then
	echo "Both ${package} processes must be running." >&2
	exit 1
fi

inspect_process() {
	local label="$1"
	local pid="$2"
	local status
	local maps

	status="$("${adb}" shell cat "/proc/${pid}/status")"
	maps="$("${adb}" shell cat "/proc/${pid}/maps")"

	echo "=== ${label} (PID ${pid}) ==="
	echo "${status}" | grep -E '^(Name|Pid|PPid|Threads|SigBlk|SigIgn|SigCgt):'
	echo "Runtime libraries:"
	echo "${maps}" |
		grep -o '/[^ ]*\.so' |
		sed 's#.*/##' |
		grep -E 'HybridTodoApp|coreclr|clrjit|monodroid|xamarin-app|assembly-store' |
		sort -u
	echo

	if [[ "${label}" == "NativeAOT" ]]; then
		grep -Fq 'libHybridTodoApp.so' <<< "${maps}"
		if grep -Fq 'libcoreclr.so' <<< "${maps}"; then
			echo "CoreCLR was unexpectedly loaded in the NativeAOT process." >&2
			exit 1
		fi
	else
		grep -Fq 'libcoreclr.so' <<< "${maps}"
		if grep -Fq 'libHybridTodoApp.so' <<< "${maps}"; then
			echo "NativeAOT was unexpectedly loaded in the CoreCLR process." >&2
			exit 1
		fi
	fi
}

inspect_process "NativeAOT" "${main_pid}"
inspect_process "CoreCLR" "${coreclr_pid}"

main_signals="$("${adb}" shell cat "/proc/${main_pid}/status" | sed -n 's/^SigCgt:[[:space:]]*//p' | tr -d '\r')"
coreclr_signals="$("${adb}" shell cat "/proc/${coreclr_pid}/status" | sed -n 's/^SigCgt:[[:space:]]*//p' | tr -d '\r')"
if [[ "${main_signals}" == "${coreclr_signals}" ]]; then
	echo "Expected the runtimes to have distinct caught-signal masks." >&2
	exit 1
fi

echo "Isolation verified: runtime libraries and signal handlers belong to different processes."
