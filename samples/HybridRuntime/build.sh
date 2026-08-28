#!/bin/bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/../.." && pwd)"
configuration="${CONFIGURATION:-Release}"
maui_version="${MAUI_VERSION:-11.0.0-preview.6.26360.8}"
shell_project="${script_dir}/HybridTodoApp/HybridTodoApp.csproj"
payload_project="${script_dir}/CoreClrPayload/HybridRuntimeCoreClr.csproj"
output_apk="${script_dir}/bin/HybridRuntime-Signed.apk"
unaligned_apk="${script_dir}/bin/HybridRuntime-unaligned.apk"
work_dir="$(mktemp -d)"

android_sdk="${ANDROID_HOME:-${ANDROID_SDK_ROOT:-}}"
if [[ -z "${android_sdk}" ]]; then
	echo "Set ANDROID_HOME to an Android SDK containing build-tools." >&2
	exit 1
fi
build_tools="$(find "${android_sdk}/build-tools" -mindepth 1 -maxdepth 1 -type d -print | sort -V | tail -1)"
android_jar="$(find "${android_sdk}/platforms" -mindepth 2 -maxdepth 2 -name android.jar -print | sort -V | tail -1)"
zipalign="${build_tools}/zipalign"
apksigner="${build_tools}/apksigner"
d8="${build_tools}/d8"

cleanup() {
	find "${work_dir}" -type f -delete
	find "${work_dir}" -depth -type d -delete
}
trap cleanup EXIT

"${repo_root}/dotnet-local.sh" build "${payload_project}" -c "${configuration}" -p:MauiVersion="${maui_version}"
"${repo_root}/dotnet-local.sh" build "${shell_project}" -c "${configuration}" \
	-p:MauiVersion="${maui_version}" \
	-p:TodoRuntime=NativeAOT \
	-p:HybridRuntime=true \
	-p:AndroidManifest=Platforms/Android/HybridAndroidManifest.xml

payload_apk="$(find "${script_dir}/CoreClrPayload/bin/${configuration}" -name '*-Signed.apk' -print -quit)"
shell_apk="$(find "${script_dir}/HybridTodoApp/bin/${configuration}" -name 'net.dot.hybridruntime-Signed.apk' -print -quit)"
if [[ -z "${payload_apk}" || -z "${shell_apk}" ]]; then
	echo "Could not locate both signed input APKs." >&2
	exit 1
fi

shell_classes="$(find "${script_dir}/HybridTodoApp/obj/${configuration}" -type d -path '*/android/bin/classes' -print -quit)"
runtime_jar="$(find "${repo_root}/bin" -path '*/lib/packs/*/tools/java_runtime_trimmable.jar' -print | sort -V | tail -1)"
if [[ -z "${shell_classes}" || -z "${runtime_jar}" || ! -f "${android_jar}" || ! -x "${d8}" ]]; then
	echo "Could not locate the NativeAOT Java classes, runtime JAR, android.jar, or d8." >&2
	exit 1
fi

mkdir -p "${script_dir}/bin" "${work_dir}/payload" "${work_dir}/application-classes" "${work_dir}/application-dex"
"${JAVA_HOME}/bin/javac" \
	-classpath "${android_jar}:${runtime_jar}:${shell_classes}" \
	-d "${work_dir}/application-classes" \
	"${script_dir}/Shell/java/net/dot/hybrid/HybridApplication.java"
"${d8}" \
	--lib "${android_jar}" \
	--classpath "${runtime_jar}" \
	--classpath "${shell_classes}" \
	--output "${work_dir}/application-dex" \
	"${work_dir}/application-classes/net/dot/hybrid/HybridApplication.class"

cp "${shell_apk}" "${unaligned_apk}"
(
	cd "${work_dir}/payload"
	unzip -q "${payload_apk}" 'lib/*' 'assemblies/*' 'assets/*' 'res/*' 'resources.arsc' 'classes*.dex' || true
	chmod -R u+rwX .
	mkdir payload-dex
	find . -maxdepth 1 -type f -name 'classes*.dex' -exec mv {} payload-dex/ \;
	if [[ -d lib ]]; then
		zip -q -0 -r "${unaligned_apk}" lib
	fi
	if [[ -d assemblies ]]; then
		zip -q -r "${unaligned_apk}" assemblies
	fi
	zip -q -d "${unaligned_apk}" 'assets/*' 'res/*' resources.arsc || true
	if [[ -d assets ]]; then
		zip -q -r "${unaligned_apk}" assets
	fi
	if [[ -d res ]]; then
		zip -q -r "${unaligned_apk}" res
	fi
	if [[ -f resources.arsc ]]; then
		zip -q -0 "${unaligned_apk}" resources.arsc
	fi

	cp "${work_dir}/application-dex/classes.dex" classes2.dex
	zip -q -0 "${unaligned_apk}" classes2.dex
	dex_index=3
	for payload_dex in payload-dex/classes.dex payload-dex/classes2.dex payload-dex/classes3.dex payload-dex/classes4.dex; do
		[[ -f "${payload_dex}" ]] || continue
		merged_dex="classes${dex_index}.dex"
		cp "${payload_dex}" "${merged_dex}"
		zip -q -0 "${unaligned_apk}" "${merged_dex}"
		dex_index=$((dex_index + 1))
	done
)
zip -q -d "${unaligned_apk}" 'META-INF/*' || true

debug_keystore="$(find "${script_dir}/HybridTodoApp/obj/${configuration}" -name debug.keystore -print -quit)"
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
