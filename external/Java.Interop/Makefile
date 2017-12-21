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

XA_CONFIGURATION  = XAIntegrationDebug

GENDARME_URL = https://github.com/downloads/spouliot/gendarme/gendarme-2.10-bin.zip

PACKAGES = \
	packages/NUnit.2.6.3/NUnit.2.6.3.nupkg \
	packages/NUnit.Runners.2.6.3/NUnit.Runners.2.6.3.nupkg

DEPENDENCIES = \
	bin/Test$(CONFIGURATION)/libNativeTiming$(NATIVE_EXT)

XA_INTEGRATION_OUTPUTS = \
	bin/$(XA_CONFIGURATION)/Java.Interop.dll

TESTS = \
	bin/Test$(CONFIGURATION)/Java.Interop-Tests.dll \
	bin/Test$(CONFIGURATION)/Java.Interop.Dynamic-Tests.dll \
	bin/Test$(CONFIGURATION)/Java.Interop.Export-Tests.dll \
	bin/Test$(CONFIGURATION)/Java.Interop.Tools.JavaCallableWrappers-Tests.dll \
	bin/Test$(CONFIGURATION)/LogcatParse-Tests.dll \
	bin/Test$(CONFIGURATION)/generator-Tests.dll \
	bin/Test$(CONFIGURATION)/Xamarin.Android.Tools.ApiXmlAdjuster-Tests.dll \
	bin/Test$(CONFIGURATION)/Xamarin.Android.Tools.Bytecode-Tests.dll

PTESTS = \
	bin/Test$(CONFIGURATION)/Java.Interop-PerformanceTests.dll

ATESTS = \
	bin/Test$(CONFIGURATION)/Android.Interop-Tests.dll

NUNIT_CONSOLE = packages/NUnit.Runners.2.6.3/tools/nunit-console.exe

BUILD_PROPS = bin/Build$(CONFIGURATION)/JdkInfo.props bin/Build$(CONFIGURATION)/MonoInfo.props

all: $(BUILD_PROPS)  src/Java.Runtime.Environment/Java.Runtime.Environment.dll.config \
		$(PACKAGES) $(DEPENDENCIES) $(TESTS) $(XA_INTEGRATION_OUTPUTS)

xa-all: $(PACKAGES) $(XA_INTEGRATION_OUTPUTS)

run-all-tests: run-tests run-test-jnimarshal run-test-generator-core run-ptests

prepare:: prepare-external

prepare-external: $(PACKAGES) $(NUNIT_CONSOLE)
	git submodule update --init --recursive

clean:
	-$(MSBUILD) $(MSBUILD_FLAGS) /t:Clean
	-rm -Rf bin/$(CONFIGURATION) bin/Build$(CONFIGURATION) bin/Test$(CONFIGURATION) bin/XAIntegration$(CONFIGURATION)
	-rm src/Java.Runtime.Environment/Java.Runtime.Environment.dll.config

include build-tools/scripts/mono.mk
include build-tools/scripts/jdk.mk
include build-tools/scripts/msbuild.mk

$(PACKAGES) $(NUNIT_CONSOLE):
	nuget restore

src/Java.Runtime.Environment/Java.Runtime.Environment.dll.config: src/Java.Runtime.Environment/Java.Runtime.Environment.dll.config.in \
		bin/Build$(CONFIGURATION)/JdkInfo.props
	sed -e 's#@JI_JVM_PATH@#$(JI_JVM_PATH)#g' -e 's#@OS_NAME@#$(DLLMAP_OS_NAME)#g' < $< > $@

xa-fxcop: lib/gendarme-2.10/gendarme.exe bin/$(XA_CONFIGURATION)/Java.Interop.dll
	$(RUNTIME) $< --html xa-gendarme.html $(if @(GENDARME_XML),--xml xa-gendarme.xml) --ignore gendarme-ignore.txt bin/$(XA_CONFIGURATION)/Java.Interop.dll

fxcop: lib/gendarme-2.10/gendarme.exe bin/GendarmeDebug/Java.Interop.dll
	cp src/Java.Interop/obj/Gendarme/Java.Interop.dll.mdb bin/GendarmeDebug/
	$(RUNTIME) $< --html gendarme.html $(if @(GENDARME_XML),--xml gendarme.xml) --ignore gendarme-ignore.txt bin/GendarmeDebug/Java.Interop.dll

lib/gendarme-2.10/gendarme.exe:
	-mkdir -p `dirname "$@"`
	curl -L -o lib/gendarme-2.10/gendarme-2.10-bin.zip $(GENDARME_URL)
	(cd lib/gendarme-2.10 ; unzip gendarme-2.10-bin.zip)

JAVA_INTEROP_LIB    = libjava-interop$(NATIVE_EXT)
NATIVE_TIMING_LIB   = libNativeTiming$(NATIVE_EXT)

bin/Test$(CONFIGURATION)/$(NATIVE_TIMING_LIB): tests/NativeTiming/timing.c $(wildcard $(JI_JDK_INCLUDE_PATHS)/jni.h)
	mkdir -p `dirname "$@"`
ifeq ($(OS),Darwin)
	gcc -g -shared -m32 -o bin/Test$(CONFIGURATION)/libNativeTiming-m32.dylib $< $(JI_JDK_INCLUDE_PATHS:%=-I%)
	gcc -g -shared -m64 -o bin/Test$(CONFIGURATION)/libNativeTiming-m64.dylib $< $(JI_JDK_INCLUDE_PATHS:%=-I%)
	lipo bin/Test$(CONFIGURATION)/libNativeTiming-m32.dylib bin/Test$(CONFIGURATION)/libNativeTiming-m64.dylib -create \
		-output "$@"
endif
ifeq ($(OS),Linux)
	gcc -g -shared -o $@ $< -m32 $(JI_JDK_INCLUDE_PATHS:%=-I%)
endif

# Usage: $(call TestAssemblyTemplate,assembly-basename)
define TestAssemblyTemplate
bin/Test$$(CONFIGURATION)/$(1)-Tests.dll: $(wildcard src/$(1)/*/*.cs src/$(1)/Test*/*/*.cs)
	$$(MSBUILD) $$(MSBUILD_FLAGS)
	touch $$@
endef # TestAssemblyTemplate

$(eval $(call TestAssemblyTemplate,Java.Interop))
$(eval $(call TestAssemblyTemplate,Java.Interop.Dynamic))
$(eval $(call TestAssemblyTemplate,Java.Interop.Export))
$(eval $(call TestAssemblyTemplate,Java.Interop.Tools.JavaCallableWrappers))

bin/Test$(CONFIGURATION)/Java.Interop-PerformanceTests.dll: $(wildcard tests/Java.Interop-PerformanceTests/*.cs) bin/Test$(CONFIGURATION)/$(NATIVE_TIMING_LIB)
	$(MSBUILD) $(MSBUILD_FLAGS)
	touch $@

bin/Test$(CONFIGURATION)/Android.Interop-Tests.dll: $(wildcard src/Android.Interop/*/*.cs src/Android.Interop/Tests/*/*.cs)
	$(MSBUILD) $(MSBUILD_FLAGS)
	touch $@

bin/$(XA_CONFIGURATION)/Java.Interop.dll: $(wildcard src/Java.Interop/*/*.cs) src/Java.Interop/Java.Interop.csproj
	$(MSBUILD) $(if $(V),/v:diag,) /p:Configuration=$(XA_CONFIGURATION) $(if $(SNK),"/p:AssemblyOriginatorKeyFile=$(SNK)",)

bin/GendarmeDebug/Java.Interop.dll: $(wildcard src/Java.Interop/*/*.cs) src/Java.Interop/Java.Interop.csproj
	$(MSBUILD) $(if $(V),/v:diag,) /p:Configuration="Gendarme" $(if $(SNK),"/p:AssemblyOriginatorKeyFile=$(SNK)",) /p:CscToolExe=`which mcs` src/Java.Interop/Java.Interop.csproj

CSHARP_REFS = \
	bin/$(CONFIGURATION)/Java.Interop.dll               \
	bin/$(CONFIGURATION)/Java.Interop.Export.dll        \
	bin/$(CONFIGURATION)/Java.Runtime.Environment.dll   \
	bin/Test$(CONFIGURATION)/TestJVM.dll                    \
	$(PTESTS)                                           \
	$(TESTS)

shell:
	MONO_TRACE_LISTENER=Console.Out \
	MONO_OPTIONS=--debug=casts csharp $(patsubst %,-r:%,$(CSHARP_REFS))

# $(call RUN_TEST,filename,log-lref?)
define RUN_TEST
	$(MSBUILD) $(MSBUILD_FLAGS) build-tools/scripts/RunNUnitTests.targets /p:TestAssembly=$(1) ;
endef

run-tests: $(TESTS) bin/Test$(CONFIGURATION)/$(JAVA_INTEROP_LIB)
	$(foreach t,$(TESTS), $(call RUN_TEST,$(t),1))

run-ptests: $(PTESTS) bin/Test$(CONFIGURATION)/$(JAVA_INTEROP_LIB)
	$(foreach t,$(PTESTS), $(call RUN_TEST,$(t)))

bin/Test$(CONFIGURATION)/$(JAVA_INTEROP_LIB): bin/$(CONFIGURATION)/$(JAVA_INTEROP_LIB)
	cp $< $@

run-android: $(ATESTS)
	(cd src/Android.Interop/Tests; $(MSBUILD) $(MSBUILD_FLAGS) '/t:Install;RunTests' $(if $(FIXTURE),/p:TestFixture=$(FIXTURE)))

run-test-jnimarshal: bin/Test$(CONFIGURATION)/Java.Interop.Export-Tests.dll bin/Test$(CONFIGURATION)/$(JAVA_INTEROP_LIB)
	MONO_TRACE_LISTENER=Console.Out \
	$(RUNTIME) bin/$(CONFIGURATION)/jnimarshalmethod-gen.exe "$<"
	$(call RUN_TEST,$<)


GENERATOR_EXPECTED_TARGETS  = tools/generator/Tests/expected.targets

# $(call GEN_CORE_OUTPUT, outdir, suffix, extra)
define GEN_CORE_OUTPUT
	-$(RM) -Rf $(1)
	mkdir -p $(1)
	$(RUNTIME) bin/Test$(CONFIGURATION)/generator.exe -o $(1) $(3) --api-level=20 tools/generator/Tests-Core/api$(2).xml \
		--enummethods=tools/generator/Tests-Core/methods$(2).xml \
		--enumfields=tools/generator/Tests-Core/fields$(2).xml \
		--enumdir=$(1)
endef

run-test-generator-core: bin/Test$(CONFIGURATION)/generator.exe
	$(call GEN_CORE_OUTPUT,bin/Test$(CONFIGURATION)/generator-core)
	diff -rup tools/generator/Tests-Core/expected bin/Test$(CONFIGURATION)/generator-core
	$(call GEN_CORE_OUTPUT,bin/Test$(CONFIGURATION)/generator-core,,--codegen-target=JavaInterop1)
	diff -rup tools/generator/Tests-Core/expected.ji bin/Test$(CONFIGURATION)/generator-core
	$(call GEN_CORE_OUTPUT,bin/Test$(CONFIGURATION)/generator-core,-cp)
	diff -rup tools/generator/Tests-Core/expected.cp bin/Test$(CONFIGURATION)/generator-core

bin/Test$(CONFIGURATION)/generator.exe: bin/$(CONFIGURATION)/generator.exe
	cp $<* `dirname "$@"`

update-test-generator-core:
	$(call GEN_CORE_OUTPUT,tools/generator/Tests-Core/expected)
	$(call GEN_CORE_OUTPUT,tools/generator/Tests-Core/expected.ji,--codegen-target=JavaInterop1)

update-test-generator-nunit:
	-$(MAKE) run-tests TESTS=bin/Test$(CONFIGURATION)/generator-Tests.dll
	for f in `find tools/generator/Tests/expected -name \*.cs` ; do \
		source=`echo $$f | sed 's#^tools/generator/Tests/expected#bin/Test$(CONFIGURATION)/out#'` ; \
		if [ -f "$$source" ]; then \
			cp -f "$$source" "$$f" ; \
		fi; \
	done
	for source in `find bin/Test$(CONFIGURATION)/out.ji -type f` ; do \
		f=`echo $$source | sed 's#^bin/Test$(CONFIGURATION)/out.ji#tools/generator/Tests/expected.ji#'` ; \
		mkdir -p `dirname $$f`; \
		cp -f "$$source" "$$f" ; \
	done
	echo '<Project DefaultTargets="Build" ToolsVersion="4.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">' > $(GENERATOR_EXPECTED_TARGETS)
	echo '  <ItemGroup>' >> $(GENERATOR_EXPECTED_TARGETS)
	for f in `find tools/generator/Tests/expected* -type f | sort -i` ; do \
		include=`echo $$f | sed 's#^tools/generator/Tests/##' | tr / \\\\` ; \
		echo "    <Content Include='$$include'>" >> $(GENERATOR_EXPECTED_TARGETS); \
		echo "      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>" >> $(GENERATOR_EXPECTED_TARGETS); \
		echo "    </Content>" >> $(GENERATOR_EXPECTED_TARGETS); \
	done
	echo '  </ItemGroup>' >> $(GENERATOR_EXPECTED_TARGETS)
	echo '</Project>' >> $(GENERATOR_EXPECTED_TARGETS)
