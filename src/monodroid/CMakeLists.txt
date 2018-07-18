cmake_minimum_required(VERSION 3.6.0)

project(libmonodroid C CXX)

option(ENABLE_NDK "Build with Android's NDK" ON)
option(STRIP_DEBUG "Strip debugging information when linking" OFF)
option(ENABLE_TIMING "Build with timing support" OFF)
option(DISABLE_DEBUG "Disable the built-in debugging code" OFF)

include(CheckIncludeFiles)
include(CheckCCompilerFlag)
include(CheckCXXCompilerFlag)

set(JAVA_INTEROP_SRC_PATH "../../external/Java.Interop/src/java-interop")
string(REPLACE "\\" "/" TOP_DIR ${CMAKE_SOURCE_DIR})

if(NOT DEFINED SGEN_BRIDGE_VERSION)
  message(FATAL_ERROR "Please set the SGEN_BRIDGE_VERSION variable on command line (-DSGEN_BRIDGE_VERSION=VERSION)")
endif()

if(NOT DEFINED MONO_PATH)
  message(FATAL_ERROR "Please set the MONO_PATH variable on command line (-DMONO_PATH=PATH)")
else()
  string(REPLACE "\\" "/" MONO_PATH ${MONO_PATH})
endif()

if(NOT ANDROID)
  if (NOT DEFINED JDK_INCLUDE)
    message(FATAL_ERROR "Please set the JDK_INCLUDE variable on command line (-DJDK_INCLUDE)")
  endif()

  string(REPLACE " " ";" JDK_INCLUDE_LIST ${JDK_INCLUDE})
  foreach(inc in ${JDK_INCLUDE_LIST})
    include_directories(${inc})
  endforeach()
  set(MONODROID_SOURCES
    ${MONODROID_SOURCES}
    ${JAVA_INTEROP_SRC_PATH}/java-interop-gc-bridge-mono.c
    )
endif()

if(MINGW)
  if(CMAKE_HOST_SYSTEM_NAME STREQUAL Linux)
    if(NOT DEFINED MINGW_DEPENDENCIES_ROOT_DIR)
      message(FATAL_ERROR "Please set the MINGW_DEPENDENCIES_ROOT_DIR variable on command line (-DMINGW_DEPENDENCIES_ROOT_DIR=PATH)")
    endif()
  endif()

  if(DEFINED MINGW_TARGET_32 AND DEFINED MINGW_TARGET_64)
    message(FATAL_ERROR "Only one of the MINGW_TARGET_32 or MINGW_TARGET_64 variables can be defined")
  endif()
  if(NOT DEFINED MINGW_TARGET_32 AND NOT DEFINED MINGW_TARGET_64)
    message(FATAL_ERROR "Either MINGW_TARGET_32 or MINGW_TARGET_64 variable must be defined")
  endif()
endif()

# Trying to perform the tests in the foreach loop unfortunately fails...
# CMake appears to run the check only once, for the first entry in the list,
# probably caching the result using the <var> name and so further tests aren't
# performed.
macro(c_compiler_has_flag _flag)
  check_c_compiler_flag(-${_flag} HAS_${_flag}_C)
  if (HAS_${_flag}_C)
    set(CMAKE_C_FLAGS "${CMAKE_C_FLAGS} -${_flag}")
  endif()
endmacro(c_compiler_has_flag)

macro(cxx_compiler_has_flag _flag)
  check_cxx_compiler_flag(-${_flag} HAS_${_flag}_CXX)
  if (HAS_${_flag}_CXX)
    set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -${_flag}")
  endif()
endmacro(cxx_compiler_has_flag)

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
  fvisibility=hidden
  fstack-protector
  Wa,--noexecstack
  Wformat
  Werror=format-security
  Wall
  mindirect-branch=thunk-inline
  mfunction-return=thunk-inline
  )

if(CMAKE_BUILD_TYPE STREQUAL Debug)
  if(NOT DISABLE_DEBUG)
    add_definitions("-DDEBUG=1")
  endif()
  set(TEST_COMPILER_ARGS ${TEST_COMPILER_ARGS} ggdb3 fno-omit-frame-pointer)
  set(CMAKE_C_FLAGS "${CMAKE_C_FLAGS} -O0")
  set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -O0")
else()
  set(TEST_COMPILER_ARGS ${TEST_COMPILER_ARGS} g fomit-frame-pointer O2)
  set(CMAKE_C_FLAGS "${CMAKE_C_FLAGS} -O2")
  set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -O2")
endif()

foreach(arg ${TEST_COMPILER_ARGS})
  c_compiler_has_flag(${arg})
  cxx_compiler_has_flag(${arg})
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

if(STRIP_DEBUG)
  set(CMAKE_SHARED_LINKER_FLAGS "${CMAKE_SHARED_LINKER_FLAGS} -Wl,-S")
endif()

if(ENABLE_NDK)
  add_definitions("-DPLATFORM_ANDROID")
  add_definitions("-DANDROID")
  add_definitions("-DLINUX -Dlinux -D__linux__")
else()
  if(APPLE)
    add_definitions("-DAPPLE_OS_X")
    set(HOST_BUILD_NAME "host-Darwin")
  elseif(CMAKE_HOST_SYSTEM_NAME STREQUAL Linux)
    if(NOT MINGW AND NOT WIN32)
      add_definitions("-D_GNU_SOURCE -DLINUX -Dlinux -D__linux__")
      set(HOST_BUILD_NAME "host-Linux")

      if(EXISTS "/.flatpak-info")
	add_definitions("-DLINUX_FLATPAK")
      endif()
    endif()

    if(MINGW_TARGET_32)
      include_directories("${MINGW_DEPENDENCIES_ROOT_DIR}/x86/include")
      link_directories("${MINGW_DEPENDENCIES_ROOT_DIR}/x86/lib")
    endif()

    if(MINGW_TARGET_64)
      include_directories("${MINGW_DEPENDENCIES_ROOT_DIR}/x86_64/include")
      link_directories("${MINGW_DEPENDENCIES_ROOT_DIR}/x86_64/lib")
    endif()
  endif()

  if(UNIX)
    set(EXTRA_COMPILER_FLAGS "${EXTRA_COMPILER_FLAGS} -Wa,--noexecstack")
    set(EXTRA_LINKER_FLAGS "${EXTRA_LINKER_FLAGS} -shared -fpic")
  endif()

  if(WIN32 OR MINGW)
    message(STATUS "Win32 or MinGW")
    set(EXTRA_COMPILER_FLAGS "${EXTRA_COMPILER_FLAGS} -DWINDOWS -DNTDDI_VERSION=NTDDI_VISTA -D_WIN32_WINNT=_WIN32_WINNT_VISTA -fomit-frame-pointer")
    set(EXTRA_LINKER_FLAGS "${EXTRA_LINKER_FLAGS} -ldl -lmman -pthread -lwsock32 -lshlwapi -lpsapi")

    if (MINGW_TARGET_32)
      set(ANDROID_ABI "host-mxe-Win32")
    elseif (MINGW_TARGET_64)
      set(ANDROID_ABI "host-mxe-Win64")
    endif()
  endif()

  if(DEFINED HOST_BUILD_NAME)
    include_directories("../../bin/${CMAKE_BUILD_TYPE}/include/${HOST_BUILD_NAME}")
    include_directories("../../bin/${CMAKE_BUILD_TYPE}/include/${HOST_BUILD_NAME}/eglib")

    if (DEFINED CONFIGURATION)
      include_directories("../../bin/${CONFIGURATION}/include/${HOST_BUILD_NAME}")
      include_directories("../../bin/${CONFIGURATION}/include/${HOST_BUILD_NAME}/eglib")
    endif()
  endif()
endif()

if (ENABLE_TIMING)
  add_definitions("-DMONODROID_TIMING=1")
endif()

add_definitions("-DHAVE_CONFIG_H")
add_definitions("-D_REENTRANT")
add_definitions("-DJI_DLL_EXPORT")
add_definitions("-DMONO_DLL_EXPORT")
add_definitions("-DDYLIB_MONO")
add_definitions("-DSGEN_BRIDGE_VERSION=${SGEN_BRIDGE_VERSION}")

set(CMAKE_C_FLAGS "${CMAKE_C_FLAGS} ${EXTRA_COMPILER_FLAGS} ${EXTRA_C_FLAGS} -std=c99")
set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} ${EXTRA_COMPILER_FLAGS} ${EXTRA_CXX_FLAGS} -std=c++11")

include_directories("jni")
include_directories("../../bin/${CMAKE_BUILD_TYPE}/include")
include_directories("../../bin/${CMAKE_BUILD_TYPE}/include/${ANDROID_ABI}/eglib")
if (DEFINED CONFIGURATION)
  # This is to allow "release" builds with Debug build type and vice versa
  include_directories("../../bin/${CONFIGURATION}/include")
  include_directories("../../bin/${CONFIGURATION}/include/${ANDROID_ABI}/eglib")
endif()
include_directories("${MONO_PATH}/mono/eglib")
include_directories("jni/zip")
include_directories("${JAVA_INTEROP_SRC_PATH}")

check_include_files("linux/netlink.h" HAVE_LINUX_NETLINK_H)
check_include_files("linux/rtnetlink.h" HAVE_LINUX_RTNETLINK_H)

set(SOURCES_DIR ${TOP_DIR}/jni)
set(MONODROID_SOURCES
  ${MONODROID_SOURCES}
  ${MONO_PATH}/support/zlib-helper.c
  ${SOURCES_DIR}/cpu-arch-detect.c
  ${SOURCES_DIR}/dylib-mono.c
  ${SOURCES_DIR}/embedded-assemblies.c
  ${SOURCES_DIR}/jni.c
  ${SOURCES_DIR}/monodroid-glue.c
  ${SOURCES_DIR}/timezones.c
  ${SOURCES_DIR}/util.c
  ${SOURCES_DIR}/zip/ioapi.c
  ${SOURCES_DIR}/zip/unzip.c
  ${JAVA_INTEROP_SRC_PATH}/java-interop.c
  ${JAVA_INTEROP_SRC_PATH}/java-interop-logger.c
  ${JAVA_INTEROP_SRC_PATH}/java-interop-mono.c
  ${JAVA_INTEROP_SRC_PATH}/java-interop-util.c
  )

if(UNIX)
  set(MONODROID_SOURCES
    ${MONODROID_SOURCES}
    ${MONO_PATH}/support/nl.c
    ${SOURCES_DIR}/debug.c
    ${SOURCES_DIR}/monodroid-networkinfo.c
    ${SOURCES_DIR}/xamarin_getifaddrs.c
    )
endif()

set(SOURCES_FRAGMENT_FILE "sources.projitems")
file(WRITE ${SOURCES_FRAGMENT_FILE} "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n\
<Project xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">\n\
  <ItemGroup>\n")

foreach(file ${MONODROID_SOURCES})
  file(APPEND ${SOURCES_FRAGMENT_FILE} "    <_MonodroidSource Include=\"${file}\" />\n")
endforeach(file)

file(APPEND ${SOURCES_FRAGMENT_FILE} "  </ItemGroup>\n\
</Project>\n")

string(TOLOWER ${CMAKE_BUILD_TYPE} MONO_ANDROID_SUFFIX)
set(MONO_ANDROID_LIB "mono-android.${MONO_ANDROID_SUFFIX}")
add_library(${MONO_ANDROID_LIB} SHARED ${MONODROID_SOURCES})

set(LINK_LIBS "-lz ${EXTRA_LINKER_FLAGS}")

if(ENABLE_NDK)
  set(LINK_LIBS "${LINK_LIBS} -llog")
elseif(NOT ANDROID)
  set(LINK_LIBS "-pthread ${LINK_LIBS} -ldl")
endif()

target_link_libraries(${MONO_ANDROID_LIB} ${LINK_LIBS})
