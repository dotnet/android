CONFIGURATION = Debug
MSBUILD       = xbuild /p:Configuration=$(CONFIGURATION) $(MSBUILD_ARGS)

all:
	$(MSBUILD)

prepare:
	nuget restore
	git submodule update --init --recursive
	(cd external/Java.Interop && nuget restore)

clean:
	$(MSBUILD) /t:Clean
