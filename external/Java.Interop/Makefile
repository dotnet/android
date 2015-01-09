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
		lib/NUnit-2.6.3/bin/nunit-console.exe $(1) \
		$(if $(RUN),-run:$(RUN)) \
		-output=bin/$(CONFIGURATION)/TestOutput-$(basename $(notdir $(1))).txt ;
endef

run-tests: $(TESTS)
	$(foreach t,$(TESTS), $(call RUN_TEST,$(t),1))

run-ptests: $(PTESTS)
	$(foreach t,$(PTESTS), $(call RUN_TEST,$(t)))
