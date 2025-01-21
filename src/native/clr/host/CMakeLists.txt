# Needed modules

include(CheckIncludeFile)
include(CheckCXXSymbolExists)

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

# Sources
string(TOLOWER ${CMAKE_BUILD_TYPE} XAMARIN_NET_ANDROID_SUFFIX)
set(XAMARIN_NET_ANDROID_LIB "net-android${CHECKED_BUILD_INFIX}.${XAMARIN_NET_ANDROID_SUFFIX}")
set(XAMARIN_NET_ANDROID_STATIC_LIB "${XAMARIN_NET_ANDROID_LIB}-static")

set(XAMARIN_MONODROID_SOURCES
  host.cc
  host-jni.cc
)

list(APPEND LOCAL_CLANG_CHECK_SOURCES
  ${XAMARIN_MONODROID_SOURCES}
)

add_clang_check_sources("${LOCAL_CLANG_CHECK_SOURCES}")

# Build
add_library(
  ${XAMARIN_NET_ANDROID_LIB}
  SHARED ${XAMARIN_MONODROID_SOURCES}
)

if(BUILD_STATIC_LIBRARY)
  add_library(
    ${XAMARIN_NET_ANDROID_STATIC_LIB}
    STATIC
    ${XAMARIN_MONODROID_SOURCES}
  )
  set_static_library_suffix(${XAMARIN_NET_ANDROID_STATIC_LIB})
endif()

macro(lib_target_options TARGET_NAME)
  target_compile_definitions(
    ${TARGET_NAME}
    PRIVATE
    HAVE_CONFIG_H
    HAVE_LZ4
    JI_DLL_EXPORT
    JI_NO_VISIBILITY
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
    ${RUNTIME_INCLUDE_DIR}
    ${TEMP_MONO_RUNTIME_INCLUDE_DIR}
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
#    xa::pinvoke-override-precompiled
    xa::lz4
    -llog
  )
endmacro ()

lib_target_options(${XAMARIN_NET_ANDROID_LIB})
xa_add_compile_definitions(${XAMARIN_NET_ANDROID_LIB})
xa_add_include_directories(${XAMARIN_NET_ANDROID_LIB})

target_link_options(${XAMARIN_NET_ANDROID_LIB}
  PRIVATE
  -Wl,--version-script,${CMAKE_SOURCE_DIR}/clr/libnet-android.map.txt
  -Wl,--no-undefined-version
)

if(BUILD_STATIC_LIBRARY)
  lib_target_options(${XAMARIN_NET_ANDROID_STATIC_LIB})
  xa_add_compile_definitions(${XAMARIN_NET_ANDROID_STATIC_LIB})
  xa_add_include_directories(${XAMARIN_NET_ANDROID_STATIC_LIB})
endif()
