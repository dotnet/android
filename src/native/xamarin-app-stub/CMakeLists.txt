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

set(XAMARIN_APP_INCLUDE_DIRS "${CMAKE_CURRENT_SOURCE_DIR}")
set(XAMARIN_APP_INCLUDE_DIRS "${XAMARIN_APP_INCLUDE_DIRS}" PARENT_SCOPE)
target_include_directories(
  ${LIB_NAME}
  PUBLIC
  "$<BUILD_INTERFACE:${XAMARIN_APP_INCLUDE_DIRS}>"
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

target_link_options(
  ${LIB_NAME}
  PRIVATE
  ${XA_COMMON_CXX_LINKER_ARGS}
  ${XA_CXX_DSO_LINKER_ARGS}
)

target_link_libraries(
  ${LIB_NAME}
  PRIVATE
  ${SHARED_LIB_NAME}
)

if(DEBUG_BUILD)
  set(LIB_SUBDIR "Debug")
else()
  set(LIB_SUBDIR "Release")
endif()

set_target_properties(
  ${LIB_NAME}
  PROPERTIES
  LIBRARY_OUTPUT_DIRECTORY "${CMAKE_LIBRARY_OUTPUT_DIRECTORY}/${LIB_SUBDIR}"
)

xa_add_compile_definitions(${LIB_NAME})
