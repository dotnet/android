CONFIGURATION = Debug
MSBUILD       = xbuild /p:Configuration=$(CONFIGURATION)

all:
	$(MSBUILD)

prepare:
	nuget restore
	git submodule update --init --recursive

clean:
	$(MSBUILD) /t:Clean
