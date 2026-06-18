cmake_minimum_required(VERSION 3.5)

project (maui.link C)

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

add_compile_options("${COMMON_COMPILER_OPTIONS}")

add_executable(maui.link main.c)
set_target_properties(maui.link PROPERTIES LINK_FLAGS_RELEASE -s)
