#
# Mono Path Probing
#
# Inputs:
#
#   $(OS): Optional; **uname**(1) value of the host operating system
#   $(CONFIGURATION): Build configuration name, e.g. Debug or Release
#   $(V): Output verbosity. If != 0, then `MONO_OPTIONS` is exported with --debug.
#
# Outputs:
#
#   bin/Build$(CONFIGURATION)/MonoInfo.props:
#       MSBuild property file which contains:
#       * `$(MonoFrameworkPath)`: `$(JI_MONO_FRAMEWORK_PATH)` value.
#       * `$(MonoLibs)`: `$(JI_MONO_LIBS)` value.
#       * `@(MonoIncludePath)`: `$(JI_MONO_INCLUDE_PATHS)` values.
#   $(JI_MONO_LIB_PATH):
#       Base path to the mono instalation, it can be used to access base class
#       assemblies in $(JI_MONO_LIB_PATH)/mono/<version>/
#   $(JI_MONO_FRAMEWORK_PATH):
#       Path to the `libmonosgen-2.0.1.dylib` file to link against.
#   $(JI_MONO_INCLUDE_PATHS):
#       One or more space separated paths containing Mono headers to pass as
#       -Ipath values to the compiler.
#       It DOES NOT contain the -I itself; use $(JI_MONO_INCLUDE_PATHS:%=-I%) for that.
#   $(JI_MONO_LIBS)
#       C compiler linker arguments to link against `$(JI_MONO_FRAMEWORK_PATH)`.
#   $(RUNTIME):
#       The **mono**(1) program to use to execute managed code.

OS            ?= $(shell uname)
RUNTIME       := $(shell if [ -f "`which mono64`" ] ; then echo mono64 ; else echo mono; fi) --debug=casts

ifneq ($(V),0)
MONO_OPTIONS  += --debug
endif   # $(V) != 0

ifneq ($(MONO_OPTIONS),)
export MONO_OPTIONS
endif   # $(MONO_OPTIONS) != ''
