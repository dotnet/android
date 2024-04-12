set(LIB_NAME xa-shared-bits)
set(LIB_ALIAS xa::shared)

set(XA_SHARED_SOURCES
  cxx-abi/string.cc
  cxx-abi/terminate.cc
  helpers.cc
  new_delete.cc
)

add_library(
  ${LIB_NAME}
  STATIC
  ${XA_SHARED_SOURCES}
)

add_library(${LIB_ALIAS} ALIAS ${LIB_NAME})

target_include_directories(
  ${LIB_NAME}
  PUBLIC
  "$<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}>"
  "$<BUILD_INTERFACE:${JAVA_INTEROP_INCLUDE_DIR}>"
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

set_target_properties(
  ${LIB_NAME}
  PROPERTIES
  ARCHIVE_OUTPUT_DIRECTORY "${CMAKE_CURRENT_BINARY_DIR}"
)

xa_add_compile_definitions(${LIB_NAME})
