CONFIGURATION = Debug
MSBUILD       = xbuild /p:Configuration=$(CONFIGURATION) $(MSBUILD_ARGS)

all:
	$(MSBUILD)

prepare:
	nuget restore
	git submodule update --init --recursive

clean:
	$(MSBUILD) /t:Clean
