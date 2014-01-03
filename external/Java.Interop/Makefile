CONFIGURATION = Debug
TESTS = bin/$(CONFIGURATION)/Java.Interop-Tests.dll

all:
	
$(TESTS):
	xbuild

run-tests: $(TESTS)
	nunit-console4 $(TESTS)
