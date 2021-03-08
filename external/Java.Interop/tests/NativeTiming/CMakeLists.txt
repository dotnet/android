set(CMAKE_OSX_ARCHITECTURES x86_64 arm64)

project(NativeTiming C)

cmake_minimum_required(VERSION 3.10.2)

foreach(dir in ${JDK_INCLUDE_LIST})
  include_directories(${dir})
endforeach()

add_library(NativeTiming SHARED timing.c)
