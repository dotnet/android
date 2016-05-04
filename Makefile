V             ?= 0
CONFIGURATION = Debug
MSBUILD       = xbuild /p:Configuration=$(CONFIGURATION) $(MSBUILD_ARGS)

ifneq ($(V),0)
MONO_OPTIONS += --debug
MSBUILD      += /v:d
endif

ifneq ($(MONO_OPTIONS),)
export MONO_OPTIONS
endif

all:
	$(MSBUILD)

prepare:
	git submodule update --init --recursive
	nuget restore
	(cd external/Java.Interop && nuget restore)

clean:
	$(MSBUILD) /t:Clean
