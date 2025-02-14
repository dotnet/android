cmake_minimum_required(VERSION 3.21)

#
# Read product version
#
file(STRINGS "../../Directory.Build.props" XA_PRODUCT_VERSION_XML REGEX "^[ \t]*<ProductVersion>(.*)</ProductVersion>")
string(REGEX REPLACE "^[ \t]*<ProductVersion>(.*)</ProductVersion>" "\\1" XA_VERSION "${XA_PRODUCT_VERSION_XML}")

project(
  android-native-bits
  VERSION ${XA_VERSION}
  DESCRIPTION ".NET for Android native runtime"
  HOMEPAGE_URL "https://github.com/dotnet/android"
  LANGUAGES CXX C ASM
)

#
# Sanity checks
#
macro(ensure_variable_set VARNAME)
  if(NOT ${VARNAME})
    message(FATAL_ERROR "Variable ${VARNAME} not set.  Please set it either on command line with -D${VARNAME}=value or in the presets file")
  endif()
endmacro()

ensure_variable_set(ANDROID_ABI)
ensure_variable_set(CMAKE_ANDROID_NDK)
ensure_variable_set(CMAKE_BUILD_TYPE)
ensure_variable_set(OUTPUT_PATH)
ensure_variable_set(XA_BUILD_CONFIGURATION)
ensure_variable_set(XA_LIB_TOP_DIR)
ensure_variable_set(RUNTIME_FLAVOR)

set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY "${OUTPUT_PATH}/${ANDROID_RID}" CACHE PATH "" FORCE)
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY "${OUTPUT_PATH}/${ANDROID_RID}" CACHE PATH "" FORCE)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

set(CMAKE_C_STANDARD 11)
set(CMAKE_C_STANDARD_REQUIRED ON)
set(CMAKE_C_EXTENSIONS OFF)

include("${CMAKE_ANDROID_NDK}/build/cmake/abis.cmake")
include("${CMAKE_SOURCE_DIR}/cmake/ArchiveDSOStub.cmake")

string(TOLOWER "${RUNTIME_FLAVOR}" RUNTIME_FLAVOR_LOWER)
if (RUNTIME_FLAVOR_LOWER STREQUAL monovm)
  set(IS_MONO_RUNTIME True)
  set(IS_CLR_RUNTIME False)
  set(SOURCES_PREFIX "mono")
elseif(RUNTIME_FLAVOR_LOWER STREQUAL coreclr)
  set(IS_MONO_RUNTIME False)
  set(IS_CLR_RUNTIME True)
  set(SOURCES_PREFIX "clr")
else()
  message(FATAL "Unknown runtime flavor ${RUNTIME_FLAVOR}")
endif()

if(CMAKE_BUILD_TYPE STREQUAL Debug)
  set(DEBUG_BUILD True)
else()
  set(DEBUG_BUILD False)
endif()

set(XA_NO_INLINE "$ENV{XA_NO_INLINE}")
if(XA_NO_INLINE)
  set(DONT_INLINE_DEFAULT ON)
else()
  set(DONT_INLINE_DEFAULT OFF)
endif()

set(XA_NO_STRIP "$ENV{XA_NO_STRIP}")
if(XA_NO_STRIP OR DEBUG_BUILD)
  set(STRIP_DEBUG_DEFAULT OFF)
endif()

option(ENABLE_CLANG_ASAN "Enable the clang AddressSanitizer support" OFF)
option(ENABLE_CLANG_UBSAN "Enable the clang UndefinedBehaviorSanitizer support" OFF)

if(ENABLE_CLANG_ASAN AND ENABLE_CLANG_UBSAN)
  message(FATAL_ERROR "Both ASAN and UBSAN builds cannot be enabled at the same time")
endif()

if(ENABLE_CLANG_ASAN OR ENABLE_CLANG_UBSAN)
  if(BUILD_ARCHIVE_DSO_STUB)
    message(FATAL_ERROR "ASAN/UBSAN builds aren't supported by the archive DSO target")
  endif()

  set(STRIP_DEBUG_DEFAULT OFF)
  set(ANALYZERS_ENABLED ON)
else()
  if(NOT XA_NO_STRIP)
    set(STRIP_DEBUG_DEFAULT ON)
  endif()
  set(ANALYZERS_ENABLED OFF)
endif()

option(COMPILER_DIAG_COLOR "Show compiler diagnostics/errors in color" ON)
option(STRIP_DEBUG "Strip debugging information when linking" ${STRIP_DEBUG_DEFAULT})
option(DISABLE_DEBUG "Disable the built-in debugging code" OFF)
option(USE_CCACHE "Use ccache, if found, to speed up recompilation" ON)
option(DONT_INLINE "Do not inline any functions which are usually inlined, to get better stack traces" ${DONT_INLINE_DEFAULT})

if(ENABLE_CLANG_ASAN)
  set(STATIC_LIB_NAME_SUFFIX "-asan")
elseif(ENABLE_CLANG_UBSAN)
  set(STATIC_LIB_NAME_SUFFIX "-ubsan")
endif()

if(DEBUG_BUILD)
  set(STATIC_LIB_NAME_SUFFIX "${STATIC_LIB_NAME_SUFFIX}-debug")
else()
  set(STATIC_LIB_NAME_SUFFIX "${STATIC_LIB_NAME_SUFFIX}-release")
endif()

if(USE_CCACHE)
  if(CMAKE_CXX_COMPILER MATCHES "/ccache/")
    message(STATUS "ccache: compiler already uses ccache")
  else()
    find_program(CCACHE ccache)
    if(CCACHE)
      set(CMAKE_CXX_COMPILER_LAUNCHER "${CCACHE}")
      set(CMAKE_C_COMPILER_LAUNCHER "${CCACHE}")
      message(STATUS "ccache: compiler will be lauched with ${CCACHE}")
    endif()
  endif()
endif()

if(ANDROID_STL STREQUAL none)
  set(USES_LIBSTDCPP False)
else()
  set(USES_LIBSTDCPP True)
endif()

set(SHARED_LIB_NAME xa::shared)

#
# Needed modules
#
# include(CheckIncludeFile)
# include(CheckCXXSymbolExists)
include(CheckCXXCompilerFlag)
include(CheckCCompilerFlag)
include(CheckLinkerFlag)

#
# General config
#
set(XA_BUILD_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../../bin/Build${XA_BUILD_CONFIGURATION}")
include("${XA_BUILD_DIR}/xa_build_configuration.cmake")

#
# Paths
#
if(IS_CLR_RUNTIME)
  set(RUNTIME_DIR_ARM64  "${CORECLR_APP_RUNTIME_DIR_ARM64}")
  set(RUNTIME_DIR_ARM    "${CORECLR_APP_RUNTIME_DIR_ARM}")
  set(RUNTIME_DIR_X86_64 "${CORECLR_APP_RUNTIME_DIR_X86_64}")
  set(RUNTIME_DIR_X86    "${CORECLR_APP_RUNTIME_DIR_X86}")

  # TEMPORARY: for now JI needs to build with MonoVM embedding APIs
  set(TEMP_NETCORE_RUNTIME_DIR_ARM64  "${NETCORE_APP_RUNTIME_DIR_ARM64}")
  set(TEMP_NETCORE_RUNTIME_DIR_ARM    "${NETCORE_APP_RUNTIME_DIR_ARM}")
  set(TEMP_NETCORE_RUNTIME_DIR_X86_64 "${NETCORE_APP_RUNTIME_DIR_X86_64}")
  set(TEMP_NETCORE_RUNTIME_DIR_X86    "${NETCORE_APP_RUNTIME_DIR_X86}")
elseif(IS_MONO_RUNTIME)
  set(RUNTIME_DIR_ARM64  "${NETCORE_APP_RUNTIME_DIR_ARM64}")
  set(RUNTIME_DIR_ARM    "${NETCORE_APP_RUNTIME_DIR_ARM}")
  set(RUNTIME_DIR_X86_64 "${NETCORE_APP_RUNTIME_DIR_X86_64}")
  set(RUNTIME_DIR_X86    "${NETCORE_APP_RUNTIME_DIR_X86}")
endif()

if(ANDROID_ABI MATCHES "^arm64-v8a")
  set(NET_RUNTIME_DIR "${RUNTIME_DIR_ARM64}")
  # TEMPORARY: for now JI needs to build with MonoVM embedding APIs
  set(TEMP_NETCORE_NET_RUNTIME_DIR "${TEMP_NETCORE_RUNTIME_DIR_ARM64}")
  set(TOOLCHAIN_TRIPLE "${NDK_ABI_arm64-v8a_TRIPLE}")
elseif(ANDROID_ABI MATCHES "^armeabi-v7a")
  set(NET_RUNTIME_DIR "${RUNTIME_DIR_ARM}")
  # TEMPORARY: for now JI needs to build with MonoVM embedding APIs
  set(TEMP_NETCORE_NET_RUNTIME_DIR "${TEMP_NETCORE_RUNTIME_DIR_ARM}")
  set(TOOLCHAIN_TRIPLE "${NDK_ABI_armeabi-v7a_TRIPLE}")
elseif(ANDROID_ABI MATCHES "^x86_64")
  set(NET_RUNTIME_DIR "${RUNTIME_DIR_X86_64}")
  # TEMPORARY: for now JI needs to build with MonoVM embedding APIs
  set(TEMP_NETCORE_NET_RUNTIME_DIR "${TEMP_NETCORE_RUNTIME_DIR_X86_64}")
  set(TOOLCHAIN_TRIPLE "${NDK_ABI_x86_64_TRIPLE}")
elseif(ANDROID_ABI MATCHES "^x86")
  set(NET_RUNTIME_DIR "${RUNTIME_DIR_X86}")
  # TEMPORARY: for now JI needs to build with MonoVM embedding APIs
  set(TEMP_NETCORE_NET_RUNTIME_DIR "${TEMP_NETCORE_RUNTIME_DIR_X86}")
  set(TOOLCHAIN_TRIPLE "${NDK_ABI_x86_TRIPLE}")
else()
  message(FATAL "${ANDROID_ABI} is not supported for .NET 6+ builds")
endif()


file(REAL_PATH "../../" REPO_ROOT_DIR)
set(EXTERNAL_DIR "${REPO_ROOT_DIR}/external")
set(JAVA_INTEROP_SRC_PATH "${EXTERNAL_DIR}/Java.Interop/src/java-interop")
set(LIBUNWIND_SOURCE_DIR "${EXTERNAL_DIR}/libunwind")
set(ROBIN_MAP_DIR "${EXTERNAL_DIR}/robin-map")

#
# Include directories
#
set(SYSROOT_CXX_INCLUDE_DIR ${CMAKE_SYSROOT}/usr/include/c++/v1)

if(IS_MONO_RUNTIME)
  set(RUNTIME_INCLUDE_DIR ${NET_RUNTIME_DIR}/native/include/mono-2.0)
else()
  # TEMPORARY: for now JI needs to build with MonoVM embedding APIs
  set(TEMP_MONO_RUNTIME_INCLUDE_DIR ${TEMP_NETCORE_NET_RUNTIME_DIR}/native/include/mono-2.0)
  set(RUNTIME_INCLUDE_DIR ${NET_RUNTIME_DIR}/native/include/clr)
endif()

set(JAVA_INTEROP_INCLUDE_DIR ${JAVA_INTEROP_SRC_PATH})
set(CONSTEXPR_XXH3_DIR "${EXTERNAL_DIR}/constexpr-xxh3")
set(XXHASH_DIR "${EXTERNAL_DIR}/xxHash")

include_directories(common/include)

if(IS_CLR_RUNTIME)
  set(XA_RUNTIME_INCLUDE_DIR ${CMAKE_CURRENT_SOURCE_DIR}/clr/include)

  macro(xa_add_include_directories TARGET)
     target_include_directories(
        ${TARGET}
        PRIVATE
        ${XA_RUNTIME_INCLUDE_DIR}
        ${EXTERNAL_DIR}
        ${CONSTEXPR_XXH3_DIR}
        ${RUNTIME_INCLUDE_DIR}
     )
  endmacro()
else()
  include_directories(mono)
endif()

#
# Compiler defines
#
macro(xa_add_compile_definitions TARGET)
  target_compile_definitions(
    ${TARGET}
    PRIVATE
    XA_VERSION="${XA_VERSION}"
    _REENTRANT
    PLATFORM_ANDROID
  )

  if(ANDROID_ABI MATCHES "^(arm64-v8a|x86_64)")
    target_compile_definitions(
      ${TARGET}
      PRIVATE
      ANDROID64
    )
  endif()

  if(IS_CLR_RUNTIME)
    target_compile_definitions(
      ${TARGET}
      PRIVATE
      TARGET_ANDROID
      XA_HOST_CLR
    )
  else()
    target_compile_definitions(
      ${TARGET}
      PRIVATE
      XA_HOST_MONOVM
    )
  endif()
endmacro()

if(NOT BUILD_ARCHIVE_DSO_STUB)
  if(DEBUG_BUILD AND NOT DISABLE_DEBUG)
    add_compile_definitions(DEBUG)
  endif()

  if(NOT DEBUG_BUILD)
    add_compile_definitions(RELEASE NDEBUG)
  endif()
endif()

#
# Compiler argument macros
#
macro(_compiler_has_arg _lang _arg)
  string(REGEX REPLACE "-|,|=" "_" _arg_name ${_arg})
  string(TOUPPER "${_lang}" _lang_upper)

  cmake_language(CALL check_${_lang}_compiler_flag "${_arg}" HAS_${_arg_name}_${_lang_upper})
  if(HAS_${_arg_name}_${_lang_upper})
    set(COMPILER_ARG_FOUND True)
  else()
    set(COMPILER_ARG_FOUND False)
  endif()
endmacro()

macro(cxx_compiler_has_arg _arg)
  _compiler_has_arg(cxx ${_arg})
endmacro()

macro(c_compiler_has_arg _arg)
  _compiler_has_arg(c ${_arg})
endmacro()

macro(_linker_has_arg _lang _arg)
  string(REGEX REPLACE "-|,|=" "_" _arg_name ${_arg})
  string(TOUPPER "${_lang}" _lang_upper)

  check_linker_flag(${_lang} "${_arg}" HAS_${_arg_name}_LINKER_${_lang_upper})
  if(HAS_${_arg_name}_LINKER_${_lang_upper})
    set(LINKER_ARG_FOUND True)
  else()
    set(LINKER_ARG_FOUND False)
  endif()
endmacro()

macro(cxx_linker_has_arg _arg)
  _linker_has_arg(CXX ${_arg})
endmacro()

macro(c_linker_has_arg _arg)
  _linker_has_arg(C ${_arg})
endmacro()

macro(xa_check_c_args VARNAME _CHECK_ARGS)
  set(_CHECKED_ARGS "")

  foreach(arg ${_CHECK_ARGS})
    c_compiler_has_arg(${arg})
    if(COMPILER_ARG_FOUND)
      list(APPEND _CHECKED_ARGS "${arg}")
    endif()
  endforeach()

  set(${VARNAME} "${_CHECKED_ARGS}")
endmacro()

macro(xa_check_cxx_args VARNAME _CHECK_ARGS)
  set(_CHECKED_ARGS "")

  foreach(arg ${_CHECK_ARGS})
    cxx_compiler_has_arg(${arg})
    if(COMPILER_ARG_FOUND)
      list(APPEND _CHECKED_ARGS "${arg}")
    endif()
  endforeach()

  set(${VARNAME} "${_CHECKED_ARGS}")
endmacro()

macro(xa_check_c_linker_args VARNAME _CHECK_ARGS)
  set(_CHECKED_ARGS "")

  foreach(arg ${_CHECK_ARGS})
    c_linker_has_arg(${arg})
    if(LINKER_ARG_FOUND)
      list(APPEND _CHECKED_ARGS "${arg}")
    endif()
  endforeach()

  set(${VARNAME} "${_CHECKED_ARGS}")
endmacro()

macro(xa_check_cxx_linker_args VARNAME _CHECK_ARGS)
  set(_CHECKED_ARGS "")

  foreach(arg ${_CHECK_ARGS})
    cxx_linker_has_arg(${arg})
    if(LINKER_ARG_FOUND)
      list(APPEND _CHECKED_ARGS "${arg}")
    endif()
  endforeach()

  set(${VARNAME} "${_CHECKED_ARGS}")
endmacro()

set(CLANG_CHECK_SOURCES "")
macro(add_clang_check_sources SOURCES)
  foreach(_source ${SOURCES})
    cmake_path(IS_RELATIVE _source _relative_path)
    if(${_relative_path})
      list(APPEND _LOCAL_CLANG_CHECK_SOURCES_ ${CMAKE_CURRENT_SOURCE_DIR}/${_source})
    else()
      list(APPEND _LOCAL_CLANG_CHECK_SOURCES_ ${_source})
    endif()
  endforeach()
  set(CLANG_CHECK_SOURCES "${_LOCAL_CLANG_CHECK_SOURCES_};${CLANG_CHECK_SOURCES}" PARENT_SCOPE)
endmacro()

#
# Compiler args
#
set(CMAKE_CXX_VISIBILITY_PRESET "hidden")
set(CMAKE_C_VISIBILITY_PRESET "hidden")

#
# Common flags are used when building all external
# and our own code
#
set(POTENTIAL_COMMON_COMPILER_ARGS
  -fstack-protector-strong
  -fstrict-return
  -fno-strict-aliasing
  -fno-function-sections
  -fno-data-sections
  -funswitch-loops
  -Wa,-noexecstack
  -fPIC
  -O2
)

if(NOT BUILD_ARCHIVE_DSO_STUB)
  list(APPEND POTENTIAL_COMMON_COMPILER_ARGS
    -g
  )
endif()

set(POTENTIAL_COMMON_LINKER_ARGS
  -fstack-protector-strong
  LINKER:-fstrict-return
  LINKER:-z,now
  LINKER:-z,relro
  LINKER:-z,noexecstack
  LINKER:--no-undefined
  LINKER:--export-dynamic
)

# Add some options to increase security. They may mildly affect performance but they won't be big, because the features are
# assisted by the hardware.
if((CMAKE_ANDROID_ARCH_ABI STREQUAL "x86") OR (CMAKE_ANDROID_ARCH_ABI STREQUAL "x86_64"))
  # -fcf-protection=full: Enable control flow protection to counter Return Oriented Programming (ROP) and Jump Oriented Programming (JOP) attacks on many x86 architectures
  list(APPEND POTENTIAL_COMMON_COMPILER_ARGS
    -fcf-protection=full
  )
endif()

if(CMAKE_ANDROID_ARCH_ABI STREQUAL "arm64-v8a")
  # -mbranch-protection=standard: Enable branch protection to counter Return Oriented Programming (ROP) and Jump Oriented Programming (JOP) attacks on AArch64
  # In clang -mbranch-protection=standard is equivalent to -mbranch-protection=bti+pac-ret and invokes the AArch64 Branch Target Identification (BTI) and Pointer Authentication using key A (pac-ret)
  list(APPEND POTENTIAL_COMMON_COMPILER_ARGS
    -mbranch-protection=standard
  )
endif()

if(COMPILER_DIAG_COLOR)
  list(APPEND POTENTIAL_COMMON_COMPILER_ARGS
    -fdiagnostics-color=always
    -fcolor-diagnostics
  )
endif()

if(STRIP_DEBUG)
  list(APPEND POTENTIAL_COMMON_LINKER_ARGS LINKER:-S)
else()
  # When not stripping symbols, we likely want to have precise stack traces, so
  # we won't omit frame pointers
  list(APPEND POTENTIAL_COMMON_COMPILER_ARGS
    -fno-omit-frame-pointer
    -fno-limit-debug-info
  )
endif()

#
# Flags to use only when building our code
#
set(POTENTIAL_XA_COMMON_COMPILER_ARGS
  -Wall
  -Wconversion
  -Wdeprecated
  -Wduplicated-branches
  -Wduplicated-cond
  -Werror=format-security
  -Werror=return-type
  -Wextra
  -Wformat-security
  -Wformat=2
  -Wno-format-nonliteral
  -Wno-vla-cxx-extension
  -Wimplicit-fallthrough
  -Wmisleading-indentation
  -Wnull-dereference
  -Wpointer-arith
  -Wshadow
  -Wsign-compare
  -Wtrampolines
  -Wuninitialized
  -fstrict-flex-arrays=3
)

if (ENABLE_CLANG_ASAN OR ENABLE_CLANG_UBSAN)
  list(APPEND POTENTIAL_XA_COMMON_COMPILER_ARGS
    -fno-omit-frame-pointer
    -fno-optimize-sibling-calls
  )
endif()

set(POTENTIAL_XA_DSO_LINKER_ARGS
  -fpic
  -fstack-clash-protection
)

unset(SANITIZER_FLAGS)
if (ENABLE_CLANG_ASAN)
  set(SANITIZER_FLAGS -fsanitize=address)
  set(CHECKED_BUILD_INFIX "-checked+asan")
elseif(ENABLE_CLANG_UBSAN)
  set(SANITIZER_FLAGS -fsanitize=undefined)
  set(CHECKED_BUILD_INFIX "-checked+ubsan")
endif()

if(SANITIZER_FLAGS)
  message(STATUS)
  message(STATUS "Got sanitizer: ${SANITIZER_FLAGS}")
  message(STATUS)

  list(APPEND POTENTIAL_XA_COMMON_COMPILER_ARGS ${SANITIZER_FLAGS})
  list(APPEND POTENTIAL_XA_COMMON_LINKER_ARGS ${SANITIZER_FLAGS})
  list(APPEND CMAKE_REQUIRED_LINK_OPTIONS ${SANITIZER_FLAGS})
endif()

message(STATUS)
message(STATUS "Checking support for common compiler and linker args")
message(STATUS)
xa_check_cxx_args(COMMON_CXX_ARGS "${POTENTIAL_COMMON_COMPILER_ARGS}")
xa_check_c_args(COMMON_C_ARGS "${POTENTIAL_COMMON_COMPILER_ARGS}")
xa_check_cxx_linker_args(COMMON_CXX_LINKER_ARGS "${POTENTIAL_COMMON_LINKER_ARGS}")
xa_check_c_linker_args(COMMON_C_LINKER_ARGS "${POTENTIAL_COMMON_LINKER_ARGS}")

message(STATUS)
message(STATUS "Checking support for XA common compiler and linker args")
message(STATUS)
xa_check_cxx_args(XA_COMMON_CXX_ARGS "${POTENTIAL_XA_COMMON_COMPILER_ARGS}")
xa_check_c_args(XA_COMMON_C_ARGS "${POTENTIAL_XA_COMMON_COMPILER_ARGS}")
xa_check_cxx_linker_args(XA_COMMON_CXX_LINKER_ARGS "${POTENTIAL_XA_COMMON_LINKER_ARGS}")
xa_check_c_linker_args(XA_COMMON_C_LINKER_ARGS "${POTENTIAL_XA_COMMON_LINKER_ARGS}")
xa_check_c_linker_args(XA_C_DSO_LINKER_ARGS "${POTENTIAL_XA_DSO_LINKER_ARGS}")
xa_check_cxx_linker_args(XA_CXX_DSO_LINKER_ARGS "${POTENTIAL_XA_DSO_LINKER_ARGS}")

if(NOT BUILD_ARCHIVE_DSO_STUB)
  add_compile_options("$<$<COMPILE_LANGUAGE:CXX>:${COMMON_CXX_ARGS}>")
  add_compile_options("$<$<COMPILE_LANGUAGE:C>:${COMMON_C_ARGS}>")

  add_link_options("$<$<COMPILE_LANGUAGE:CXX>:${COMMON_CXX_LINKER_ARGS}>")
  add_link_options("$<$<COMPILE_LANGUAGE:C>:${COMMON_C_LINKER_ARGS}>")
endif()

#
# Helper macros
#
macro(set_static_library_suffix TARGET_NAME)
  if(STATIC_LIB_NAME_SUFFIX)
    set_target_properties(
      ${TARGET_NAME}
      PROPERTIES
      SUFFIX "${STATIC_LIB_NAME_SUFFIX}.a"
    )
  endif()
endmacro()

if(BUILD_ARCHIVE_DSO_STUB)
  add_subdirectory(common/archive-dso-stub)
else()
  add_subdirectory(common/java-interop)
  add_subdirectory(common/libstub)
  add_subdirectory(common/libunwind)
  add_subdirectory(common/lz4)

  add_subdirectory(${SOURCES_PREFIX}/runtime-base)
  add_subdirectory(${SOURCES_PREFIX}/shared)
  add_subdirectory(${SOURCES_PREFIX}/xamarin-app-stub)

  if (IS_MONO_RUNTIME)
    add_subdirectory(mono/tracing)
    add_subdirectory(mono/pinvoke-override)

    if(DEBUG_BUILD)
      add_subdirectory(mono/xamarin-debug-app-helper)
    endif()

    add_subdirectory(mono/monodroid)

    add_custom_target(run_static_analysis
    COMMAND ${ANDROID_TOOLCHAIN_ROOT}/bin/clang-check -analyze -p="${CMAKE_CURRENT_BINARY_DIR}" ${CLANG_CHECK_SOURCES} > ${CMAKE_SOURCE_DIR}/static-analysis.${ANDROID_ABI}.${CMAKE_BUILD_TYPE}.txt 2>&1
    COMMAND_EXPAND_LISTS
    WORKING_DIRECTORY "${CMAKE_CURRENT_BINARY_DIR}"
    USES_TERMINAL
  )
  endif()

  if (IS_CLR_RUNTIME)
    add_subdirectory(clr/host)
    add_subdirectory(clr/startup)
  endif()
endif()
