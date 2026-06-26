OS           ?= $(shell uname)

V             ?= 0
CONFIGURATION = Debug

ifeq ($(OS),Darwin)
NATIVE_EXT = .dylib
DLLMAP_OS_NAME = osx
endif
ifeq ($(OS),Linux)
NATIVE_EXT = .so
DLLMAP_OS_NAME = linux
endif

PREPARE_EXTERNAL_FILES  = \
	external/xamarin-android-tools/src/Xamarin.Android.Tools.AndroidSdk/Xamarin.Android.Tools.AndroidSdk.csproj

DEPENDENCIES =

NET_SUFFIX = -net10.0
TEST_OUTPUT = bin/Test$(CONFIGURATION)$(NET_SUFFIX)

TESTS =

NET_TESTS = \
	$(TEST_OUTPUT)/Java.Interop-Tests.dll \
	$(TEST_OUTPUT)/Java.Interop.Tools.JavaCallableWrappers-Tests.dll \
	$(TEST_OUTPUT)/Java.Interop.Tools.JavaSource-Tests.dll \
	$(TEST_OUTPUT)/Java.Interop.Tools.Maven-Tests.dll \
	$(TEST_OUTPUT)/Java.Interop.Tools.JavaTypeSystem-Tests.dll \
	$(TEST_OUTPUT)/Java.Interop.Tools.Generator-Tests.dll \
	$(TEST_OUTPUT)/Xamarin.Android.Tools.ApiXmlAdjuster-Tests.dll \
	$(TEST_OUTPUT)/Xamarin.Android.Tools.Bytecode-Tests.dll \
	$(TEST_OUTPUT)/Xamarin.SourceWriter-Tests.dll \
	$(TEST_OUTPUT)/generator-Tests.dll \
	$(TEST_OUTPUT)/logcat-parse-Tests.dll

PTESTS =

all: $(DEPENDENCIES) $(TESTS) $(NET_TESTS)

bin/ilverify:
	-mkdir bin
	dotnet tool install --tool-path bin dotnet-ilverify

run-all-tests:
	r=0; \
	$(MAKE) run-tests                 || r=1 ; \
	$(MAKE) run-net-tests             || r=1 ; \
	$(MAKE) run-ptests                || r=1 ; \
	$(MAKE) run-java-source-utils-tests     || r=1 ; \
	exit $$r;

include build-tools/scripts/msbuild.mk

prepare:: $(BUILD_PROPS)

prepare::
	$(MSBUILD) $(MSBUILD_FLAGS) -target:Prepare
	$(MSBUILD) $(MSBUILD_FLAGS) -target:Restore

clean:
	-$(MSBUILD) $(MSBUILD_FLAGS) /t:Clean
	-rm -Rf bin/$(CONFIGURATION) bin/Build$(CONFIGURATION) bin/Test$(CONFIGURATION)

include build-tools/scripts/mono.mk
-include bin/Build$(CONFIGURATION)/mono.mk
-include bin/Build$(CONFIGURATION)/JdkInfo.mk

NATIVE_TIMING_LIB   = libNativeTiming$(NATIVE_EXT)

bin/Test$(CONFIGURATION)/$(NATIVE_TIMING_LIB): tests/NativeTiming/timing.c $(wildcard $(JI_JDK_INCLUDE_PATHS)/jni.h)
	mkdir -p `dirname "$@"`
	gcc -g -shared -m64 -fPIC -o $@ $< $(JI_JDK_INCLUDE_PATHS:%=-I%)

define TestAssemblyTemplate
$(TEST_OUTPUT)/$(1)-Tests.dll: tests/$(1)-Tests/$(1)-Tests.csproj
	$$(MSBUILD) $$(MSBUILD_FLAGS)
	touch $$@
endef

$(eval $(call TestAssemblyTemplate,Java.Interop))
$(eval $(call TestAssemblyTemplate,Java.Interop.Tools.JavaCallableWrappers))
$(eval $(call TestAssemblyTemplate,Java.Interop.Tools.JavaSource))
$(eval $(call TestAssemblyTemplate,Java.Interop.Tools.Maven))
$(eval $(call TestAssemblyTemplate,Java.Interop.Tools.JavaTypeSystem))
$(eval $(call TestAssemblyTemplate,Java.Interop.Tools.Generator))
$(eval $(call TestAssemblyTemplate,Xamarin.Android.Tools.ApiXmlAdjuster))
$(eval $(call TestAssemblyTemplate,Xamarin.Android.Tools.Bytecode))
$(eval $(call TestAssemblyTemplate,Xamarin.SourceWriter))
$(eval $(call TestAssemblyTemplate,generator))
$(eval $(call TestAssemblyTemplate,logcat-parse))

bin/$(CONFIGURATION)/Java.Interop.dll: $(wildcard src/Java.Interop/*/*.cs) src/Java.Interop/Java.Interop.csproj
	$(MSBUILD) $(if $(V),/v:diag,) /p:Configuration=$(CONFIGURATION) $(if $(SNK),"/p:AssemblyOriginatorKeyFile=$(SNK)",)

CSHARP_REFS = \
	bin/$(CONFIGURATION)/Java.Interop.dll               \
	bin/$(CONFIGURATION)/Java.Interop.Export.dll        \
	$(PTESTS)                                           \
	$(TESTS)

shell:
	MONO_TRACE_LISTENER=Console.Out \
	MONO_OPTIONS=--debug=casts csharp $(patsubst %,-r:%,$(CSHARP_REFS))

# $(call RUN_TEST,filename,log-lref?)
define RUN_TEST
	$(MSBUILD) $(MSBUILD_FLAGS) build-tools/scripts/RunNUnitTests.targets /p:TestAssembly=$(1) || r=1;
endef

run-tests: $(TESTS)
	r=0; \
	$(foreach t,$(TESTS), $(call RUN_TEST,$(t),1)) \
	exit $$r;

run-net-tests: $(NET_TESTS)
	r=0; \
	$(foreach t,$(NET_TESTS), dotnet test $(t) || r=1;) \
	exit $$r;

run-ptests: $(PTESTS)
	r=0; \
	$(foreach t,$(PTESTS), $(call RUN_TEST,$(t))) \
	exit $$r;

run-java-source-utils-tests:
	$(MSBUILD) $(MSBUILD_FLAGS) tools/java-source-utils/java-source-utils.csproj /t:RunTests

bin/Test$(CONFIGURATION)/generator.exe: bin/$(CONFIGURATION)/generator.exe
	cp $<* `dirname "$@"`

update-test-generator-nunit:
	-$(MAKE) run-tests TESTS=bin/Test$(CONFIGURATION)/generator-Tests.dll
	for f in `find tests/generator-Tests/expected -name \*.cs` ; do \
		source=`echo $$f | sed 's#^tests/generator-Tests/expected#bin/Test$(CONFIGURATION)/out#'` ; \
		if [ -f "$$source" ]; then \
			cp -f "$$source" "$$f" ; \
		fi; \
	done
	for source in `find bin/Test$(CONFIGURATION)/out.ji -type f` ; do \
		f=`echo $$source | sed 's#^bin/Test$(CONFIGURATION)/out.ji#tests/generator-Tests/expected.ji#'` ; \
		mkdir -p `dirname $$f`; \
		cp -f "$$source" "$$f" ; \
	done
