set(LIB_NAME xamarin-app)
set(LIB_ALIAS xa::xamarin-app)

set(XAMARIN_APP_SOURCES
  application_dso_stub.cc
)

add_library(
  ${LIB_NAME}
  SHARED
  ${XAMARIN_APP_SOURCES}
)

add_library(${LIB_ALIAS} ALIAS ${LIB_NAME})

target_include_directories(
  ${LIB_NAME}
  PUBLIC
  "$<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}>"
)

target_include_directories(
  ${LIB_NAME}
  SYSTEM PRIVATE
  ${SYSROOT_CXX_INCLUDE_DIR}
  ${MONO_RUNTIME_INCLUDE_DIR}
)

target_compile_options(
  ${LIB_NAME}
  PRIVATE
  ${XA_COMMON_CXX_ARGS}
)

target_link_libraries(
  ${LIB_NAME}
  PRIVATE
  xa::shared
)

set_target_properties(
  ${LIB_NAME}
  PROPERTIES
  LIBRARY_OUTPUT_DIRECTORY "${CMAKE_CURRENT_BINARY_DIR}"
)

xa_add_compile_definitions(${LIB_NAME})
