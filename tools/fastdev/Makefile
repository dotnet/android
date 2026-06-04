include ../../env.config
include $(TOP)/mk/build.make

VERBOSITY=diag
SLN=fastdevtools.csproj
MSBUILD_ARGS=/verbosity:$(VERBOSITY) /nologo /restore
MSBUILD=msbuild $(MSBUILD_ARGS)
srcdir=$(shell pwd)

OUTPUT_FIND = $(ENABLED_RUNTIMES:%=$(BUILDDIR)/lib/xamarin.android/xbuild/Xamarin/Android/lib/%/xamarin.find)
OUTPUT_STAT = $(ENABLED_RUNTIMES:%=$(BUILDDIR)/lib/xamarin.android/xbuild/Xamarin/Android/lib/%/xamarin.stat)
OUTPUT_SYNC = $(ENABLED_RUNTIMES:%=$(BUILDDIR)/lib/xamarin.android/xbuild/Xamarin/Android/lib/%/xamarin.sync)

OUTPUT = $(OUTPUT_FIND) $(OUTPUT_STAT) $(OUTPUT_SYNC)
SRC = $(SLN) xamarin.find/CMakeLists.txt xamarin.find/main.cc xamarin.stat/CMakeLists.txt xamarin.stat/main.cc xamarin.sync/CMakeLists.txt xamarin.sync/main.cc

all $(OUTPUT):: $(SRC)
	-$(MSBUILD) $(SLN) /property:Configuration=$(CONFIGURATION)

clean::
	-$(MSBUILD) $(SLN) /property:Configuration=$(CONFIGURATION) /t:Clean