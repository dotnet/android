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
DOTNET_ROOT   = $(topdir)/bin/$(CONFIGURATION)/dotnet/
DOTNET_TOOL   = $(DOTNET_ROOT)dotnet
DOTNET_VERB   = build
MSBUILD_FLAGS = /p:Configuration=$(CONFIGURATION) $(MSBUILD_ARGS)

ifeq ($(OS_NAME),Darwin)
_PKG_CONFIG   = /Library/Frameworks/Mono.framework/Commands/pkg-config
else    # $(OS_NAME) != Darwin
_PKG_CONFIG   = pkg-config
endif   # $(OS_NAME) == Darwin

ifeq ($(MSBUILD),msbuild)
export USE_MSBUILD  = 1
endif   # $(MSBUILD) == msbuild

ifneq ($(V),0)
MSBUILD_FLAGS += /v:diag
endif   # $(V) != 0

ifeq ($(USE_MSBUILD),1)

# $(call MSBUILD_BINLOG,name,msbuild=$(MSBUILD),outdir=Build)
define MSBUILD_BINLOG
	$(if $(2),$(2),$(MSBUILD)) $(MSBUILD_FLAGS) \
		/binaryLogger:"$(dir $(realpath $(firstword $(MAKEFILE_LIST))))/bin/$(if $(3),$(3),Build)$(CONFIGURATION)/msbuild-`date +%Y%m%dT%H%M%S`-$(1).binlog"
endef

# $(call DOTNET_BINLOG,name,build=$(DOTNET_VERB),dotnet=$(DOTNET_TOOL))
define DOTNET_BINLOG
	$(if $(3),,PATH="$(DOTNET_ROOT):$(PATH)") $(if $(3),$(3),$(DOTNET_TOOL)) $(if $(2),$(2),$(DOTNET_VERB)) -c $(CONFIGURATION) -v:n $(MSBUILD_ARGS) \
		-bl:"$(dir $(realpath $(firstword $(MAKEFILE_LIST))))/bin/Build$(CONFIGURATION)/msbuild-`date +%Y%m%dT%H%M%S`-$(1).binlog"
endef

# $(call SYSTEM_DOTNET_BINLOG,name,build=$(DOTNET_VERB))
define SYSTEM_DOTNET_BINLOG
	$(call DOTNET_BINLOG,$(1),$(2),dotnet)
endef

else    # $(MSBUILD) != 1
_CSC_EMITS_PDB  := $(shell if $(_PKG_CONFIG) --atleast-version=4.9 mono ; then echo Pdb; fi )
ifeq ($(_CSC_EMITS_PDB),Pdb)
MSBUILD_FLAGS += /p:_DebugFileExt=.pdb
else    # $(_CSC_EMITS_PDB) == ''
MSBUILD_FLAGS += /p:_DebugFileExt=.mdb
endif   # $(_CSC_EMITS_PDB) == Pdb

# $(call MSBUILD_BINLOG,name,msbuild=$(MSBUILD))
define MSBUILD_BINLOG
	$(if $(2),$(2),$(MSBUILD)) $(MSBUILD_FLAGS)
endef

endif   # $(USE_MSBUILD) == 1
