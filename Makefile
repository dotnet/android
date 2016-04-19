CONFIGURATION = Debug
MSBUILD       = xbuild /p:Configuration=$(CONFIGURATION)

all:
	$(MSBUILD)

clean:
	$(MSBUILD) /t:Clean
