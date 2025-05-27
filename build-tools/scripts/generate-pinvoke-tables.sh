#!/bin/bash -e
MY_DIR="$(dirname $0)"
HOST="$(uname | tr A-Z a-z)"

NATIVE_DIR="${MY_DIR}/../../src/native"
MONODROID_SOURCE_DIR="${NATIVE_DIR}/mono/pinvoke-override"
MONODROID_INCLUDE_DIR="${NATIVE_DIR}/mono/shared"
CLR_SOURCE_DIR="${NATIVE_DIR}/clr/pinvoke-override"
CLR_INCLUDE_DIR="${NATIVE_DIR}/clr/include/shared"
GENERATOR_SOURCE="generate-pinvoke-tables.cc"
GENERATOR_BINARY="generate-pinvoke-tables"
TARGET_FILE="pinvoke-tables.include"
GENERATED_FILE="${TARGET_FILE}.generated"
DIFF_FILE="${TARGET_FILE}.diff"
EXTERNAL_DIR="${MY_DIR}/../../external/"

function die()
{
	echo "$@"
	exit 1
}

function usage()
{
	cat <<EOF
Usage: ${MY_NAME} [OPTIONS]

where OPTIONS are one or more of:

   -c|--ci                  indicates that the script runs on one of the .NET for Android CI build
                            servers. This affects selection of the compiler
   -t|--test-only           indicate that the script should not replace the target file but merely
                            test whether the file is different to the newly generated one
   -h|--help                show this help screen
EOF

	exit 0
}

RUNNING_ON_CI="no"
TEST_ONLY="no"

while (( "$#" )); do
    case "$1" in
		-c|--ci) RUNNING_ON_CI="yes"; shift ;;
		-t|--test-only) TEST_ONLY="yes"; shift ;;
		-h|--help) usage ;;
		*) shift ;;
	esac
done

case ${HOST} in
	linux)
		if [ "${RUNNING_ON_CI}" == "no" ]; then
			COMPILER="g++"
		else
			COMPILER="g++-10"
		fi ;;

	darwin)
		if [ "${RUNNING_ON_CI}" == "no" ]; then
			COMPILER="clang++"
		else
			COMPILER="g++-11"
		fi ;;

	*) die Unsupported OS ;;
esac

function generate()
{
	local SOURCE_DIR="${1}"
	local INCLUDE_DIR="${2}"
	local SOURCE="${SOURCE_DIR}/${GENERATOR_SOURCE}"
	local BINARY="${SOURCE_DIR}/${GENERATOR_BINARY}"
	local RESULT="${SOURCE_DIR}/${GENERATED_FILE}"
	local TARGET="${SOURCE_DIR}/${TARGET_FILE}"
	local DIFF="${SOURCE_DIR}/${DIFF_FILE}"

	${COMPILER} -O2 -std=c++20 -I${EXTERNAL_DIR} -I${EXTERNAL_DIR}/constexpr-xxh3 -I${INCLUDE_DIR} -I${NATIVE_DIR}/common/include "${SOURCE}" -o "${BINARY}"
	"${BINARY}" "${RESULT}"

	FILES_DIFFER="no"
	cmp "${RESULT}" "${TARGET}" > /dev/null 2>&1 || FILES_DIFFER="yes"

	if [ "${TEST_ONLY}" == "no" ]; then
		if [ "${FILES_DIFFER}" == "yes" ]; then
			  mv "${RESULT}" "${TARGET}"
		else
			rm "${RESULT}"
		fi
	else
		if [ "${FILES_DIFFER}" == "yes" ]; then
			echo "Generated p/invokes table file differs from the current one"
			diff -U3 -Narp "${TARGET}" "${RESULT}" > "${DIFF}"

			echo "Diff file saved in: ${DIFF}"
			echo "------ DIFF START ------"
			cat "${DIFF}"
			echo "------ DIFF END ------"
			echo
			RETVAL=1
		else
			echo Generated file is identical to the current one
		fi
	fi
}

RETVAL=0
cat <<EOF
**
** Generating for MonoVM
**
EOF
generate "${MONODROID_SOURCE_DIR}" "${MONODROID_INCLUDE_DIR}"

cat <<EOF

--------------------------------------

**
** Generating for CoreCLR
**
EOF
generate "${CLR_SOURCE_DIR}" "${CLR_INCLUDE_DIR}"

exit ${RETVAL}
