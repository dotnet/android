set(LIB_NAME_PRECOMPILED pinvoke-override-precompiled)
set(LIB_ALIAS_PRECOMPILED xa::pinvoke-override-precompiled)

set(LIB_NAME_DYNAMIC pinvoke-override-dynamic)
set(LIB_ALIAS_DYNAMIC xa::pinvoke-override-dynamic)

set(XA_PINVOKE_OVERRIDE_PRECOMPILED_SOURCES
  precompiled.cc
)
add_clang_check_sources("${XA_PINVOKE_OVERRIDE_PRECOMPILED_SOURCES}")

set(XA_PINVOKE_OVERRIDE_COMMON_SOURCES
  common.cc
)

set(XA_PINVOKE_OVERRIDE_DYNAMIC_SOURCES
  dynamic.cc
)
add_clang_check_sources("${XA_PINVOKE_OVERRIDE_DYNAMIC_SOURCES}")

list(APPEND POTENTIAL_LOCAL_COMPILER_ARGS
  -ffunction-sections
  -fdata-sections
)

xa_check_c_args(PINVOKE_OVERRIDE_CXX_ARGS "${POTENTIAL_LOCAL_COMPILER_ARGS}")

macro(create_library _libname _alias _sources)
  add_library(
    ${_libname}
    STATIC
    ${_sources}
    ${XA_PINVOKE_OVERRIDE_COMMON_SOURCES}
  )

  add_library(${_alias} ALIAS ${_libname})

  set_static_library_suffix(${_libname})

  target_compile_definitions(
    ${_libname}
    PRIVATE
    TSL_NO_EXCEPTIONS
  )

  target_compile_options(
    ${_libname}
    PRIVATE
    ${XA_COMMON_CXX_ARGS}
    ${PINVOKE_OVERRIDE_CXX_ARGS}
  )

  target_include_directories(
    ${_libname}
    PRIVATE
    ${ROBIN_MAP_DIR}/include
  )

  target_include_directories(
    ${_libname}
    PUBLIC
    "$<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}>"
  )

  target_include_directories(
    ${_libname}
    SYSTEM PRIVATE
    ${SYSROOT_CXX_INCLUDE_DIR}
    ${MONO_RUNTIME_INCLUDE_DIR}
  )

  target_link_libraries(
    ${_libname}
    PRIVATE
    ${SHARED_LIB_NAME}
    xa::xamarin-app
    xa::runtime-base
  )

  xa_add_compile_definitions(${_libname})
endmacro()

create_library(${LIB_NAME_PRECOMPILED} ${LIB_ALIAS_PRECOMPILED} ${XA_PINVOKE_OVERRIDE_PRECOMPILED_SOURCES})
create_library(${LIB_NAME_DYNAMIC} ${LIB_ALIAS_DYNAMIC} ${XA_PINVOKE_OVERRIDE_DYNAMIC_SOURCES})

target_compile_definitions(
  ${LIB_NAME_PRECOMPILED}
  PRIVATE
  PRECOMPILED
)

set_target_properties(
  ${LIB_NAME_PRECOMPILED}
  PROPERTIES
  ARCHIVE_OUTPUT_DIRECTORY "${CMAKE_CURRENT_BINARY_DIR}"
)
