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
#   $(OS_NAME): Operating system; used to determine `pkg-config` location
#   $(V): Build verbosity
#
# Outputs:
#
#   $(MSBUILD): The MSBuild program to use. Defaults to `xbuild` unless overridden.
#   $(MSBUILD_FLAGS): Additional MSBuild flags; contains $(CONFIGURATION), $(V), $(MSBUILD_ARGS).

MSBUILD       = msbuild
MSBUILD_FLAGS = /p:Configuration=$(CONFIGURATION) $(MSBUILD_ARGS)

ifeq ($(OS_NAME),Darwin)
_PKG_CONFIG   = /Library/Frameworks/Mono.framework/Commands/pkg-config
else    # $(OS_NAME) != Darwin
_PKG_CONFIG   = pkg-config
endif   # $(OS_NAME) == Darwin

ifeq ($(MSBUILD),msbuild)
export USE_MSBUILD  = 1
endif   # $(MSBUILD) == msbuild

ifeq ($(USE_MSBUILD),1)

# $(call MSBUILD_BINLOG,name,msbuild=$(MSBUILD))
define MSBUILD_BINLOG
	$(if $(2),$(2),$(MSBUILD)) $(MSBUILD_FLAGS) /v:normal \
		/binaryLogger:"$(dir $(realpath $(firstword $(MAKEFILE_LIST))))/bin/Build$(CONFIGURATION)/msbuild-`date +%Y%m%dT%H%M%S`-$(1).binlog"
endef

else    # $(MSBUILD) != 1
_CSC_EMITS_PDB  := $(shell if $(_PKG_CONFIG) --atleast-version=4.9 mono ; then echo Pdb; fi )
ifeq ($(_CSC_EMITS_PDB),Pdb)
MSBUILD_FLAGS += /p:_DebugFileExt=.pdb
else    # $(_CSC_EMITS_PDB) == ''
MSBUILD_FLAGS += /p:_DebugFileExt=.mdb
endif   # $(_CSC_EMITS_PDB) == Pdb

ifneq ($(V),0)
MSBUILD_FLAGS += /v:diag
endif   # $(V) != 0

# $(call MSBUILD_BINLOG,name,msbuild=$(MSBUILD))
define MSBUILD_BINLOG
	$(if $(2),$(2),$(MSBUILD)) $(MSBUILD_FLAGS)
endef

endif   # $(USE_MSBUILD) == 1
