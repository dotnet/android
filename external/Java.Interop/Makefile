CONFIGURATION = Debug
TESTS = bin/$(CONFIGURATION)/Java.Interop-Tests.dll

all: $(TESTS)

bin/$(CONFIGURATION)/Java.Interop-Tests.dll: $(wildcard src/Java.Interop/*/*.cs src/Java.Interop/Tests/*/*.cs)
	xbuild

run-tests: $(TESTS)
	MONO_TRACE_LISTENER=Console.Out \
	_JI_LOG=gref=g.txt \
	mono --debug $$MONO_OPTIONS --runtime=v4.0.0 \
		lib/NUnit-2.6.3/bin/nunit-console.exe $(TESTS) \
		-output=TestOutput.txt
