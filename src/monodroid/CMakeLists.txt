cmake_minimum_required(VERSION 3.6.0)

project(libmonodroid C CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS ON)

set(CMAKE_C_STANDARD 99)
set(CMAKE_C_STANDARD_REQUIRED ON)

option(ENABLE_NDK "Build with Android's NDK" ON)
option(STRIP_DEBUG "Strip debugging information when linking" OFF)
option(ENABLE_TIMING "Build with timing support" OFF)
option(DISABLE_DEBUG "Disable the built-in debugging code" OFF)

include(CheckIncludeFiles)
include(CheckCXXSymbolExists)
include(CheckCCompilerFlag)
include(CheckCXXCompilerFlag)
include("../../build-tools/cmake/xa_macros.cmake")

set(JAVA_INTEROP_SRC_PATH "../../external/Java.Interop/src/java-interop")
string(REPLACE "\\" "/" TOP_DIR ${CMAKE_SOURCE_DIR})
set(SOURCES_DIR ${TOP_DIR}/jni)

if(NOT DEFINED MONO_PATH)
  message(FATAL_ERROR "Please set the MONO_PATH variable on command line (-DMONO_PATH=PATH)")
else()
  string(REPLACE "\\" "/" MONO_PATH ${MONO_PATH})
endif()

if(NOT DEFINED CONFIGURATION)
  message(FATAL_ERROR "Please set the CONFIGURATION variable on command line (-DCONFIGURATION=name)")
endif()

if(NOT DEFINED CMAKE_BUILD_TYPE)
  message(FATAL_ERROR "Please set the CMAKE_BUILD_TYPE variable on command line (-DCMAKE_BUILD_TYPE=name)")
endif()

set(BIN_DIRS ${CMAKE_BUILD_TYPE} ${CONFIGURATION})
foreach(bdir ${BIN_DIRS})
  set(BIN_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../../bin/${bdir}")
  if(EXISTS ${BIN_DIR})
    message(STATUS "BIN_DIR exists")
    set(DEFAULT_BIN_DIR "${BIN_DIR}")
    break()
  endif()
endforeach(bdir)
set(XA_LIB_TOP_DIR "${DEFAULT_BIN_DIR}/lib/xamarin.android/xbuild/Xamarin/Android/lib")

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
    ${SOURCES_DIR}/inmemory-assemblies.cc
    ${JAVA_INTEROP_SRC_PATH}/java-interop-gc-bridge-mono.cc
    ${JAVA_INTEROP_SRC_PATH}/java-interop-jvm.cc
    )
endif()

if(MINGW)
  if(NOT DEFINED MINGW_DEPENDENCIES_ROOT_DIR)
    message(FATAL_ERROR "Please set the MINGW_DEPENDENCIES_ROOT_DIR variable on command line (-DMINGW_DEPENDENCIES_ROOT_DIR=PATH)")
  endif()

  if(DEFINED MINGW_TARGET_32 AND DEFINED MINGW_TARGET_64)
    message(FATAL_ERROR "Only one of the MINGW_TARGET_32 or MINGW_TARGET_64 variables can be defined")
  endif()
  if(NOT DEFINED MINGW_TARGET_32 AND NOT DEFINED MINGW_TARGET_64)
    message(FATAL_ERROR "Either MINGW_TARGET_32 or MINGW_TARGET_64 variable must be defined")
  endif()
endif()

xa_common_prepare()
# Don't put the leading '-' in options
set(TEST_COMPILER_ARGS_CPP ${XA_COMPILER_FLAGS}
  fno-rtti
  fno-exceptions
  Wall
  Wconversion
  Wdeprecated
  Werror=format-security
  Wformat
  Wformat-security
  Wsign-compare
  Wuninitialized
  )

set(TEST_COMPILER_ARGS_C ${XA_COMPILER_FLAGS})

if(CMAKE_BUILD_TYPE STREQUAL Debug)
  if(NOT DISABLE_DEBUG)
    add_definitions("-DDEBUG=1")
  endif()
endif()

foreach(arg ${TEST_COMPILER_ARGS_CPP})
  cxx_compiler_has_flag(${arg})
endforeach(arg)

foreach(arg ${TEST_COMPILER_ARGS_C})
  c_compiler_has_flag(${arg})
endforeach(arg)

set(TEST_LINKER_ARGS ${XA_LINKER_ARGS})

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
  if(ANDROID_ABI MATCHES "^(arm64-v8a|x86_64)")
    add_definitions("-DANDROID64")
  endif()

  if (ANDROID_NDK_MAJOR GREATER 17)
    # For some reason NDK 18 (and 19b2 as of this time) doesn't set this *required* macro...
    add_definitions("-D__ANDROID_API__=${ANDROID_NATIVE_API_LEVEL}")
  endif()

  if (ANDROID_NDK_MAJOR LESS 20)
    # This is missing from the current NDK releases headers (that includes NDK r19b2)
    add_definitions("-D__ANDROID_API_Q__=29")
  endif()

  link_directories("${XA_LIB_TOP_DIR}/${ANDROID_ABI}")
else()
  # MinGW needs it for {v,a}sprintf
  add_definitions("-D_GNU_SOURCE")
  if(APPLE)
    add_definitions("-DAPPLE_OS_X")
    set(CMAKE_MACOSX_RPATH 1)
    set(HOST_BUILD_NAME "host-Darwin")
  elseif(CMAKE_HOST_SYSTEM_NAME STREQUAL Linux)
    if(NOT MINGW AND NOT WIN32)
      add_definitions("-DLINUX -Dlinux -D__linux__")
      set(HOST_BUILD_NAME "host-Linux")

      if(EXISTS "/.flatpak-info")
		add_definitions("-DLINUX_FLATPAK")
      endif()
    endif()
  endif()

  if(UNIX)
    set(EXTRA_COMPILER_FLAGS "${EXTRA_COMPILER_FLAGS} -Wa,--noexecstack")
    set(EXTRA_LINKER_FLAGS "${EXTRA_LINKER_FLAGS} -shared -fpic")
  endif()

  if(WIN32 OR MINGW)
    message(STATUS "Win32 or MinGW")
    set(EXTRA_COMPILER_FLAGS "${EXTRA_COMPILER_FLAGS} -DWINDOWS -DNTDDI_VERSION=NTDDI_VISTA -D_WIN32_WINNT=_WIN32_WINNT_VISTA -fomit-frame-pointer")
    set(EXTRA_LINKER_FLAGS "${EXTRA_LINKER_FLAGS} -ldl -lmman -static -pthread -dynamic -lmincore -lmswsock -lwsock32 -lshlwapi -lpsapi -lwinmm")

    if (MINGW_TARGET_32)
      set(ANDROID_ABI "host-mxe-Win32")
      include_directories("${MINGW_DEPENDENCIES_ROOT_DIR}/x86/include")
      link_directories("${MINGW_DEPENDENCIES_ROOT_DIR}/x86/lib")
      if(CMAKE_HOST_SYSTEM_NAME STREQUAL Darwin)
        include_directories("/usr/local/opt/mingw-zlib/usr/i686-w64-mingw32/include")
      endif()
    elseif (MINGW_TARGET_64)
      set(ANDROID_ABI "host-mxe-Win64")
      include_directories("${MINGW_DEPENDENCIES_ROOT_DIR}/x86_64/include")
      link_directories("${MINGW_DEPENDENCIES_ROOT_DIR}/x86_64/lib")
      if(CMAKE_HOST_SYSTEM_NAME STREQUAL Darwin)
        include_directories("/usr/local/opt/mingw-zlib/usr/x86_64-w64-mingw32/include")
      endif()
    endif()

    link_directories("${XA_LIB_TOP_DIR}/${ANDROID_ABI}")
  endif()

  if(DEFINED HOST_BUILD_NAME)
    link_directories("${XA_LIB_TOP_DIR}/${HOST_BUILD_NAME}")
    include_directories("${DEFAULT_BIN_DIR}/include/${HOST_BUILD_NAME}")
    include_directories("${DEFAULT_BIN_DIR}/include/${HOST_BUILD_NAME}/eglib")
    include_directories("../../bin/${CONFIGURATION}/include/${HOST_BUILD_NAME}")
    include_directories("../../bin/${CONFIGURATION}/include/${HOST_BUILD_NAME}/eglib")
  endif()
endif()

if (ENABLE_TIMING)
  add_definitions("-DMONODROID_TIMING=1")
endif()

if (WIN32)
  include_directories(BEFORE "jni/win32")
endif()

add_definitions("-DHAVE_CONFIG_H")
add_definitions("-D_REENTRANT")
add_definitions("-DJI_DLL_EXPORT")
add_definitions("-DMONO_DLL_EXPORT")

set(CMAKE_C_FLAGS "${CMAKE_C_FLAGS} ${EXTRA_COMPILER_FLAGS} ${EXTRA_C_FLAGS}")
set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} ${EXTRA_COMPILER_FLAGS} ${EXTRA_CXX_FLAGS}")

include_directories("${DEFAULT_BIN_DIR}/include/mono-2.0")
include_directories("jni")
include_directories("${DEFAULT_BIN_DIR}/include")
include_directories("${DEFAULT_BIN_DIR}/include/${ANDROID_ABI}/eglib")
# This is to allow "release" builds with Debug build type and vice versa
include_directories("../../bin/${CONFIGURATION}/include")
include_directories("../../bin/${CONFIGURATION}/include/${ANDROID_ABI}/eglib")
include_directories("${MONO_PATH}/mono/eglib")
include_directories("jni/zip")
include_directories("${JAVA_INTEROP_SRC_PATH}")

check_include_files("linux/netlink.h" HAVE_LINUX_NETLINK_H)
check_include_files("linux/rtnetlink.h" HAVE_LINUX_RTNETLINK_H)
check_include_files("linux/if_arp.h" HAVE_LINUX_IF_ARP_H)

configure_file(jni/host-config.h.in ${CMAKE_CURRENT_BINARY_DIR}/include/host-config.h)
include_directories(${CMAKE_CURRENT_BINARY_DIR}/include/ ${CMAKE_SOURCE_DIR}/include)

set(MONODROID_SOURCES
  ${MONODROID_SOURCES}
  ${SOURCES_DIR}/new_delete.cc
  ${SOURCES_DIR}/android-system.cc
  ${SOURCES_DIR}/basic-android-system.cc
  ${SOURCES_DIR}/basic-utilities.cc
  ${SOURCES_DIR}/cpu-arch-detect.cc
  ${SOURCES_DIR}/debug-constants.cc
  ${SOURCES_DIR}/embedded-assemblies.cc
  ${SOURCES_DIR}/globals.cc
  ${SOURCES_DIR}/jni.c
  ${SOURCES_DIR}/logger.cc
  ${SOURCES_DIR}/monodroid-glue.cc
  ${SOURCES_DIR}/osbridge.cc
  ${SOURCES_DIR}/shared-constants.cc
  ${SOURCES_DIR}/timezones.cc
  ${SOURCES_DIR}/util.cc
  ${SOURCES_DIR}/zip/ioapi.c
  ${SOURCES_DIR}/zip/unzip.c
  ${JAVA_INTEROP_SRC_PATH}/java-interop.cc
  ${JAVA_INTEROP_SRC_PATH}/java-interop-mono.cc
  ${JAVA_INTEROP_SRC_PATH}/java-interop-util.cc
  )

if(UNIX)
  set(MONODROID_SOURCES
    ${MONODROID_SOURCES}
    ${SOURCES_DIR}/debug.cc
    ${SOURCES_DIR}/monodroid-networkinfo.cc
    ${SOURCES_DIR}/xamarin_getifaddrs.cc
    )
endif()

set(XAMARIN_APP_STUB_SOURCES ${SOURCES_DIR}/application_dso_stub.cc)
add_library(xamarin-app SHARED ${XAMARIN_APP_STUB_SOURCES})

if(NOT WIN32 AND NOT MINGW)
  set(XAMARIN_DEBUG_APP_HELPER_SOURCES
	${SOURCES_DIR}/basic-android-system.cc
	${SOURCES_DIR}/basic-utilities.cc
	${SOURCES_DIR}/cpu-arch-detect.cc
	${SOURCES_DIR}/debug-app-helper.cc
	${SOURCES_DIR}/new_delete.cc
	${SOURCES_DIR}/shared-constants.cc
	)
  add_library(xamarin-debug-app-helper SHARED ${XAMARIN_DEBUG_APP_HELPER_SOURCES})
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
if(APPLE)
  add_custom_command(
    TARGET ${MONO_ANDROID_LIB}
    POST_BUILD
    COMMAND xcrun install_name_tool -change "@rpath/libxamarin-app.dylib" "@loader_path/libxamarin-app.dylib" $<TARGET_FILE:${MONO_ANDROID_LIB}>
  )
endif()

if(MINGW_ZLIB_LIBRARY_PATH)
  set(LINK_LIBS "-lmonosgen-2.0.dll ${MINGW_ZLIB_LIBRARY_PATH} ${EXTRA_LINKER_FLAGS}")
else()
  set(LINK_LIBS "-lmonosgen-2.0 -lz ${EXTRA_LINKER_FLAGS}")
endif()

set(DEBUG_HELPER_LINK_LIBS "-ldl")
if(ENABLE_NDK)
  set(LINK_LIBS "${LINK_LIBS} -llog")
  set(DEBUG_HELPER_LINK_LIBS "${DEBUG_HELPER_LINK_LIBS} -llog")
elseif(NOT ANDROID)
  set(LINK_LIBS "-pthread ${LINK_LIBS} -ldl")
endif()

target_link_libraries(${MONO_ANDROID_LIB} ${LINK_LIBS} xamarin-app)

if(NOT WIN32 AND NOT MINGW)
  target_link_libraries(xamarin-debug-app-helper ${DEBUG_HELPER_LINK_LIBS})
endif()
