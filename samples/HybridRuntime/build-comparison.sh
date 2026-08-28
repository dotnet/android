#!/bin/bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/../.." && pwd)"
configuration="${CONFIGURATION:-Release}"
maui_version="${MAUI_VERSION:-11.0.0-preview.6.26360.8}"
todo_project="${script_dir}/HybridTodoApp/HybridTodoApp.csproj"

build_todo_variant() {
	local runtime="$1"
	local package_name="$2"
	local output_name="$3"

	rm -rf "${script_dir}/HybridTodoApp/bin" "${script_dir}/HybridTodoApp/obj"
	"${repo_root}/dotnet-local.sh" build "${todo_project}" \
		-c "${configuration}" \
		-p:MauiVersion="${maui_version}" \
		-p:TodoRuntime="${runtime}"

	local apk
	apk="$(find "${script_dir}/HybridTodoApp/bin/${configuration}" -name "${package_name}-Signed.apk" -print -quit)"
	if [[ -z "${apk}" ]]; then
		echo "Could not locate the ${runtime} TODO APK." >&2
		exit 1
	fi
	cp "${apk}" "${script_dir}/bin/${output_name}"
}

mkdir -p "${script_dir}/bin"
rm -f "${script_dir}/bin/MauiCoreCLR-Signed.apk" "${script_dir}/bin/MauiNativeAOT-Signed.apk"
build_todo_variant CoreCLR net.dot.hybridruntime.todo.coreclr TodoCoreCLR-Signed.apk
build_todo_variant NativeAOT net.dot.hybridruntime.todo.nativeaot TodoNativeAOT-Signed.apk
"${script_dir}/build.sh"
