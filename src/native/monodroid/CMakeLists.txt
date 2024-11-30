option(ENABLE_TIMING "Build with timing support" OFF)

# First generate some code

#
# Must be the same value as DSOWrapperGenerator.PayloadSectionAlignment in src/Xamarin.Android.Build.Tasks/Utilities/DSOWrapperGenerator.cs
#
set(ARCHIVE_DSO_STUB_PAYLOAD_SECTION_ALIGNMENT 0x4000)

file(COPY "${XA_ARCHIVE_STUB_OUTPUT_DIRECTORY}/${ARCHIVE_DSO_STUB_LIB_FILE_NAME}" DESTINATION ${CMAKE_CURRENT_BINARY_DIR})
set(ARCHIVE_DSO_STUB_LIB_PATH "${CMAKE_CURRENT_BINARY_DIR}/${ARCHIVE_DSO_STUB_LIB_FILE_NAME}")

# Emulate what we do when embedding something inside the ELF file
set(PAYLOAD_PATH ${CMAKE_CURRENT_SOURCE_DIR}/CMakeLists.txt)

execute_process(
  COMMAND ${CMAKE_OBJCOPY} --add-section payload=${PAYLOAD_PATH} ${ARCHIVE_DSO_STUB_LIB_PATH}
  WORKING_DIRECTORY ${CMAKE_CURRENT_BINARY_DIR}
  ECHO_ERROR_VARIABLE
  COMMAND_ERROR_IS_FATAL ANY
)

execute_process(
  COMMAND ${CMAKE_OBJCOPY} --set-section-flags payload=readonly,data --set-section-alignment payload=${ARCHIVE_DSO_STUB_PAYLOAD_SECTION_ALIGNMENT} ${ARCHIVE_DSO_STUB_LIB_PATH}
  WORKING_DIRECTORY ${CMAKE_CURRENT_BINARY_DIR}
  ECHO_ERROR_VARIABLE
  COMMAND_ERROR_IS_FATAL ANY
)

execute_process(
  COMMAND ${CMAKE_READELF} --file-header --section-headers ${ARCHIVE_DSO_STUB_LIB_PATH} --elf-output-style=JSON
  WORKING_DIRECTORY ${CMAKE_CURRENT_BINARY_DIR}
  OUTPUT_VARIABLE ARCHIVE_DSO_STUB_HEADER_JSON
  ECHO_ERROR_VARIABLE
  COMMAND_ERROR_IS_FATAL ANY
)
string(JSON SECTION_HEADER_ENTRY_SIZE GET "${ARCHIVE_DSO_STUB_HEADER_JSON}" 0 "ElfHeader" "SectionHeaderEntrySize")
string(JSON SECTION_HEADER_ENTRY_COUNT GET "${ARCHIVE_DSO_STUB_HEADER_JSON}" 0 "ElfHeader" "SectionHeaderCount")

math(EXPR PAYLOAD_SECTION_INDEX "${SECTION_HEADER_ENTRY_COUNT} - 1")
string(JSON PAYLOAD_SECTION_OFFSET GET "${ARCHIVE_DSO_STUB_HEADER_JSON}" 0 "Sections" ${PAYLOAD_SECTION_INDEX} "Section" "Offset")
file(REMOVE ${ARCHIVE_DSO_STUB_LIB_PATH})

message(STATUS "Archive DSO stub header entry size: ${SECTION_HEADER_ENTRY_SIZE}; section count: ${SECTION_HEADER_ENTRY_COUNT}; payload offset: ${PAYLOAD_SECTION_OFFSET}")
configure_file(
  archive-dso-stub-config.hh.in
  ${CMAKE_CURRENT_BINARY_DIR}/include/archive-dso-stub-config.hh
  USE_SOURCE_PERMISSIONS
)

# Needed modules

include(CheckIncludeFile)
include(CheckCXXSymbolExists)

# Paths

set(BIONIC_SOURCES_DIR "${REPO_ROOT_DIR}/src-ThirdParty/bionic")
set(BUILD_STATIC_LIBRARY OFF)

if(DEBUG_BUILD)
  # Convince NDK to really optimize our Debug builds. Without this, NDK's cmake toolchain definition
  # will force a -O0 on us and our "debug" build is not really for debugging of our native code but
  # rather for "debug" builds of user apps - it has extra code but it has to be as fast as possible.
  set(XA_COMPILER_FLAGS_DEBUG "-fno-limit-debug-info -O2")
  set(CMAKE_C_FLAGS_DEBUG ${XA_COMPILER_FLAGS_DEBUG})
  set(CMAKE_CXX_FLAGS_DEBUG ${XA_COMPILER_FLAGS_DEBUG})
elseif(NOT ANALYZERS_ENABLED)
  set(BUILD_STATIC_LIBRARY OFF) # Turn off for now, until we implement dynamic runtime linking at app build time
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
set(XAMARIN_MONO_ANDROID_STATIC_LIB "${XAMARIN_MONO_ANDROID_LIB}-static")

set(XAMARIN_MONODROID_SOURCES
  debug-constants.cc
  debug.cc
  embedded-assemblies-zip.cc
  embedded-assemblies.cc
  globals.cc
  internal-pinvokes.cc
  jni-remapping.cc
  mono-log-adapter.cc
  monodroid-glue.cc
  monodroid-networkinfo.cc
  monodroid-tracing.cc
  monovm-properties.cc
  osbridge.cc
  performance-methods.cc
  runtime-util.cc
  timezones.cc
  xamarin_getifaddrs.cc
)

list(APPEND LOCAL_CLANG_CHECK_SOURCES
  ${XAMARIN_MONODROID_SOURCES}
)

if(NOT DEBUG_BUILD)
  list(APPEND XAMARIN_MONODROID_SOURCES
    xamarin-android-app-context.cc
  )

  list(APPEND LOCAL_CLANG_CHECK_SOURCES
    xamarin-android-app-context.cc
  )
endif()
add_clang_check_sources("${LOCAL_CLANG_CHECK_SOURCES}")

if(NOT USES_LIBSTDCPP)
  list(APPEND XAMARIN_MONODROID_SOURCES
    ${BIONIC_SOURCES_DIR}/cxa_guard.cc
  )
endif()

# Build
configure_file(host-config.h.in ${CMAKE_CURRENT_BINARY_DIR}/include/host-config.h)

add_library(
  ${XAMARIN_MONO_ANDROID_LIB}
  SHARED ${XAMARIN_MONODROID_SOURCES}
)

if(BUILD_STATIC_LIBRARY)
  add_library(
    ${XAMARIN_MONO_ANDROID_STATIC_LIB}
    STATIC
    ${XAMARIN_MONODROID_SOURCES}
  )
  set_static_library_suffix(${XAMARIN_MONO_ANDROID_STATIC_LIB})
endif()

macro(lib_target_options TARGET_NAME)
  target_compile_definitions(
    ${TARGET_NAME}
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
      ${TARGET_NAME}
      PRIVATE
      NO_INLINE
    )
  endif()

  if(DEBUG_BUILD AND NOT DISABLE_DEBUG)
    target_compile_definitions(
      ${TARGET_NAME}
      PRIVATE
      DEBUG
    )
  endif()

  if (ENABLE_TIMING)
    target_compile_definitions(
      ${TARGET_NAME}
      PRIVATE
      MONODROID_TIMING
    )
  endif()

  target_compile_options(
    ${TARGET_NAME}
    PRIVATE
    ${XA_DEFAULT_SYMBOL_VISIBILITY}
    ${XA_COMMON_CXX_ARGS}
  )

  target_include_directories(
    ${TARGET_NAME} BEFORE
    PRIVATE
    ${CMAKE_CURRENT_BINARY_DIR}/include
    ${EXTERNAL_DIR}
    ${ROBIN_MAP_DIR}/include
  )

  target_include_directories(
    ${TARGET_NAME}
    SYSTEM PRIVATE
    ${SYSROOT_CXX_INCLUDE_DIR}
    ${MONO_RUNTIME_INCLUDE_DIR}
    ${NATIVE_TRACING_INCLUDE_DIRS}
    ${LIBUNWIND_INCLUDE_DIRS}
  )

  target_link_directories(
    ${TARGET_NAME}
    PRIVATE
    ${NET_RUNTIME_DIR}/native
  )

  target_link_options(
    ${TARGET_NAME}
    PRIVATE
    ${XA_DEFAULT_SYMBOL_VISIBILITY}
    ${XA_COMMON_CXX_LINKER_ARGS}
    ${XA_CXX_DSO_LINKER_ARGS}
  )

  target_link_libraries(
    ${TARGET_NAME}
    ${LINK_LIBS}
    xa::xamarin-app
    ${SHARED_LIB_NAME}
    xa::runtime-base
    xa::java-interop
    xa::pinvoke-override-precompiled
    xa::lz4
    -lmonosgen-2.0
    -llog
  )
endmacro ()

lib_target_options(${XAMARIN_MONO_ANDROID_LIB})
xa_add_compile_definitions(${XAMARIN_MONO_ANDROID_LIB})

if(BUILD_STATIC_LIBRARY)
  lib_target_options(${XAMARIN_MONO_ANDROID_STATIC_LIB})
  xa_add_compile_definitions(${XAMARIN_MONO_ANDROID_STATIC_LIB})
endif()
