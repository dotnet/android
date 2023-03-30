cmake_minimum_required(VERSION 3.18.1)

#
# MUST be included before project()!
#
include("../../build-tools/cmake/xa_common.cmake")

project(libunwind-xamarin C)

set(CMAKE_C_STANDARD 11)

include(CheckCSourceCompiles)
include(CheckIncludeFiles)
include(CheckSymbolExists)
include("../../build-tools/cmake/xa_macros.cmake")

if(NOT DEFINED LIBUNWIND_LIBRARY_NAME)
  message(FATAL_ERROR "Please set the LIBUNWIND_LIBRARY_NAME variable on command line (-DLIBUNWIND_LIBRARY_NAME=base_library_name)")
endif()

if(NOT DEFINED LIBUNWIND_SOURCE_DIR)
  message(FATAL_ERROR "Please set the LIBUNWIND_SOURCE_DIR variable on command line (-DLIBUNWIND_SOURCE_DIR=source_dir_path)")
endif()

if(NOT DEFINED LIBUNWIND_OUTPUT_DIR)
  message(FATAL_ERROR "Please set the LIBUNWIND_OUTPUT_DIR variable on command line (-DLIBUNWIND_OUTPUT_DIR=output_dir_path)")
endif()

set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY ${LIBUNWIND_OUTPUT_DIR})
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${LIBUNWIND_OUTPUT_DIR})
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${LIBUNWIND_OUTPUT_DIR})

if(CMAKE_ANDROID_ARCH_ABI STREQUAL arm64-v8a)
  set(TARGET_AARCH64 TRUE)
  set(arch aarch64)
elseif(CMAKE_ANDROID_ARCH_ABI STREQUAL armeabi-v7a)
  set(TARGET_ARM TRUE)
  set(arch arm)
elseif(CMAKE_ANDROID_ARCH_ABI STREQUAL x86_64)
  set(TARGET_AMD64 TRUE)
  set(arch x86_64)
elseif(CMAKE_ANDROID_ARCH_ABI STREQUAL x86)
  set(TARGET_X86 TRUE)
  set(arch x86)
else()
endif()

set(DSO_SYMBOL_VISIBILITY "hidden")
xa_common_prepare()

if(CMAKE_BUILD_TYPE STREQUAL Debug)
  list(APPEND LOCAL_COMPILER_ARGS -g -fno-omit-frame-pointer)
else()
  list(APPEND LOCAL_COMPILER_ARGS -s -fomit-frame-pointer)
  add_compile_definitions(NDEBUG)
endif()

xa_check_c_flags(XA_C_FLAGS "${LOCAL_COMPILER_ARGS}")
xa_check_c_linker_flags(XA_C_LINKER_FLAGS "${LOCAL_COMPILER_ARGS}")

add_compile_options(${XA_C_FLAGS})
add_link_options(${XA_C_LINKER_FLAGS})

add_compile_definitions(HAVE_CONFIG_H)
add_compile_definitions(_GNU_SOURCE)

# Detect include files
set(CMAKE_REQUIRED_DEFINITIONS -D_GNU_SOURCE)

check_include_files(asm/vsyscall.h HAVE_ASM_VSYSCALL_H)
check_include_files(byteswap.h HAVE_BYTESWAP_H)
check_include_files(elf.h HAVE_ELF_H)
check_include_files(endian.h HAVE_ENDIAN_H)
check_include_files(link.h HAVE_LINK_H)
check_include_files(sys/endian.h HAVE_SYS_ENDIAN_H)
check_include_files(sys/link.h HAVE_SYS_LINK_H)
check_include_files(sys/param.h HAVE_SYS_PARAM_H)
check_include_files(sys/syscal.h HAVE_SYS_SYSCALL_H)

# Detect functions
check_symbol_exists(mincore "sys/mman.h" HAVE_MINCORE)
check_symbol_exists(pipe2 "fcntl.h;unistd.h" HAVE_PIPE2)

# TODO: consider enabling zlib

configure_file(config.h.in ${CMAKE_CURRENT_BINARY_DIR}/include/config.h)
configure_file(${LIBUNWIND_SOURCE_DIR}/include/libunwind-common.h.in ${CMAKE_CURRENT_BINARY_DIR}/include/libunwind-common.h)
configure_file(${LIBUNWIND_SOURCE_DIR}/include/libunwind.h.in ${CMAKE_CURRENT_BINARY_DIR}/include/libunwind.h)
configure_file(${LIBUNWIND_SOURCE_DIR}/include/tdep/libunwind_i.h.in ${CMAKE_CURRENT_BINARY_DIR}/include/tdep/libunwind_i.h)

add_library(${LIBUNWIND_LIBRARY_NAME}
  STATIC
  ${LIBUNWIND_XAMARIN_SOURCES}
)
