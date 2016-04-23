CONFIGURATION = Debug
MSBUILD       = xbuild /p:Configuration=$(CONFIGURATION) $(MSBUILD_ARGS)

all:
	(cd src/Mono.Android && $(MSBUILD) /t:_GenerateBinding)
	$(MSBUILD)

prepare:
	nuget restore
	git submodule update --init --recursive
	(cd external/Java.Interop && nuget restore)

clean:
	$(MSBUILD) /t:Clean
