# Set the minimum version of CMake that can be used
# To find the cmake version run
# $ cmake --version
# 3.20+ is required so policy CMP0128 is NEW and the C++ standard flag
# (e.g. -std=c++20) is always emitted; elf-cleaner.cpp uses std::counting_semaphore.
cmake_minimum_required(VERSION 3.20)

# Set the project name
project (elfcleaner C CXX)

# Keep ELFCLEANER_VERSION, CMAKE_CXX_STANDARD, and the PACKAGE_NAME/
# PACKAGE_VERSION/COPYRIGHT compile definitions below in sync with
# external/termux-elf-cleaner/CMakeLists.txt.
set(ELFCLEANER_VERSION "3.0.1")

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

set(CMAKE_C_STANDARD 99)
set(CMAKE_C_STANDARD_REQUIRED ON)

if(NOT CMAKE_BUILD_TYPE AND NOT CMAKE_CONFIGURATION_TYPES)
  message("Setting build type to 'Release' as none was specified.")
  set(CMAKE_BUILD_TYPE Release CACHE STRING "Choose the type of build." FORCE)
  # Set the possible values of build type for cmake-gui
  set_property(CACHE CMAKE_BUILD_TYPE PROPERTY STRINGS "Debug" "Release"
    "MinSizeRel" "Release")
endif()

set(CMAKE_C_FLAGS_RELEASE "${CMAKE_C_FLAGS_RELEASE} -O2 -Wall")
set(CMAKE_CXX_FLAGS_RELEASE "${CMAKE_CXX_FLAGS_RELEASE} -O2 -Wall")

set(ELFCLEANER_SOURCES
  ../../../external/termux-elf-cleaner/elf-cleaner.cpp
  )

# Add an executable
add_executable(elfcleaner ${ELFCLEANER_SOURCES})
target_compile_definitions(elfcleaner
  PRIVATE "COPYRIGHT=\"Copyright (C) 2022-2024 Termux and contributors.\""
  PRIVATE "PACKAGE_VERSION=\"${ELFCLEANER_VERSION}\""
  PRIVATE "PACKAGE_NAME=\"termux-elf-cleaner\""
  )
set_target_properties(elfcleaner PROPERTIES LINK_FLAGS_RELEASE -s)