cmake_minimum_required(VERSION 3.18.1)

#
# MUST be included before project()!
#
include("../../build-tools/cmake/xa_common.cmake")

project(libsqlite-xamarin C)

include(CheckIncludeFiles)
include("../../build-tools/cmake/xa_macros.cmake")

if(NOT DEFINED SQLITE_LIBRARY_NAME)
  message(FATAL_ERROR "Please set the SQLITE_LIBRARY_NAME variable on command line (-DSQLITE_LIBRARY_NAME=base_library_name)")
endif()

if(NOT DEFINED SQLITE_SOURCE_DIR)
  message(FATAL_ERROR "Please set the SQLITE_SOURCE_DIR variable on command line (-DSQLITE_SOURCE_DIR=source_dir_path)")
endif()

if(NOT DEFINED SQLITE_OUTPUT_DIR)
  message(FATAL_ERROR "Please set the SQLITE_OUTPUT_DIR variable on command line (-DSQLITE_OUTPUT_DIR=output_dir_path)")
endif()

set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY ${SQLITE_OUTPUT_DIR})
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${SQLITE_OUTPUT_DIR})
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${SQLITE_OUTPUT_DIR})

add_compile_definitions(__ANDROID_API__=${ANDROID_NATIVE_API_LEVEL})
add_compile_definitions(NO_ANDROID_FUNCS=1)
add_compile_definitions(NDEBUG=1)
add_compile_definitions(HAVE_USLEEP=1)
add_compile_definitions(SQLITE_HAVE_ISNAN)
add_compile_definitions(SQLITE_DEFAULT_JOURNAL_SIZE_LIMIT=1048576)
add_compile_definitions(SQLITE_THREADSAFE=2)
add_compile_definitions(SQLITE_TEMP_STORE=3)
add_compile_definitions(SQLITE_POWERSAFE_OVERWRITE=1)
add_compile_definitions(SQLITE_DEFAULT_FILE_FORMAT=4)
add_compile_definitions(SQLITE_DEFAULT_AUTOVACUUM=1)
add_compile_definitions(SQLITE_ENABLE_MEMORY_MANAGEMENT=1)
add_compile_definitions(SQLITE_ENABLE_FTS3)
add_compile_definitions(SQLITE_ENABLE_FTS3_BACKWARDS)
add_compile_definitions(SQLITE_ENABLE_FTS4)
add_compile_definitions(SQLITE_OMIT_BUILTIN_TEST)
add_compile_definitions(SQLITE_OMIT_COMPILEOPTION_DIAGS)
add_compile_definitions(SQLITE_OMIT_LOAD_EXTENSION)
add_compile_definitions(SQLITE_DEFAULT_FILE_PERMISSIONS=0600)
add_compile_definitions(SQLITE_SECURE_DELETE)
add_compile_definitions(SQLITE_ENABLE_BATCH_ATOMIC_WRITE)
add_compile_definitions(BIONIC_IOCTL_NO_SIGNEDNESS_OVERLOAD)
add_compile_definitions(HAVE_MALLOC_H=1)
add_compile_definitions(USE_PREAD64)
add_compile_definitions(fdatasync=fdatasync)

if(ANDROID_NATIVE_API_LEVEL GREATER_EQUAL 17)
  add_compile_definitions(HAVE_MALLOC_USABLE_SIZE)
endif()

set(DSO_SYMBOL_VISIBILITY "default")
xa_common_prepare()

# Don't put the leading '-' in options
set(LOCAL_COMPILER_ARGS "")

if(CMAKE_BUILD_TYPE STREQUAL Debug)
  list(APPEND LOCAL_COMPILER_ARGS -g -fno-omit-frame-pointer)
else()
  list(APPEND LOCAL_COMPILER_ARGS -s -fomit-frame-pointer)
endif()

xa_check_c_flags(XA_C_FLAGS "${LOCAL_COMPILER_ARGS}")
xa_check_c_linker_flags(XA_C_LINKER_FLAGS "${LOCAL_COMPILER_ARGS}")

add_compile_options(${XA_C_FLAGS})
add_link_options(${XA_C_LINKER_FLAGS})

file(TO_CMAKE_PATH "${SQLITE_SOURCE_DIR}/dist/sqlite3.c" SQLITE_XAMARIN_SOURCES)

add_library(${SQLITE_LIBRARY_NAME} SHARED ${SQLITE_XAMARIN_SOURCES})

target_link_libraries(${SQLITE_LIBRARY_NAME} -ldl -llog)
