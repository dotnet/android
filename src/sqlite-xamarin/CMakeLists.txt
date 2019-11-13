cmake_minimum_required(VERSION 3.6.0)

project(libsqlite-xamarin C)

include(CheckIncludeFiles)
include(CheckCCompilerFlag)
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

if (ANDROID_NDK_MAJOR GREATER 17)
  # For some reason NDK 18 doesn't set this *required* macro...
  add_definitions("-D__ANDROID_API__=${ANDROID_NATIVE_API_LEVEL}")
endif()

if (ANDROID_NDK_MAJOR LESS 20)
  # This is missing from the older NDKs api-leve.h file
  add_definitions("-D__ANDROID_API_Q__=29")
endif()

add_definitions("-DNO_ANDROID_FUNCS=1")
add_definitions("-DNDEBUG=1")
add_definitions("-DHAVE_USLEEP=1")
add_definitions("-DSQLITE_HAVE_ISNAN")
add_definitions("-DSQLITE_DEFAULT_JOURNAL_SIZE_LIMIT=1048576")
add_definitions("-DSQLITE_THREADSAFE=2")
add_definitions("-DSQLITE_TEMP_STORE=3")
add_definitions("-DSQLITE_POWERSAFE_OVERWRITE=1")
add_definitions("-DSQLITE_DEFAULT_FILE_FORMAT=4")
add_definitions("-DSQLITE_DEFAULT_AUTOVACUUM=1")
add_definitions("-DSQLITE_ENABLE_MEMORY_MANAGEMENT=1")
add_definitions("-DSQLITE_ENABLE_FTS3")
add_definitions("-DSQLITE_ENABLE_FTS3_BACKWARDS")
add_definitions("-DSQLITE_ENABLE_FTS4")
add_definitions("-DSQLITE_OMIT_BUILTIN_TEST")
add_definitions("-DSQLITE_OMIT_COMPILEOPTION_DIAGS")
add_definitions("-DSQLITE_OMIT_LOAD_EXTENSION")
add_definitions("-DSQLITE_DEFAULT_FILE_PERMISSIONS=0600")
add_definitions("-DSQLITE_SECURE_DELETE")
add_definitions("-DSQLITE_ENABLE_BATCH_ATOMIC_WRITE")
add_definitions("-DBIONIC_IOCTL_NO_SIGNEDNESS_OVERLOAD")
add_definitions("-DHAVE_MALLOC_H=1")

if (ANDROID_NATIVE_API_LEVEL GREATER 16)
  add_definitions("-DHAVE_MALLOC_USABLE_SIZE")
endif()

# Disabled until we switch to building with API >= 12
if (ANDROID_NATIVE_API_LEVEL GREATER 11)
  add_definitions("-DUSE_PREAD64")
endif()

add_definitions("-Dfdatasync=fdatasync")

if (NOT ANDROID_NDK_MAJOR OR ANDROID_NDK_MAJOR LESS 20)
  add_definitions("-D__ANDROID_API_Q__=29")
endif()

set(DSO_SYMBOL_VISIBILITY "default")
xa_common_prepare()
# Don't put the leading '-' in options
set(TEST_COMPILER_ARGS ${XA_COMPILER_FLAGS})

if(CMAKE_BUILD_TYPE STREQUAL Debug)
  set(TEST_COMPILER_ARGS ${TEST_COMPILER_ARGS} ggdb3 fno-omit-frame-pointer)
  set(LOCAL_CMAKE_C_FLAGS "${LOCAL_CMAKE_C_FLAGS} -O0")
else()
  set(TEST_COMPILER_ARGS ${TEST_COMPILER_ARGS} g fomit-frame-pointer O2)
  set(LOCAL_CMAKE_C_FLAGS "${LOCAL_CMAKE_C_FLAGS} -O2")
endif()

foreach(arg ${TEST_COMPILER_ARGS})
  c_compiler_has_flag(${arg})
endforeach(arg)

set(TEST_LINKER_ARGS ${XA_LINKER_ARGS})

foreach(arg ${TEST_LINKER_ARGS})
  linker_has_flag(${arg})
endforeach(arg)

set(CMAKE_C_FLAGS "${CMAKE_C_FLAGS} ${LOCAL_C_FLAGS}")

file(TO_CMAKE_PATH "${SQLITE_SOURCE_DIR}/dist/sqlite3.c" SQLITE_XAMARIN_SOURCES)

set(SOURCES_FRAGMENT_FILE "sources.projitems")
file(WRITE ${SOURCES_FRAGMENT_FILE} "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n\
<Project xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">\n\
  <ItemGroup>\n")

foreach(file ${SQLITE_XAMARIN_SOURCES})
  file(APPEND ${SOURCES_FRAGMENT_FILE} "    <_SqliteSource Include=\"${file}\" />\n")
endforeach(file)

file(APPEND ${SOURCES_FRAGMENT_FILE} "  </ItemGroup>\n\
</Project>\n")

add_library(${SQLITE_LIBRARY_NAME} SHARED ${SQLITE_XAMARIN_SOURCES})

set(LINK_LIBS "-ldl -llog")

target_link_libraries(${SQLITE_LIBRARY_NAME} ${LINK_LIBS})
