set(LIB_NAME xa-shared-bits)
set(LIB_ALIAS xa::shared)

set(XA_SHARED_SOURCES
  helpers.cc
  log_functions.cc
  new_delete.cc
)
add_clang_check_sources("${XA_SHARED_SOURCES};")

set(XXHASH_DIR "${EXTERNAL_DIR}/xxHash")
set(CONSTEXPR_XXH3_DIR "${EXTERNAL_DIR}/constexpr-xxh3")

add_library(
  ${LIB_NAME}
  STATIC
  ${XA_SHARED_SOURCES}
  ${XA_SHARED_CXX_ABI_SOURCES}
)
add_library(${LIB_ALIAS} ALIAS ${LIB_NAME})

set_static_library_suffix(${LIB_NAME})

macro(lib_target_options TARGET_NAME)
  target_include_directories(
    ${TARGET_NAME}
    PUBLIC
    "$<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}>"
    "$<BUILD_INTERFACE:${JAVA_INTEROP_INCLUDE_DIR}>"
    "$<BUILD_INTERFACE:${EXTERNAL_DIR}>"
    "$<BUILD_INTERFACE:${CONSTEXPR_XXH3_DIR}>"
  )

  target_link_libraries(
    ${TARGET_NAME}
    PUBLIC
    xa::java-interop
    -llog
  )

  target_include_directories(
    ${TARGET_NAME}
    SYSTEM PRIVATE
    ${SYSROOT_CXX_INCLUDE_DIR}
    ${MONO_RUNTIME_INCLUDE_DIR}
  )

  target_compile_options(
    ${TARGET_NAME}
    PRIVATE
    ${XA_COMMON_CXX_ARGS}
  )

  if(DEBUG_BUILD)
    set_target_properties(
      ${TARGET_NAME}
      PROPERTIES
      ARCHIVE_OUTPUT_DIRECTORY "${CMAKE_CURRENT_BINARY_DIR}"
    )
  endif()

  xa_add_compile_definitions(${TARGET_NAME})
endmacro()

lib_target_options(${LIB_NAME})
