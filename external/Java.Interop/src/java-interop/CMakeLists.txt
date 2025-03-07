cmake_minimum_required(VERSION 3.10.2)

project(
    java-interop
    DESCRIPTION "Java.Interop native support"
    HOMEPAGE_URL "https://github.com/dotnet/java-interop/"
    LANGUAGES CXX C
)

set(CMAKE_C_STANDARD 11)
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
set(CMAKE_CXX_VISIBILITY_PRESET hidden)

option(ENABLE_MONO_INTEGRATION "Require Mono runtime" OFF)
option(ENABLE_OSX_ARCHITECTURES "macOS architectures" "")

set(CMAKE_OSX_ARCHITECTURES ${ENABLE_OSX_ARCHITECTURES})

set(JAVA_INTEROP_CORE_SOURCES
    java-interop-dlfcn.cc
    java-interop-jvm.cc
    java-interop-logger.cc
    java-interop-util.cc
    java-interop.cc
    ${JNI_C_PATH}
)
set(JAVA_INTEROP_MONO_SOURCES
    java-interop-gc-bridge-mono.cc
    java-interop-mono.cc
)

add_compile_definitions("JAVA_INTEROP_DLL_EXPORT")
add_compile_definitions("JI_DLL_EXPORT")

foreach(dir in ${JDK_INCLUDE_LIST})
  include_directories(${dir})
endforeach()

set(LINK_FLAGS "")

if(ENABLE_MONO_INTEGRATION)
  foreach(dir in ${MONO_INCLUDE_LIST})
    include_directories(${dir})
  endforeach()
  list(APPEND LINK_FLAGS ${MONO_LINK_FLAGS})
  set(JAVA_INTEROP_SOURCES ${JAVA_INTEROP_CORE_SOURCES} ${JAVA_INTEROP_MONO_SOURCES})
else()
  set(JAVA_INTEROP_SOURCES ${JAVA_INTEROP_CORE_SOURCES})
endif()

add_library(
    java-interop
    SHARED
    ${JAVA_INTEROP_SOURCES}
)
target_link_libraries(
    java-interop
    ${LINK_FLAGS}
)
