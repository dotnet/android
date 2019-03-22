CONFIGURATION   := Debug
NUNIT_CONSOLE   := packages/NUnit.ConsoleRunner.3.9.0/tools/nunit3-console.exe
OS              := $(shell uname)
RUNTIME         := mono --debug=casts
V               ?= 0

include build-tools/scripts/msbuild.mk

all:
	$(MSBUILD) $(MSBUILD_FLAGS) Xamarin.Android.Tools.sln

clean:
	-$(MSBUILD) $(MSBUILD_FLAGS) /t:Clean Xamarin.Android.Tools.sln

prepare:
	nuget restore Xamarin.Android.Tools.sln

run-all-tests: run-nunit-tests

# $(call RUN_NUNIT_TEST,filename,log-lref?)
define RUN_NUNIT_TEST
	MONO_TRACE_LISTENER=Console.Out \
	$(RUNTIME) \
		$(NUNIT_CONSOLE) $(NUNIT_EXTRA) $(1) \
		$(if $(RUN),-run:$(RUN)) \
		--result="TestResult-$(basename $(notdir $(1))).xml;format=nunit2" \
		-output=bin/Test$(CONFIGURATION)/TestOutput-$(basename $(notdir $(1))).txt \
	|| true ; \
	if [ -f "bin/Test$(CONFIGURATION)/TestOutput-$(basename $(notdir $(1))).txt" ] ; then \
		cat bin/Test$(CONFIGURATION)/TestOutput-$(basename $(notdir $(1))).txt ; \
	fi
endef

$(NUNIT_CONSOLE): prepare

NUNIT_TESTS = \
	bin/Test$(CONFIGURATION)/Xamarin.Android.Tools.AndroidSdk-Tests.dll

run-nunit-tests: $(NUNIT_TESTS)
	$(foreach t,$(NUNIT_TESTS), $(call RUN_NUNIT_TEST,$(t),1))
