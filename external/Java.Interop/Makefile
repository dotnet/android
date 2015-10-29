CONFIGURATION = Debug

XA_CONFIGURATION  = XAIntegrationDebug

DEPENDENCIES = \
	bin/$(CONFIGURATION)/libNativeTiming.dylib

XA_INTEGRATION_OUTPUTS = \
	bin/$(XA_CONFIGURATION)/Java.Interop.dll

TESTS = \
	bin/$(CONFIGURATION)/Java.Interop-Tests.dll \
	bin/$(CONFIGURATION)/Java.Interop.Dynamic-Tests.dll \
	bin/$(CONFIGURATION)/Java.Interop.Export-Tests.dll

PTESTS = \
	bin/$(CONFIGURATION)/Java.Interop-PerformanceTests.dll

ATESTS = \
	bin/$(CONFIGURATION)/Android.Interop-Tests.dll

XBUILD = xbuild

all: $(DEPENDENCIES) $(TESTS) $(XA_INTEGRATION_OUTPUTS)

xa-all: $(XA_INTEGRATION_OUTPUTS)

clean:
	$(XBUILD) /t:Clean
	rm -Rf bin/$(CONFIGURATION)

JDK     = JavaDeveloper.pkg
JDK_URL = http://adcdownload.apple.com/Developer_Tools/java_for_os_x_2013005_developer_package/java_for_os_x_2013005_dp__11m4609.dmg

LOCAL_JDK_HEADERS = LocalJDK/System/Library/Frameworks/JavaVM.framework/Versions/A/Headers

osx-setup: $(LOCAL_JDK_HEADERS)/jni.h

$(LOCAL_JDK_HEADERS)/jni.h:
	@if [ ! -f $(JDK) ]; then \
		echo "Please download '$(JDK)', from: $(JDK_URL)" ; \
		exit 1; \
	fi
	-mkdir LocalJDK
	_jdk="$$(cd `dirname "$(JDK)"`; pwd)/`basename "$(JDK)"`" ; \
	(cd LocalJDK; xar -xf $$_jdk)
	(cd LocalJDK; gunzip -c JavaEssentialsDev.pkg/Payload | cpio -i)

bin/$(CONFIGURATION)/libNativeTiming.dylib: tests/NativeTiming/timing.c $(LOCAL_JDK_HEADERS)/jni.h
	mkdir -p `dirname "$@"`
	gcc -g -shared -o $@ $< -m32 -I $(LOCAL_JDK_HEADERS)

bin/$(CONFIGURATION)/libJavaInterop.dylib: JniEnvironment.g.c $(LOCAL_JDK_HEADERS)/jni.h
	mkdir -p `dirname "$@"`
	gcc -g -shared -o $@ $< -m32 -I $(LOCAL_JDK_HEADERS)

bin/$(CONFIGURATION)/Java.Interop-Tests.dll: $(wildcard src/Java.Interop/*/*.cs src/Java.Interop/Tests/*/*.cs)
	$(XBUILD)
	touch $@

bin/$(CONFIGURATION)/Java.Interop.Export-Tests.dll: $(wildcard src/Java.Interop.Export/*/*.cs src/Java.Interop.Export/Tests/*/*.cs)
	$(XBUILD)
	touch $@

bin/$(CONFIGURATION)/Java.Interop-PerformanceTests.dll: $(wildcard tests/Java.Interop-PerformanceTests/*.cs) bin/$(CONFIGURATION)/libNativeTiming.dylib
	$(XBUILD)
	touch $@

bin/$(CONFIGURATION)/Android.Interop-Tests.dll: $(wildcard src/Android.Interop/*/*.cs src/Android.Interop/Tests/*/*.cs)
	$(XBUILD)
	touch $@

bin/$(XA_CONFIGURATION)/Java.Interop.dll: $(wildcard src/Java.Interop/*/*.cs) src/Java.Interop/Java.Interop.csproj
	$(XBUILD) /p:Configuration=$(XA_CONFIGURATION)

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
	_JI_LOG=gref=g-$(basename $(notdir $(1))).txt,$(if $(2),lref=l-$(basename $(notdir $(1))).txt,) \
	mono --debug=casts $$MONO_OPTIONS --runtime=v4.0.0 \
		lib/NUnit-2.6.3/bin/nunit-console.exe $(NUNIT_EXTRA) $(1) \
		$(if $(RUN),-run:$(RUN)) \
		-output=bin/$(CONFIGURATION)/TestOutput-$(basename $(notdir $(1))).txt ;
endef

run-tests: $(TESTS)
	$(foreach t,$(TESTS), $(call RUN_TEST,$(t),1))

run-ptests: $(PTESTS)
	$(foreach t,$(PTESTS), $(call RUN_TEST,$(t)))

run-android: $(ATESTS)
	(cd src/Android.Interop/Tests; $(XBUILD) '/t:Install;RunTests' $(if $(FIXTURE),/p:TestFixture=$(FIXTURE)))

run-test-jnimarshal: bin/$(CONFIGURATION)/Java.Interop.Export-Tests.dll
	MONO_TRACE_LISTENER=Console.Out \
	mono --debug bin/$(CONFIGURATION)/jnimarshalmethod-gen.exe bin/$(CONFIGURATION)/Java.Interop.Export-Tests.dll
