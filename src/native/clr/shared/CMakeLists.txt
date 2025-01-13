set(LIB_NAME xa-shared-bits)
set(LIB_ALIAS xa::shared-clr)

set(XA_SHARED_SOURCES
  helpers.cc
  log_functions.cc
)
add_clang_check_sources("${XA_SHARED_SOURCES};")

add_library(
  ${LIB_NAME}
  STATIC
  ${XA_SHARED_SOURCES}
)
add_library(${LIB_ALIAS} ALIAS ${LIB_NAME})

set_static_library_suffix(${LIB_NAME})

macro(lib_target_options TARGET_NAME)
  target_include_directories(
    ${TARGET_NAME}
    PUBLIC
    "$<BUILD_INTERFACE:${JAVA_INTEROP_INCLUDE_DIR}>"
    "$<BUILD_INTERFACE:${EXTERNAL_DIR}>"
  )

  target_link_libraries(
    ${TARGET_NAME}
    PUBLIC
    xa::java-interop-clr
    -llog
  )

  target_include_directories(
    ${TARGET_NAME}
    SYSTEM PRIVATE
    ${SYSROOT_CXX_INCLUDE_DIR}
  )

  target_compile_options(
    ${TARGET_NAME}
    PRIVATE
    ${XA_COMMON_CXX_ARGS}
  )

  xa_add_compile_definitions(${TARGET_NAME})
  xa_add_include_directories(${TARGET_NAME})
endmacro()

lib_target_options(${LIB_NAME})
