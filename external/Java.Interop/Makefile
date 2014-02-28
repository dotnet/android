CONFIGURATION = Debug

DEPENDENCIES = \
	bin/$(CONFIGURATION)/libNativeTiming.dylib

TESTS = \
	bin/$(CONFIGURATION)/Java.Interop-Tests.dll \
	bin/$(CONFIGURATION)/Java.Interop.Export-Tests.dll

PTESTS = \
	bin/$(CONFIGURATION)/Java.Interop-PerformanceTests.dll

all: $(DEPENDENCIES) $(TESTS)

clean:
	xbuild /t:Clean
	rm -Rf bin/$(CONFIGURATION)

bin/$(CONFIGURATION)/libNativeTiming.dylib: tests/NativeTiming/timing.c
	mkdir -p `dirname "$@"`
	gcc -g -shared -o $@ $< -m32 -I /System/Library/Frameworks/JavaVM.framework/Headers

bin/$(CONFIGURATION)/Java.Interop-Tests.dll: $(wildcard src/Java.Interop/*/*.cs src/Java.Interop/Tests/*/*.cs)
	xbuild
	touch $@

bin/$(CONFIGURATION)/Java.Interop.Export-Tests.dll: $(wildcard src/Java.Interop.Export/*/*.cs src/Java.Interop.Export/Tests/*/*.cs)
	xbuild
	touch $@

bin/$(CONFIGURATION)/Java.Interop-PerformanceTests.dll: $(wildcard tests/Java.Interop-PerformanceTests/*.cs) bin/$(CONFIGURATION)/libNativeTiming.dylib
	xbuild
	touch $@

# $(call RUN_TEST,filename)
define RUN_TEST
	MONO_TRACE_LISTENER=Console.Out \
	_JI_LOG=gref=g-$(basename $(notdir $(1))).txt,lref=l-$(basename $(notdir $(1))).txt \
	mono --debug=casts $$MONO_OPTIONS --runtime=v4.0.0 \
		lib/NUnit-2.6.3/bin/nunit-console.exe $(1) \
		-output=bin/$(CONFIGURATION)/TestOutput-$(basename $(notdir $(1))).txt ;
endef

run-tests: $(TESTS)
	$(foreach t,$(TESTS), $(call RUN_TEST,$(t)))

run-ptests: $(PTESTS)
	$(foreach t,$(PTESTS), $(call RUN_TEST,$(t)))
