cmake_minimum_required(VERSION 3.6.0)

project(libsqlite-xamarin C)

include(CheckIncludeFiles)
include(CheckCCompilerFlag)

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

add_definitions("-DNO_ANDROID_FUNCS=1")
add_definitions("-DHAVE_POSIX_FALLOCATE=0")
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
add_definitions("-DHAVE_MALLOC_H=1")

macro(c_compiler_has_flag _flag)
  check_c_compiler_flag(-${_flag} HAS_${_flag}_C)
  if (HAS_${_flag}_C)
    set(CMAKE_C_FLAGS "${CMAKE_C_FLAGS} -${_flag}")
  endif()
endmacro(c_compiler_has_flag)

macro(linker_has_flag _flag)
  set(CMAKE_REQUIRED_FLAGS "-${_flag}")
  check_c_compiler_flag("" HAS_${_flag}_LINKER)
  if(HAS_${_flag}_LINKER)
    set(CMAKE_SHARED_LINKER_FLAGS "${CMAKE_SHARED_LINKER_FLAGS} -${_flag}")
  endif()
endmacro()

# Don't put the leading '-' in options
set(TEST_COMPILER_ARGS
  fno-strict-aliasing
  ffunction-sections
  funswitch-loops
  finline-limit=300
  fstack-protector
  Wa,--noexecstack
  Wformat
  Werror=format-security
  Wall
  mindirect-branch=thunk-inline
  mfunction-return=thunk-inline
  )

if(CMAKE_BUILD_TYPE STREQUAL Debug)
  set(TEST_COMPILER_ARGS ${TEST_COMPILER_ARGS} ggdb3 fno-omit-frame-pointer)
  set(CMAKE_C_FLAGS "${CMAKE_C_FLAGS} -O0")
else()
  set(TEST_COMPILER_ARGS ${TEST_COMPILER_ARGS} g fomit-frame-pointer O2)
  set(CMAKE_C_FLAGS "${CMAKE_C_FLAGS} -O2")
endif()

foreach(arg ${TEST_COMPILER_ARGS})
  c_compiler_has_flag(${arg})
endforeach(arg)

set(TEST_LINKER_ARGS
  Wl,-z,now
  Wl,-z,relro
  Wl,-z,noexecstack
  Wl,--no-undefined
  )

if(MINGW)
  set(TEST_LINKER_ARGS
    ${TEST_LINKER_ARGS}
    Wl,--export-all-symbols
    )
else()
  set(TEST_LINKER_ARGS
    ${TEST_LINKER_ARGS}
    Wl,--export-dynamic
    )
endif()

foreach(arg ${TEST_LINKER_ARGS})
  linker_has_flag(${arg})
endforeach(arg)

set(SQLITE_XAMARIN_SOURCES
  ${SQLITE_SOURCE_DIR}/dist/sqlite3.c
  )

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

set(LINK_LIBS "-ldl")

if(ENABLE_NDK)
  set(LINK_LIBS "${LINK_LIBS} -llog")
elseif(NOT ANDROID)
  set(LINK_LIBS "-pthread ${LINK_LIBS}")
endif()

target_link_libraries(${SQLITE_LIBRARY_NAME} ${LINK_LIBS})
