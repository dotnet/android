cmake_minimum_required(VERSION 3.21)
# CMake's -D command-line parser can strip CR; construct the exact byte in-process.
string(ASCII ${CONTROL_BYTE} CONTROL)
set(XA_STARTUP_DIAGNOSTICS_BUILD_ID "android-d549-diag1${CONTROL}" CACHE STRING "" FORCE)
include("${CMAKE_CURRENT_LIST_DIR}/../../src/native/cmake/StartupDiagnostics.cmake")
