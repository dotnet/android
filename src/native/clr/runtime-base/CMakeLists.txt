# First generate some code

#
# Must be the same value as DSOWrapperGenerator.PayloadSectionAlignment in src/Xamarin.Android.Build.Tasks/Utilities/DSOWrapperGenerator.cs
#
set(ARCHIVE_DSO_STUB_PAYLOAD_SECTION_ALIGNMENT 0x4000)

file(COPY "${XA_ARCHIVE_STUB_OUTPUT_DIRECTORY}/${ARCHIVE_DSO_STUB_LIB_FILE_NAME}" DESTINATION ${CMAKE_CURRENT_BINARY_DIR})
set(ARCHIVE_DSO_STUB_LIB_PATH "${CMAKE_CURRENT_BINARY_DIR}/${ARCHIVE_DSO_STUB_LIB_FILE_NAME}")

# Emulate what we do when embedding something inside the ELF file
set(PAYLOAD_PATH ${CMAKE_CURRENT_SOURCE_DIR}/CMakeLists.txt)

execute_process(
  COMMAND ${CMAKE_OBJCOPY} --add-section payload=${PAYLOAD_PATH} ${ARCHIVE_DSO_STUB_LIB_PATH}
  WORKING_DIRECTORY ${CMAKE_CURRENT_BINARY_DIR}
  ECHO_ERROR_VARIABLE
  COMMAND_ERROR_IS_FATAL ANY
)

execute_process(
  COMMAND ${CMAKE_OBJCOPY} --set-section-flags payload=readonly,data --set-section-alignment payload=${ARCHIVE_DSO_STUB_PAYLOAD_SECTION_ALIGNMENT} ${ARCHIVE_DSO_STUB_LIB_PATH}
  WORKING_DIRECTORY ${CMAKE_CURRENT_BINARY_DIR}
  ECHO_ERROR_VARIABLE
  COMMAND_ERROR_IS_FATAL ANY
)

execute_process(
  COMMAND ${CMAKE_READELF} --file-header --section-headers ${ARCHIVE_DSO_STUB_LIB_PATH} --elf-output-style=JSON
  WORKING_DIRECTORY ${CMAKE_CURRENT_BINARY_DIR}
  OUTPUT_VARIABLE ARCHIVE_DSO_STUB_HEADER_JSON
  ECHO_ERROR_VARIABLE
  COMMAND_ERROR_IS_FATAL ANY
)
string(JSON SECTION_HEADER_ENTRY_SIZE GET "${ARCHIVE_DSO_STUB_HEADER_JSON}" 0 "ElfHeader" "SectionHeaderEntrySize")
string(JSON SECTION_HEADER_ENTRY_COUNT GET "${ARCHIVE_DSO_STUB_HEADER_JSON}" 0 "ElfHeader" "SectionHeaderCount")

math(EXPR PAYLOAD_SECTION_INDEX "${SECTION_HEADER_ENTRY_COUNT} - 1")
string(JSON PAYLOAD_SECTION_OFFSET GET "${ARCHIVE_DSO_STUB_HEADER_JSON}" 0 "Sections" ${PAYLOAD_SECTION_INDEX} "Section" "Offset")
file(REMOVE ${ARCHIVE_DSO_STUB_LIB_PATH})

message(STATUS "Archive DSO stub header entry size: ${SECTION_HEADER_ENTRY_SIZE}; section count: ${SECTION_HEADER_ENTRY_COUNT}; payload offset: ${PAYLOAD_SECTION_OFFSET}")
configure_file(
  archive-dso-stub-config.hh.in
  ${CMAKE_CURRENT_BINARY_DIR}/include/archive-dso-stub-config.hh
  USE_SOURCE_PERMISSIONS
)

set(LIB_NAME runtime-base)
set(LIB_ALIAS xa::runtime-base)

set(XA_RUNTIME_BASE_SOURCES
  android-system.cc
  cpu-arch-detect.cc
  logger.cc
  timing.cc
  timing-internal.cc
  util.cc
)
add_clang_check_sources("${XA_RUNTIME_BASE_SOURCES}")

list(APPEND POTENTIAL_LOCAL_COMPILER_ARGS
  -ffunction-sections
  -fdata-sections
)

xa_check_c_args(RUNTIME_BASE_CXX_ARGS "${POTENTIAL_LOCAL_COMPILER_ARGS}")

add_library(
  ${LIB_NAME}
  STATIC
  ${XA_RUNTIME_BASE_SOURCES}
)

add_library(${LIB_ALIAS} ALIAS ${LIB_NAME})

set_static_library_suffix(${LIB_NAME})

target_compile_options(
  ${LIB_NAME}
  PRIVATE
  ${XA_COMMON_CXX_ARGS}
  ${RUNTIME_BASE_CXX_ARGS}
)

target_include_directories(
  ${LIB_NAME}
  PUBLIC
  "$<BUILD_INTERFACE:${CMAKE_CURRENT_BINARY_DIR}/include>"
)

target_include_directories(
  ${LIB_NAME}
  SYSTEM PRIVATE
  ${SYSROOT_CXX_INCLUDE_DIR}
)

target_link_libraries(
  ${LIB_NAME}
  PRIVATE
  xa::shared
  xa::xamarin-app
)

xa_add_compile_definitions(${LIB_NAME})
xa_add_include_directories(${LIB_NAME})
