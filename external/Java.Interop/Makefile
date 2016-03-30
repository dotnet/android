CONFIGURATION = Debug

XA_CONFIGURATION  = XAIntegrationDebug

GENDARME_URL = https://cloud.github.com/downloads/spouliot/gendarme/gendarme-2.10-bin.zip

PACKAGES = \
	packages/NUnit.2.6.3/NUnit.2.6.3.nupkg \
	packages/NUnit.Runners.2.6.3/NUnit.Runners.2.6.3.nupkg

DEPENDENCIES = \
	bin/Test$(CONFIGURATION)/libNativeTiming.dylib

XA_INTEGRATION_OUTPUTS = \
	bin/$(XA_CONFIGURATION)/Java.Interop.dll

TESTS = \
	bin/Test$(CONFIGURATION)/Java.Interop-Tests.dll \
	bin/Test$(CONFIGURATION)/Java.Interop.Dynamic-Tests.dll \
	bin/Test$(CONFIGURATION)/Java.Interop.Export-Tests.dll \
	bin/Test$(CONFIGURATION)/LogcatParse-Tests.dll

PTESTS = \
	bin/Test$(CONFIGURATION)/Java.Interop-PerformanceTests.dll

ATESTS = \
	bin/Test$(CONFIGURATION)/Android.Interop-Tests.dll

XBUILD = xbuild $(if $(V),/v:diag,)
NUNIT_CONSOLE = packages/NUnit.Runners.2.6.3/tools/nunit-console.exe

all: $(PACKAGES) $(DEPENDENCIES) $(TESTS) $(XA_INTEGRATION_OUTPUTS)

xa-all: $(XA_INTEGRATION_OUTPUTS)

clean:
	$(XBUILD) /t:Clean
	rm -Rf bin/$(CONFIGURATION)

$(PACKAGES) $(NUNIT_CONSOLE):
	nuget restore

JDK     = JavaDeveloper-2013005_dp__11m4609.pkg
JDK_URL = http://storage.bos.xamarin.com/android-sdk-tool/archives/JavaDeveloper-2013005_dp__11m4609.pkg

APPLE_JDK_URL     = http://adcdownload.apple.com/Developer_Tools/java_for_os_x_2013005_developer_package/java_for_os_x_2013005_dp__11m4609.dmg

LOCAL_JDK_HEADERS = LocalJDK/System/Library/Frameworks/JavaVM.framework/Versions/A/Headers

osx-setup: $(LOCAL_JDK_HEADERS)/jni.h

$(LOCAL_JDK_HEADERS)/jni.h:
	@if [ ! -f $(JDK) ]; then \
		curl -o $(JDK) $(JDK_URL) ; \
	fi
	-mkdir LocalJDK
	_jdk="$$(cd `dirname "$(JDK)"`; pwd)/`basename "$(JDK)"`" ; \
	(cd LocalJDK; xar -xf $$_jdk)
	(cd LocalJDK; gunzip -c JavaEssentialsDev.pkg/Payload | cpio -i)

xa-fxcop: lib/gendarme-2.10/gendarme.exe bin/$(XA_CONFIGURATION)/Java.Interop.dll
	mono --debug $< --html xa-gendarme.html $(if @(GENDARME_XML),--xml xa-gendarme.xml) --ignore xa-gendarme-ignore.txt bin/$(XA_CONFIGURATION)/Java.Interop.dll

lib/gendarme-2.10/gendarme.exe:
	-mkdir -p `dirname "$@"`
	curl -o lib/gendarme-2.10/gendarme-2.10-bin.zip $(GENDARME_URL)
	(cd lib/gendarme-2.10 ; unzip gendarme-2.10-bin.zip)

bin/Test$(CONFIGURATION)/libNativeTiming.dylib: tests/NativeTiming/timing.c $(LOCAL_JDK_HEADERS)/jni.h
	mkdir -p `dirname "$@"`
	gcc -g -shared -o $@ $< -m32 -I $(LOCAL_JDK_HEADERS)

bin/Test$(CONFIGURATION)/libJavaInterop.dylib: JniEnvironment.g.c $(LOCAL_JDK_HEADERS)/jni.h
	mkdir -p `dirname "$@"`
	gcc -g -shared -o $@ $< -m32 -I $(LOCAL_JDK_HEADERS)

bin/Test$(CONFIGURATION)/Java.Interop-Tests.dll: $(wildcard src/Java.Interop/*/*.cs src/Java.Interop/Tests/*/*.cs)
	$(XBUILD)
	touch $@

bin/Test$(CONFIGURATION)/Java.Interop.Dynamic-Tests.dll: $(wildcard src/Java.Interop.Dynamic/*/*.cs src/Java.Interop.Dynamic/Tests/*/*.cs)
	$(XBUILD)
	touch $@

bin/Test$(CONFIGURATION)/Java.Interop.Export-Tests.dll: $(wildcard src/Java.Interop.Export/*/*.cs src/Java.Interop.Export/Tests/*/*.cs)
	$(XBUILD)
	touch $@

bin/Test$(CONFIGURATION)/Java.Interop-PerformanceTests.dll: $(wildcard tests/Java.Interop-PerformanceTests/*.cs) bin/Test$(CONFIGURATION)/libNativeTiming.dylib
	$(XBUILD)
	touch $@

bin/Test$(CONFIGURATION)/Android.Interop-Tests.dll: $(wildcard src/Android.Interop/*/*.cs src/Android.Interop/Tests/*/*.cs)
	$(XBUILD)
	touch $@

bin/$(XA_CONFIGURATION)/Java.Interop.dll: $(wildcard src/Java.Interop/*/*.cs) src/Java.Interop/Java.Interop.csproj
	$(XBUILD) /p:Configuration=$(XA_CONFIGURATION) $(if $(SNK),"/p:AssemblyOriginatorKeyFile=$(SNK)",)

CSHARP_REFS = \
	bin/$(CONFIGURATION)/Java.Interop.dll               \
	bin/$(CONFIGURATION)/Java.Interop.Export.dll        \
	bin/$(CONFIGURATION)/Java.Runtime.Environment.dll   \
	bin/$(CONFIGURATION)/TestJVM.dll                    \
	$(PTESTS)                                           \
	$(TESTS)

shell:
	cd bin/$(CONFIGURATION) && \
	MONO_TRACE_LISTENER=Console.Out \
	MONO_OPTIONS=--debug=casts csharp $(patsubst %,-r:%,$(notdir $(CSHARP_REFS)))

# $(call RUN_TEST,filename,log-lref?)
define RUN_TEST
	MONO_TRACE_LISTENER=Console.Out \
	JAVA_INTEROP_GREF_LOG=g-$(basename $(notdir $(1))).txt $(if $(2),JAVA_INTEROP_LREF_LOG=l-$(basename $(notdir $(1))).txt,) \
	mono --debug=casts $$MONO_OPTIONS --runtime=v4.0.0 \
		$(NUNIT_CONSOLE) $(NUNIT_EXTRA) $(1) \
		$(if $(RUN),-run:$(RUN)) \
		-output=bin/Test$(CONFIGURATION)/TestOutput-$(basename $(notdir $(1))).txt ;
endef

run-tests: $(TESTS) bin/Test$(CONFIGURATION)/libjava-interop.dylib
	$(foreach t,$(TESTS), $(call RUN_TEST,$(t),1))

run-ptests: $(PTESTS) bin/Test$(CONFIGURATION)/libjava-interop.dylib
	$(foreach t,$(PTESTS), $(call RUN_TEST,$(t)))

bin/Test$(CONFIGURATION)/libjava-interop.dylib: bin/$(CONFIGURATION)/libjava-interop.dylib
	cp $< $@

run-android: $(ATESTS)
	(cd src/Android.Interop/Tests; $(XBUILD) '/t:Install;RunTests' $(if $(FIXTURE),/p:TestFixture=$(FIXTURE)))

run-test-jnimarshal: bin/$(CONFIGURATION)/Java.Interop.Export-Tests.dll
	MONO_TRACE_LISTENER=Console.Out \
	mono --debug bin/$(CONFIGURATION)/jnimarshalmethod-gen.exe bin/$(CONFIGURATION)/Java.Interop.Export-Tests.dll
	$(call RUN_TEST,$<)
