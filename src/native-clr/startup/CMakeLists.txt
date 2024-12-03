set(LIB_NAME xamarin-startup)
set(LIB_ALIAS xa::xamarin-startup-clr)

set(XAMARIN_STARTUP_SOURCES
  zip.cc
)
add_clang_check_sources("${XAMARIN_STARTUP_SOURCES}")

add_library(
  ${LIB_NAME}
  STATIC
  ${XAMARIN_STARTUP_SOURCES}
)

add_library(${LIB_ALIAS} ALIAS ${LIB_NAME})
set_static_library_suffix(${LIB_NAME})

target_include_directories(
  ${LIB_NAME}
  SYSTEM PRIVATE
  ${SYSROOT_CXX_INCLUDE_DIR}
)

target_compile_options(
  ${LIB_NAME}
  PRIVATE
  ${XA_COMMON_CXX_ARGS}
  # Avoid the 'warning: dynamic exception specifications are deprecated' warning from libc++ headers
  -Wno-deprecated-dynamic-exception-spec
)

target_link_directories(
  ${LIB_NAME}
  PRIVATE
  ${NET_RUNTIME_DIR}/native-clr
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
  xa::shared-clr
  -llog
)

xa_add_compile_definitions(${LIB_NAME})
xa_add_include_directories(${LIB_NAME})
