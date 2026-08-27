#!/bin/bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/../.." && pwd)"
configuration="${CONFIGURATION:-Release}"
maui_version="${MAUI_VERSION:-11.0.0-preview.6.26360.8}"
shell_project="${script_dir}/Shell/HybridRuntimeShell.csproj"
payload_project="${script_dir}/CoreClrPayload/HybridRuntimeCoreClr.csproj"
output_apk="${script_dir}/bin/HybridRuntime-Signed.apk"
unaligned_apk="${script_dir}/bin/HybridRuntime-unaligned.apk"
work_dir="$(mktemp -d)"

cleanup() {
	find "${work_dir}" -type f -delete
	find "${work_dir}" -depth -type d -delete
}
trap cleanup EXIT

"${repo_root}/dotnet-local.sh" build "${payload_project}" -c "${configuration}" -p:MauiVersion="${maui_version}"
"${repo_root}/dotnet-local.sh" build "${shell_project}" -c "${configuration}"

payload_apk="$(find "${script_dir}/CoreClrPayload/bin/${configuration}" -name '*-Signed.apk' -print -quit)"
shell_apk="$(find "${script_dir}/Shell/bin/${configuration}" -name '*-Signed.apk' -print -quit)"
if [[ -z "${payload_apk}" || -z "${shell_apk}" ]]; then
	echo "Could not locate both signed input APKs." >&2
	exit 1
fi

mkdir -p "${script_dir}/bin" "${work_dir}/payload"
cp "${shell_apk}" "${unaligned_apk}"
(
	cd "${work_dir}/payload"
	unzip -q "${payload_apk}" 'lib/*' 'assemblies/*' || true
	if [[ -d lib ]]; then
		zip -q -0 -r "${unaligned_apk}" lib
	fi
	if [[ -d assemblies ]]; then
		zip -q -r "${unaligned_apk}" assemblies
	fi
)
zip -q -d "${unaligned_apk}" 'META-INF/*' || true

android_sdk="${ANDROID_HOME:-${ANDROID_SDK_ROOT:-}}"
if [[ -z "${android_sdk}" ]]; then
	echo "Set ANDROID_HOME to an Android SDK containing build-tools." >&2
	exit 1
fi

build_tools="$(find "${android_sdk}/build-tools" -mindepth 1 -maxdepth 1 -type d -print | sort -V | tail -1)"
zipalign="${build_tools}/zipalign"
apksigner="${build_tools}/apksigner"
debug_keystore="$(find "${script_dir}/Shell/obj/${configuration}" -name debug.keystore -print -quit)"
if [[ -z "${debug_keystore}" && -f "${HOME}/Library/Application Support/Xamarin/Mono for Android/debug.keystore" ]]; then
	debug_keystore="${HOME}/Library/Application Support/Xamarin/Mono for Android/debug.keystore"
fi
if [[ -z "${debug_keystore}" && -f "${HOME}/.local/share/Xamarin/Mono for Android/debug.keystore" ]]; then
	debug_keystore="${HOME}/.local/share/Xamarin/Mono for Android/debug.keystore"
fi
if [[ ! -x "${zipalign}" || ! -x "${apksigner}" || -z "${debug_keystore}" ]]; then
	echo "Could not locate zipalign, apksigner, or the generated debug keystore." >&2
	exit 1
fi

rm -f "${output_apk}"
"${zipalign}" -f -p 16 "${unaligned_apk}" "${output_apk}"
"${apksigner}" sign \
	--ks "${debug_keystore}" \
	--ks-key-alias androiddebugkey \
	--ks-pass pass:android \
	--key-pass pass:android \
	"${output_apk}"
"${apksigner}" verify --verbose "${output_apk}"

echo "${output_apk}"
