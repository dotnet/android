option(ENABLE_TIMING "Build with timing support" OFF)

# Needed modules

include(CheckIncludeFile)
include(CheckCXXSymbolExists)

# Paths

set(SOURCES_DIR ${CMAKE_SOURCE_DIR}/monodroid/jni)
set(BIONIC_SOURCES_DIR "${REPO_ROOT_DIR}/src-ThirdParty/bionic")
set(ROBIN_MAP_DIR "${EXTERNAL_DIR}/robin-map")

if(DEBUG_BUILD)
  # Convince NDK to really optimize our Debug builds. Without this, NDK's cmake toolchain definition
  # will force a -O0 on us and our "debug" build is not really for debugging of our native code but
  # rather for "debug" builds of user apps - it has extra code but it has to be as fast as possible.
  set(XA_COMPILER_FLAGS_DEBUG "-fno-limit-debug-info -O2")
  set(CMAKE_C_FLAGS_DEBUG ${XA_COMPILER_FLAGS_DEBUG})
  set(CMAKE_CXX_FLAGS_DEBUG ${XA_COMPILER_FLAGS_DEBUG})
endif()

# Library directories
set(XA_LIBRARY_OUTPUT_DIRECTORY "${XA_LIB_TOP_DIR}/lib/${ANDROID_RID}")
set(XA_LIBRARY_STUBS_OUTPUT_DIRECTORY "${XA_LIB_TOP_DIR}/libstubs/${ANDROID_RID}")

# Header checks

if(ENABLE_CLANG_UBSAN OR ENABLE_CLANG_ASAN)
  set(OLD_CMAKE_REQUIRED_FLAGS "${CMAKE_REQUIRED_FLAGS}")
  set(CMAKE_REQUIRED_FLAGS "${CMAKE_REQUIRED_FLAGS} -llog")
  string(STRIP "${CMAKE_REQUIRED_FLAGS}" CMAKE_REQUIRED_FLAGS)
endif()

check_include_file("linux/netlink.h" HAVE_LINUX_NETLINK_H)
check_include_file("linux/rtnetlink.h" HAVE_LINUX_RTNETLINK_H)
check_include_file("linux/if_arp.h" HAVE_LINUX_IF_ARP_H)

if(ENABLE_CLANG_UBSAN OR ENABLE_CLANG_ASAN)
  set(CMAKE_REQUIRED_FLAGS "${OLD_CMAKE_REQUIRED_FLAGS}")
endif()

# Sources
string(TOLOWER ${CMAKE_BUILD_TYPE} XAMARIN_MONO_ANDROID_SUFFIX)
set(XAMARIN_MONO_ANDROID_LIB "mono-android${CHECKED_BUILD_INFIX}.${XAMARIN_MONO_ANDROID_SUFFIX}")

set(XAMARIN_MONODROID_SOURCES
  ${SOURCES_DIR}/debug-constants.cc
  ${SOURCES_DIR}/debug.cc
  ${SOURCES_DIR}/embedded-assemblies-zip.cc
  ${SOURCES_DIR}/embedded-assemblies.cc
  ${SOURCES_DIR}/globals.cc
  ${SOURCES_DIR}/jni-remapping.cc
  ${SOURCES_DIR}/mono-log-adapter.cc
  ${SOURCES_DIR}/monodroid-glue.cc
  ${SOURCES_DIR}/monodroid-networkinfo.cc
  ${SOURCES_DIR}/monodroid-tracing.cc
  ${SOURCES_DIR}/monovm-properties.cc
  ${SOURCES_DIR}/osbridge.cc
  ${SOURCES_DIR}/pinvoke-override-api.cc
  ${SOURCES_DIR}/runtime-util.cc
  ${SOURCES_DIR}/timing.cc
  ${SOURCES_DIR}/timezones.cc
  ${SOURCES_DIR}/timing-internal.cc
  ${SOURCES_DIR}/xamarin_getifaddrs.cc
)

if(NOT DEBUG_BUILD)
  list(APPEND XAMARIN_MONODROID_SOURCES
    ${SOURCES_DIR}/xamarin-android-app-context.cc
  )
endif()

if(NOT USES_LIBSTDCPP)
  list(APPEND XAMARIN_MONODROID_SOURCES
    ${BIONIC_SOURCES_DIR}/cxa_guard.cc
  )
endif()

# Build
configure_file(jni/host-config.h.in ${CMAKE_CURRENT_BINARY_DIR}/include/host-config.h)

add_library(
  ${XAMARIN_MONO_ANDROID_LIB}
  SHARED ${XAMARIN_MONODROID_SOURCES}
)

target_compile_definitions(
  ${XAMARIN_MONO_ANDROID_LIB}
  PRIVATE
  HAVE_CONFIG_H
  HAVE_LZ4
  JI_DLL_EXPORT
  JI_NO_VISIBILITY
  MONO_DLL_EXPORT
  NET
  TSL_NO_EXCEPTIONS
)

if(DONT_INLINE)
  target_compile_definitions(
    ${XAMARIN_MONO_ANDROID_LIB}
    PRIVATE
    NO_INLINE
  )
endif()

if(DEBUG_BUILD AND NOT DISABLE_DEBUG)
  target_compile_definitions(
    ${XAMARIN_MONO_ANDROID_LIB}
    PRIVATE
    DEBUG
  )
endif()

if (ENABLE_TIMING)
  target_compile_definitions(
    ${XAMARIN_MONO_ANDROID_LIB}
    PRIVATE
    MONODROID_TIMING
  )
endif()

target_compile_options(
  ${XAMARIN_MONO_ANDROID_LIB}
  PRIVATE
  ${XA_DEFAULT_SYMBOL_VISIBILITY}
)

target_include_directories(
  ${XAMARIN_MONO_ANDROID_LIB} BEFORE
  PRIVATE
  ${CMAKE_CURRENT_BINARY_DIR}/include/ ${CMAKE_SOURCE_DIR}/include
  ${EXTERNAL_DIR}
  ${ROBIN_MAP_DIR}/include
  jni
)

target_include_directories(
  ${XAMARIN_MONO_ANDROID_LIB}
  SYSTEM PRIVATE
  ${SYSROOT_CXX_INCLUDE_DIR}
  ${MONO_RUNTIME_INCLUDE_DIR}
  ${NATIVE_TRACING_INCLUDE_DIRS}
  ${LIBUNWIND_INCLUDE_DIRS}
)

target_link_directories(
  ${XAMARIN_MONO_ANDROID_LIB}
  PRIVATE
  ${NET_RUNTIME_DIR}/native
)

target_link_options(
  ${XAMARIN_MONO_ANDROID_LIB}
  PRIVATE
  ${XA_DEFAULT_SYMBOL_VISIBILITY}
  ${XA_CXX_DSO_LINKER_ARGS}
  )

target_link_libraries(
  ${XAMARIN_MONO_ANDROID_LIB}
  ${LINK_LIBS}
  xa::xamarin-app
  xa::shared
  xa::runtime-base
  xa::java-interop
  xa::lz4
  -lmonosgen-2.0
  -llog
)

xa_add_compile_definitions(${XAMARIN_MONO_ANDROID_LIB})
