set(LIB_NAME archive-dso-stub)

set(LIB_SOURCES
  stub.cc
)

add_library(
  ${LIB_NAME}
  SHARED
  ${LIB_SOURCES}
)

target_compile_options(
  ${LIB_NAME}
  PRIVATE
  ${XA_COMMON_CXX_ARGS} -nostdlib -fno-exceptions -fno-rtti
)

target_link_options(
  ${LIB_NAME}
  PRIVATE
  ${XA_COMMON_CXX_LINKER_ARGS} -nostdlib -fno-exceptions -fno-rtti -s
)
