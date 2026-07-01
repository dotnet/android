cmake_minimum_required(VERSION 3.10.2)

set(CMAKE_TRY_COMPILE_TARGET_TYPE STATIC_LIBRARY)

project(
    java-interop-validation
    DESCRIPTION "Java.Interop native source validation"
    HOMEPAGE_URL "https://github.com/dotnet/java-interop/"
    LANGUAGES CXX C
)

set(CMAKE_C_STANDARD 11)
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
set(CMAKE_CXX_VISIBILITY_PRESET hidden)

if(ENABLE_OSX_ARCHITECTURES)
  set(CMAKE_OSX_ARCHITECTURES ${ENABLE_OSX_ARCHITECTURES})
endif()

foreach(dir ${MONO_INCLUDE_LIST})
  include_directories(${dir})
endforeach()

if(OUTPUT_DIR)
  set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY "${OUTPUT_DIR}")
endif()

add_library(
    java-interop-validation
    STATIC
    java-interop-dlfcn.cc
    java-interop-mono.cc
    java-interop-util.cc
    java-interop.cc
)
