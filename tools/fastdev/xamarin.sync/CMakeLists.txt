# Set the minimum version of CMake that can be used
# To find the cmake version run
# $ cmake --version
cmake_minimum_required(VERSION 3.5)

# Set the project name
project (xamarin.sync C CXX)
#set(CMAKE_FIND_LIBRARY_SUFFIXES ".a")
#find_package(ZLIB REQUIRED)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS ON)

set(CMAKE_C_STANDARD 99)
set(CMAKE_C_STANDARD_REQUIRED ON)

option(ENABLE_NDK "Build with Android's NDK" OFF)

if(ENABLE_NDK)
  add_definitions("-DPLATFORM_ANDROID")
  add_definitions("-DANDROID")
  add_definitions("-DLINUX -Dlinux -D__linux__")
endif()

if(NOT CMAKE_BUILD_TYPE AND NOT CMAKE_CONFIGURATION_TYPES)
  message("Setting build type to 'Release' as none was specified.")
  set(CMAKE_BUILD_TYPE Release CACHE STRING "Choose the type of build." FORCE)
  # Set the possible values of build type for cmake-gui
  set_property(CACHE CMAKE_BUILD_TYPE PROPERTY STRINGS "Debug" "Release"
    "MinSizeRel" "Release")
endif()

set(COMMON_COMPILER_OPTIONS
  -O2
  -Wall
  -fstack-protector-strong
  -fstrict-return
  -fno-omit-frame-pointer
)

add_compile_options("$<$<COMPILE_LANGUAGE:CXX>:${COMMON_COMPILER_OPTIONS}>")
add_compile_options("$<$<COMPILE_LANGUAGE:C>:${COMMON_COMPILER_OPTIONS}>")
#set(CMAKE_RUNTIME_OUTPUT_DIRECTORY "../../../../out/${ANDROID_ABI}")

if(NOT DEFINED ${LZ4_SRC_DIR})
  set(LZ4_SRC_DIR, "../../../src-ThirdParty/lz4")
endif()
set(LZ4_INCLUDE_DIR ${LZ4_SRC_DIR})
set(LZ4_SOURCES
  "${LZ4_SRC_DIR}/lz4.c"
  )

include_directories(${LZ4_INCLUDE_DIR})

set(XAMARINCOPY_SOURCES
  ${LZ4_SOURCES}
  main.cc
  )

# Add an executable
add_executable(xamarin.sync ${XAMARINCOPY_SOURCES})
set_target_properties(xamarin.sync PROPERTIES LINK_FLAGS_RELEASE -s)

#if(NOT APPLE)
#  target_link_libraries(xamarin.sync -static)
#endif()

#target_link_libraries(xamarin.sync "${ZLIB_LIBRARIES}")
#include_directories("${ZLIB_INCLUDE_DIRS}")