set(LIB_NAME test-jni-library)

set(LIB_SOURCES
  stub.cc
)

add_library(
  ${LIB_NAME}
  SHARED
  ${LIB_SOURCES}
)

set_target_properties(
  ${LIB_NAME}
  PROPERTIES
  LIBRARY_OUTPUT_DIRECTORY "${XA_TEST_OUTPUT_DIR}/${ANDROID_RID}"
)

target_compile_options(
  ${LIB_NAME}
  PRIVATE
  ${XA_DEFAULT_SYMBOL_VISIBILITY}
  ${XA_COMMON_CXX_ARGS}
  -nostdlib -fno-exceptions -fno-rtti
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
