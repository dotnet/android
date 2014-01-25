CONFIGURATION = Debug
TESTS = \
	bin/$(CONFIGURATION)/Java.Interop-Tests.dll \
	bin/$(CONFIGURATION)/Java.Interop.Export-Tests.dll

all: $(TESTS)

bin/$(CONFIGURATION)/Java.Interop-Tests.dll: $(wildcard src/Java.Interop/*/*.cs src/Java.Interop/Tests/*/*.cs)
	xbuild

bin/$(CONFIGURATION)/Java.Interop.Export-Tests.dll: $(wildcard src/Java.Interop.Export/*/*.cs src/Java.Interop.Export/Tests/*/*.cs)
	xbuild

# $(call RUN_TEST,filename)
define RUN_TEST
	MONO_TRACE_LISTENER=Console.Out \
	_JI_LOG=gref=g-$(basename $(notdir $(1))).txt \
	mono --debug $$MONO_OPTIONS --runtime=v4.0.0 \
		lib/NUnit-2.6.3/bin/nunit-console.exe $(1) \
		-output=bin/$(CONFIGURATION)/TestOutput-$(basename $(notdir $(1))).txt ;
endef

run-tests: $(TESTS)
	$(foreach t,$(TESTS), $(call RUN_TEST,$(t)))
