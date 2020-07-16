# Trying to perform the tests in the foreach loop unfortunately fails...
# CMake appears to run the check only once, for the first entry in the list,
# probably caching the result using the <var> name and so further tests aren't
# performed.
macro(c_compiler_has_flag _flag)
  string(REGEX REPLACE "-|,|=" "_" flag_name ${_flag})
  check_c_compiler_flag(-${_flag} HAS_${flag_name}_C)
  if (HAS_${flag_name}_C)
    set(CMAKE_C_FLAGS "${CMAKE_C_FLAGS} -${_flag}")
  endif()
endmacro(c_compiler_has_flag)

macro(cxx_compiler_has_flag _flag)
  string(REGEX REPLACE "-|,|=" "_" flag_name ${_flag})
  check_cxx_compiler_flag(-${_flag} HAS_${flag_name}_CXX)
  if (HAS_${flag_name}_CXX)
    set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -${_flag}")
  endif()
endmacro(cxx_compiler_has_flag)

macro(linker_has_flag _flag)
  string(REGEX REPLACE "-|,|=" "_" flag_name ${_flag})
  set(CMAKE_REQUIRED_FLAGS "-${_flag}")
  check_c_compiler_flag("" HAS_${flag_name}_LINKER)
  if(HAS_${flag_name}_LINKER)
    set(CMAKE_SHARED_LINKER_FLAGS "${CMAKE_SHARED_LINKER_FLAGS} -${_flag}")
  endif()
endmacro()

macro(xa_common_prepare)
  if(NOT DSO_SYMBOL_VISIBILITY)
    set(DSO_SYMBOL_VISIBILITY "hidden")
  endif()

  #
  # Currently not supported by NDK clang, but worth considering when it is eventually supported:
  #
  #  -fsanitize=safe-stack
  #

  # Don't put the leading '-' in options
  set(XA_COMPILER_FLAGS
    fno-strict-aliasing
    ffunction-sections
    funswitch-loops
    finline-limit=300
    fvisibility=${DSO_SYMBOL_VISIBILITY}
    fstack-protector-strong
    fstrict-return
    Wa,-noexecstack
    fPIC
    g
    fomit-frame-pointer
    O2
    )

  if(CMAKE_BUILD_TYPE STREQUAL Release)
    add_definitions("-DRELEASE")
  endif()

  set(XA_LINKER_ARGS
    Wl,-z,now
    Wl,-z,relro
    Wl,-z,noexecstack
    Wl,--no-undefined
    )

  if(MINGW)
    set(XA_LINKER_ARGS
      ${XA_LINKER_ARGS}
      Wl,--export-all-symbols
      )
  else()
    set(XA_LINKER_ARGS
      ${XA_LINKER_ARGS}
      Wl,--export-dynamic
      )
  endif()
endmacro()

macro(xa_macos_prepare_arm64)
  if(APPLE)
    set(SDK_SUPPORTS_ARM64 False)
    set(SDK_SUPPORTS_X86_64 False)
    execute_process(
      COMMAND xcode-select -p
      RESULT_VARIABLE XCODE_SELECT_RESULT
      OUTPUT_VARIABLE XCODE_DEVELOPER_PATH
      )
    if(NOT ${XCODE_SELECT_RESULT} EQUAL "0")
      message(WARNING "xcode-select failed with result ${XCODE_SELECT_RESULT}")
    else()
      string(STRIP "${XCODE_DEVELOPER_PATH}" XCODE_DEVELOPER_PATH)
      set(SDKSETTINGS_PATH "${XCODE_DEVELOPER_PATH}/Platforms/MacOSX.platform/Developer/SDKs/MacOSX.sdk/SDKSettings.plist")

      # CAUTION: do NOT ever remove the '-o -' parameter, without '-o' plutil will overwrite the .plist file
      execute_process(
		COMMAND plutil -extract SupportedTargets.macosx.Archs json -o - "${SDKSETTINGS_PATH}"
        RESULT_VARIABLE PLUTIL_RESULT
        OUTPUT_VARIABLE SDK_ARCHITECTURES
		)
      if(NOT ${PLUTIL_RESULT} EQUAL 0)
        message(WARNING "plutil failed to read ${SDKSETTINGS_PATH}, returned with result ${PLUTIL_RESULT}")
      else()
        string(FIND "${SDK_ARCHITECTURES}" "\"arm64\"" ARCH_POS)
        if(${ARCH_POS} GREATER_EQUAL 0)
          set(SDK_SUPPORTS_ARM64 True)
        endif()

        string(FIND "${SDK_ARCHITECTURES}" "\"x86_64\"" ARCH_POS)
        if(${ARCH_POS} GREATER_EQUAL 0)
          set(SDK_SUPPORTS_X86_64 True)
        endif()
      endif()
    endif()

    unset(XA_OSX_ARCHITECTURES)
    if(SDK_SUPPORTS_ARM64)
      message(STATUS "SDK at ${XCODE_DEVELOPER_PATH} supports creation of ARM64 binaries")
      list(APPEND XA_OSX_ARCHITECTURES "arm64")
    endif()
    if(SDK_SUPPORTS_X86_64)
      message(STATUS "SDK at ${XCODE_DEVELOPER_PATH} supports creation of X86_64 binaries")
      list(APPEND XA_OSX_ARCHITECTURES "x86_64")
    endif()
  endif()
endmacro()
