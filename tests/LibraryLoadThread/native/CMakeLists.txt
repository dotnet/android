cmake_minimum_required(VERSION 3.18.1)

project("ThreadLoad")

add_library(thread-load SHARED
            library.cc)

target_link_libraries(thread-load
                      android
                      log)
