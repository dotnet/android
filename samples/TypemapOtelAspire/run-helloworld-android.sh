#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"

project="${HELLOWORLD_ANDROID_PROJECT:-samples/HelloWorld/HelloWorld/HelloWorld.DotNet.csproj}"
configuration="${HELLOWORLD_ANDROID_CONFIGURATION:-Release}"
runtime="${HELLOWORLD_ANDROID_RUNTIME:-CoreCLR}"
typemap="${HELLOWORLD_ANDROID_TYPEMAP:-trimmable}"
package_name="${HELLOWORLD_ANDROID_PACKAGE:-com.xamarin.android.helloworld}"
activity_name="${HELLOWORLD_ANDROID_ACTIVITY:-example.MainActivity}"
otlp_endpoint="${OTEL_EXPORTER_OTLP_ENDPOINT:-${ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL:-http://localhost:4318}}"
otlp_headers="${OTEL_EXPORTER_OTLP_HEADERS:-}"
keep_resource_alive="${HELLOWORLD_ANDROID_KEEP_RESOURCE_ALIVE:-true}"

adb_args=()
if [[ -n "${ANDROID_SERIAL:-}" ]]; then
	adb_args=(-s "$ANDROID_SERIAL")
fi

adb_cmd() {
	if [[ ${#adb_args[@]} -gt 0 ]]; then
		adb "${adb_args[@]}" "$@"
	else
		adb "$@"
	fi
}

timestamp() {
	date "+%H:%M:%S"
}

log() {
	echo "[$(timestamp)] $*"
}

host_port="4318"
if [[ "$otlp_endpoint" =~ ^https?://[^:/]+:([0-9]+) ]]; then
	host_port="${BASH_REMATCH[1]}"
fi

if [[ -z "$otlp_headers" ]] && command -v pgrep >/dev/null 2>&1; then
	while read -r pid; do
		if [[ -z "$pid" ]]; then
			continue
		fi
		dashboard_environment="$(ps eww -p "$pid" | tr ' ' '\n')"
		if ! grep -q '^DASHBOARD__FRONTEND__PUBLICURL=' <<< "$dashboard_environment"; then
			continue
		fi
		otlp_api_key="$(sed -n 's/^DASHBOARD__OTLP__PRIMARYAPIKEY=//p' <<< "$dashboard_environment" | head -1)"
		if [[ -n "$otlp_api_key" ]]; then
			otlp_headers="x-otlp-api-key=$otlp_api_key"
			break
		fi
	done < <(pgrep -x dotnet || true)
fi

case "$runtime" in
	MonoVM|mono|monovm)
		use_mono_runtime="true"
		;;
	CoreCLR|coreclr)
		use_mono_runtime="false"
		;;
	*)
		echo "Unknown HELLOWORLD_ANDROID_RUNTIME '$runtime'. Expected CoreCLR or MonoVM." >&2
		exit 1
		;;
esac

if ! command -v adb >/dev/null 2>&1; then
	echo "adb was not found on PATH." >&2
	exit 1
fi

if [[ ! -x "$repo_root/dotnet-local.sh" ]]; then
	echo "dotnet-local.sh was not found or is not executable in $repo_root." >&2
	exit 1
fi

device_count="$(adb_cmd devices | awk 'NR > 1 && $2 == "device" { count++ } END { print count + 0 }')"
if [[ "$device_count" -eq 0 ]]; then
	echo "No Android device is connected. Start an emulator or connect a device, then rerun Aspire." >&2
	exit 1
fi
if [[ "$device_count" -gt 1 && -z "${ANDROID_SERIAL:-}" ]]; then
	echo "Multiple Android devices are connected. Set ANDROID_SERIAL before rerunning Aspire." >&2
	exit 1
fi

log "Using OTLP endpoint from Aspire: $otlp_endpoint"
if [[ -n "$otlp_headers" ]]; then
	log "Using OTLP headers from Aspire dashboard."
fi
log "Forwarding Android tcp:4318 to host tcp:$host_port"
adb_cmd reverse "tcp:4318" "tcp:$host_port"

log "Removing any existing $package_name install"
adb_cmd uninstall "$package_name" >/dev/null 2>&1 || true

log "Building and installing $project"
log "Configuration=$configuration Runtime=$runtime TypeMap=$typemap"
"$repo_root/dotnet-local.sh" build "$project" \
	-c "$configuration" \
	-t:Install \
	-p:UseMonoRuntime="$use_mono_runtime" \
	-p:_AndroidTypeMapImplementation="$typemap" \
	-p:AndroidPackageFormats=apk

log "Starting $package_name/$activity_name"
adb_cmd shell am force-stop "$package_name" >/dev/null
adb_cmd logcat -c || true
start_args=(-W -n "$package_name/$activity_name")
if [[ -n "$otlp_headers" ]]; then
	start_args+=(--es OTEL_EXPORTER_OTLP_HEADERS "$otlp_headers")
fi
adb_cmd shell am start "${start_args[@]}"

for _ in {1..10}; do
	if adb_cmd shell pidof "$package_name" >/dev/null 2>&1; then
		log "HelloWorld is running. Typemap telemetry should appear in the Aspire dashboard under service helloworld-android."
		if [[ "$keep_resource_alive" == "true" ]]; then
			log "Keeping the Aspire resource alive while the Android process is running."
			while adb_cmd shell pidof "$package_name" >/dev/null 2>&1; do
				sleep 2
			done
			log "HelloWorld process exited."
			exit 1
		fi
		exit 0
	fi
	sleep 1
done

echo "HelloWorld did not stay running after launch." >&2
adb_cmd logcat -b crash -d -t 80 >&2 || true
exit 1
