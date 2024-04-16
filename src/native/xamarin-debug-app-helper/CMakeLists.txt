set(LIB_NAME xamarin-debug-app-helper)
set(LIB_ALIAS xa::debug-app-helper)

set(XAMARIN_APP_SOURCES
  debug-app-helper.cc
)

add_library(
  ${LIB_NAME}
  SHARED
  ${XAMARIN_APP_SOURCES}
)

add_library(${LIB_ALIAS} ALIAS ${LIB_NAME})

target_include_directories(
  ${LIB_NAME}
  SYSTEM PRIVATE
  ${SYSROOT_CXX_INCLUDE_DIR}
  ${MONO_RUNTIME_INCLUDE_DIR}
)

target_compile_definitions(
  ${LIB_NAME}
  PRIVATE -DDEBUG_APP_HELPER
)

target_compile_options(
  ${LIB_NAME}
  PRIVATE
  ${XA_COMMON_CXX_ARGS}
)

target_link_directories(
  ${LIB_NAME}
  PRIVATE
  ${NET_RUNTIME_DIR}/native
)

target_link_libraries(
  ${LIB_NAME}
  PRIVATE
  xa::shared
  xa::xamarin-app
  xa::runtime-base
  xa::java-interop
  -ldl
  -llog
  -lmonosgen-2.0
)

xa_add_compile_definitions(${LIB_NAME})
