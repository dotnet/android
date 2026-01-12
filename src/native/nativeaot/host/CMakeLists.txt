# Needed modules

include(CheckIncludeFile)
include(CheckCXXSymbolExists)

set(BUILD_STATIC_LIBRARY ON)

if(DEBUG_BUILD)
  # Convince NDK to really optimize our Debug builds. Without this, NDK's cmake toolchain definition
  # will force a -O0 on us and our "debug" build is not really for debugging of our native code but
  # rather for "debug" builds of user apps - it has extra code but it has to be as fast as possible.
  set(XA_COMPILER_FLAGS_DEBUG "-fno-limit-debug-info -O2")
  set(CMAKE_C_FLAGS_DEBUG ${XA_COMPILER_FLAGS_DEBUG})
  set(CMAKE_CXX_FLAGS_DEBUG ${XA_COMPILER_FLAGS_DEBUG})
endif()

# Extra source directories
set(LIBCXX_SOURCE_DIR "${THIRD_PARTY_SOURCE_DIR}/llvm/libcxx/src")
set(CXXABI_SOURCE_DIR ${CMAKE_CURRENT_SOURCE_DIR}/../cxx-abi)

# Library directories
set(XA_LIBRARY_OUTPUT_DIRECTORY "${XA_LIB_TOP_DIR}/lib/${ANDROID_RID}")

# Header checks

# Sources
string(TOLOWER ${CMAKE_BUILD_TYPE} XAMARIN_NAOT_ANDROID_SUFFIX)
set(XAMARIN_NAOT_ANDROID_LIB "naot-android${CHECKED_BUILD_INFIX}.${XAMARIN_NAOT_ANDROID_SUFFIX}")
set(XAMARIN_NAOT_ANDROID_STATIC_LIB "${XAMARIN_NAOT_ANDROID_LIB}-static")

set(CLR_SOURCES_PATH "../../clr")

set(XAMARIN_MONODROID_SOURCES
  bridge-processing.cc
  host.cc
  host-environment.cc
  host-jni.cc
  internal-pinvoke-stubs.cc

  ../runtime-base/android-system.cc

  # libc++ sources
  ${LIBCXX_SOURCE_DIR}/verbose_abort.cpp

  # C++ ABI sources
  ${CXXABI_SOURCE_DIR}/new.cc
  ${CXXABI_SOURCE_DIR}/string.cc
  ${CXXABI_SOURCE_DIR}/terminate.cc

  # Sources from CoreCLR host
  ${CLR_SOURCES_PATH}/host/bridge-processing.cc
  ${CLR_SOURCES_PATH}/host/gc-bridge.cc
  ${CLR_SOURCES_PATH}/host/host-shared.cc
  ${CLR_SOURCES_PATH}/host/internal-pinvokes-shared.cc
  ${CLR_SOURCES_PATH}/host/os-bridge.cc
  ${CLR_SOURCES_PATH}/host/runtime-util.cc
  ${CLR_SOURCES_PATH}/runtime-base/android-system-shared.cc
  ${CLR_SOURCES_PATH}/runtime-base/cpu-arch-detect.cc
  ${CLR_SOURCES_PATH}/runtime-base/logger.cc
  ${CLR_SOURCES_PATH}/runtime-base/util.cc
  ${CLR_SOURCES_PATH}/shared/helpers.cc
  ${CLR_SOURCES_PATH}/shared/log_functions.cc
)

list(APPEND LOCAL_CLANG_CHECK_SOURCES
  ${XAMARIN_MONODROID_SOURCES}
)

add_clang_check_sources("${LOCAL_CLANG_CHECK_SOURCES}")

# Build
add_library(
  ${XAMARIN_NAOT_ANDROID_LIB}
  SHARED ${XAMARIN_MONODROID_SOURCES}
)
strip_unneeded_elf_sections(${XAMARIN_NAOT_ANDROID_LIB})

if(BUILD_STATIC_LIBRARY)
  add_library(
    ${XAMARIN_NAOT_ANDROID_STATIC_LIB}
    STATIC
    ${XAMARIN_MONODROID_SOURCES}
  )
  set_static_library_suffix(${XAMARIN_NAOT_ANDROID_STATIC_LIB})
  target_compile_definitions(
    ${XAMARIN_NAOT_ANDROID_STATIC_LIB}
    PRIVATE
    DYNAMIC_RUNTIME_LINKING
  )
endif()

macro(lib_target_options TARGET_NAME)
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

  target_compile_options(
    ${TARGET_NAME}
    PRIVATE
    ${XA_DEFAULT_SYMBOL_VISIBILITY}
    ${XA_COMMON_CXX_ARGS}
  )

  target_include_directories(
    ${TARGET_NAME} BEFORE
    PRIVATE
    ${EXTERNAL_DIR}
  )

  target_include_directories(
    ${TARGET_NAME}
    SYSTEM PRIVATE
    ${SYSROOT_CXX_INCLUDE_DIR}
    ${RUNTIME_INCLUDE_DIR}
    ${TEMP_MONO_RUNTIME_INCLUDE_DIR}
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
    xa::java-interop
    -llog
  )
endmacro ()

lib_target_options(${XAMARIN_NAOT_ANDROID_LIB})
xa_add_compile_definitions(${XAMARIN_NAOT_ANDROID_LIB})
xa_add_include_directories(${XAMARIN_NAOT_ANDROID_LIB})

target_link_options(${XAMARIN_NAOT_ANDROID_LIB}
  PRIVATE
  -Wl,--version-script,${CMAKE_SOURCE_DIR}/nativeaot/libnaot-android.map.txt
  -Wl,--no-undefined-version
)

if(BUILD_STATIC_LIBRARY)
  lib_target_options(${XAMARIN_NAOT_ANDROID_STATIC_LIB})
  xa_add_compile_definitions(${XAMARIN_NAOT_ANDROID_STATIC_LIB})
  xa_add_include_directories(${XAMARIN_NAOT_ANDROID_STATIC_LIB})
endif()
