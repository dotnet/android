#
# MSBuild Abstraction.
#
# Makefile targets which need to invoke MSBuild should use `$(MSBUILD)`,
# not some specific MSBuild program such as `xbuild` or `msbuild`.
#
# Typical use will also include `$(MSBUILD_FLAGS)`, which provides the
# Configuration and logging verbosity, as per $(CONFIGURATION) and $(V):
#
#   $(MSBUILD) $(MSBUILD_FLAGS) path/to/Project.csproj
#
# Inputs:
#
#   $(CONFIGURATION): Build configuration name, e.g. Debug or Release
#   $(MSBUILD): The MSBuild program to use.
#   $(MSBUILD_ARGS): Extra arguments to pass to $(MSBUILD); embedded into $(MSBUILD_FLAGS)
#   $(V): Build verbosity
#
# Outputs:
#
#   $(MSBUILD): The MSBuild program to use. Defaults to `xbuild` unless overridden.
#   $(MSBUILD_FLAGS): Additional MSBuild flags; contains $(CONFIGURATION), $(V), $(MSBUILD_ARGS).

MSBUILD       = xbuild
MSBUILD_FLAGS = /p:Configuration=$(CONFIGURATION) $(MSBUILD_ARGS)

ifneq ($(V),0)
MSBUILD_FLAGS += /v:diag
endif   # $(V) != 0

ifeq ($(MSBUILD),msbuild)
USE_MSBUILD   = 1
endif   # $(MSBUILD) == msbuild

ifeq ($(USE_MSBUILD),1)
_BROKEN_MSBUILD := $(shell if ! pkg-config --atleast-version=4.8 mono ; then echo Broken; fi )
ifeq ($(_BROKEN_MSBUILD),Broken)
MSBUILD_FLAGS += /p:_XAFixMSBuildFrameworkPathOverride=True
endif   # $(_BROKEN_MSBUILD) == Broken
endif   # $(USE_MSBUILD) == 1
