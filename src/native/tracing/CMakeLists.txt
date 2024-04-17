set(LIB_NAME xamarin-native-tracing)
set(LIB_ALIAS xa::native-tracing)

set(NATIVE_TRACING_INCLUDE_DIRS "${CMAKE_CURRENT_SOURCE_DIR}")
set(NATIVE_TRACING_INCLUDE_DIRS "${NATIVE_TRACING_INCLUDE_DIRS}" PARENT_SCOPE)

set(XAMARIN_TRACING_SOURCES
  native-tracing.cc
)

add_library(
  ${LIB_NAME}
  SHARED
  ${XAMARIN_TRACING_SOURCES}
)

add_library(${LIB_ALIAS} ALIAS ${LIB_NAME})

target_include_directories(
  ${LIB_NAME}
  PUBLIC
  "$<BUILD_INTERFACE:${NATIVE_TRACING_INCLUDE_DIRS}>"
)

target_include_directories(
  ${LIB_NAME}
  SYSTEM PRIVATE
  ${SYSROOT_CXX_INCLUDE_DIR}
  ${MONO_RUNTIME_INCLUDE_DIR}
)

target_compile_definitions(
  ${LIB_NAME}
  PRIVATE
  XAMARIN_TRACING
)

target_compile_options(
  ${LIB_NAME}
  PRIVATE
  ${XA_COMMON_CXX_ARGS}
  # Avoid the 'warning: dynamic exception specifications are deprecated' warning from libc++ headers
  -Wno-deprecated-dynamic-exception-spec
  # Prevent genration of the .eh_frame section (we don't use exceptions and don't need it)
  -fno-asynchronous-unwind-tables
)

target_link_directories(
  ${LIB_NAME}
  PRIVATE
  ${NET_RUNTIME_DIR}/native
)

target_link_options(
  ${LIB_NAME}
  PRIVATE
  ${XA_COMMON_CXX_LINKER_ARGS}
  ${XA_CXX_DSO_LINKER_ARGS}
)

target_link_libraries(
  ${LIB_NAME}
  PRIVATE
  xa::shared-no-abi
  xa::unwind
  xa::runtime-base
  xa::java-interop
  -lmonosgen-2.0
  -llog
  ${CMAKE_SYSROOT}/usr/lib/${SYSROOT_ABI_LIB_DIR}/${TOOLCHAIN_TRIPLE}/libc++abi.a
)

xa_add_compile_definitions(${LIB_NAME})
