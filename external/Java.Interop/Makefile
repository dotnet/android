CONFIGURATION = Debug
TESTS = bin/$(CONFIGURATION)/Java.Interop-Tests.dll

all: $(TESTS)
	
$(TESTS):
	xbuild

run-tests: $(TESTS)
	MONO_TRACE_LISTENER=Console.Out \
	_JI_LOG=gref=g.txt \
	nunit-console4 $(TESTS) -output=TestOutput.txt
