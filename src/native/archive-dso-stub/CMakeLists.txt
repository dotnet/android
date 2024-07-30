include("${CMAKE_SOURCE_DIR}/cmake/ArchiveDSOStub.cmake")

set(LIB_NAME archive-dso-stub)

set(LIB_SOURCES
  stub.cc
)

add_library(
  ${LIB_NAME}
  SHARED
  ${LIB_SOURCES}
)

set(ARCHIVE_DSO_STUB_LIB_NAME "lib${LIB_NAME}.so" PARENT_SCOPE)

target_compile_options(
  ${LIB_NAME}
  PRIVATE
  ${XA_DEFAULT_SYMBOL_VISIBILITY}
  ${XA_COMMON_CXX_ARGS}
  -nostdlib -fno-exceptions -fno-rtti -s
)

target_link_options(
  ${LIB_NAME}
  PRIVATE
  ${XA_COMMON_CXX_LINKER_ARGS}
  -nostdlib -fno-exceptions -fno-rtti -s
)

add_custom_command(
  TARGET ${LIB_NAME}
  POST_BUILD
  COMMAND ${CMAKE_STRIP} "$<TARGET_FILE:${LIB_NAME}>"
)
