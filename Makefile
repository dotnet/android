V             ?= 0
prefix         = /usr/local
CONFIGURATION  = Debug
RUNTIME       := $(shell which mono64 2> /dev/null && echo mono64 || echo mono) --debug=casts
SOLUTION       = Xamarin.Android.sln
TEST_TARGETS   = build-tools/scripts/RunTests.targets
API_LEVEL     ?=
PREPARE_ARGS =
PREPARE_BUILD_LOG = bin/Build$(CONFIGURATION)/bootstrap-build.binlog
PREPARE_RESTORE_LOG = bin/Build$(CONFIGURATION)/bootstrap-restore.binlog
PREPARE_SOURCE_DIR = build-tools/xaprepare
PREPARE_SOLUTION = $(PREPARE_SOURCE_DIR)/xaprepare.sln
PREPARE_EXE = $(PREPARE_SOURCE_DIR)/xaprepare/bin/$(CONFIGURATION)/xaprepare.exe
PREPARE_COMMON_MSBUILD_FLAGS = /p:Configuration=$(CONFIGURATION) $(PREPARE_MSBUILD_ARGS) $(MSBUILD_ARGS)
PREPARE_MSBUILD_FLAGS = /binaryLogger:"$(PREPARE_BUILD_LOG)" $(PREPARE_COMMON_MSBUILD_FLAGS)
PREPARE_RESTORE_FLAGS = /binaryLogger:"$(PREPARE_RESTORE_LOG)" $(PREPARE_COMMON_MSBUILD_FLAGS)
PREPARE_SCENARIO =
PREPARE_CI ?= 0
PREPARE_AUTOPROVISION ?= 0
PREPARE_IGNORE_MONO_VERSION ?= 1

_PREPARE_CI_MODE_ARGS = --no-emoji --run-mode=CI -a
_PREPARE_ARGS =

all:
	$(call MSBUILD_BINLOG,all,$(_SLN_BUILD)) $(MSBUILD_FLAGS) $(SOLUTION)

-include bin/Build$(CONFIGURATION)/rules.mk

ifeq ($(OS_NAME),)
export OS_NAME       := $(shell uname)
endif

ifeq ($(OS_ARCH),)
export OS_ARCH       := $(shell uname -m)
endif

export NO_SUDO       ?= false

ifneq ($(NO_SUDO),false)
_PREPARE_ARGS += --auto-provisioning-uses-sudo=false
endif

ifneq ($(V),0)
MONO_OPTIONS   += --debug
NUGET_VERBOSITY = -Verbosity Detailed
_PREPARE_ARGS += -v:d
endif

ifneq ($(PREPARE_CI),0)
_PREPARE_ARGS += $(_PREPARE_CI_MODE_ARGS)
endif

ifneq ($(PREPARE_AUTOPROVISION),0)
_PREPARE_ARGS += --auto-provision=yes --auto-provision-uses-sudo=yes
endif

ifeq ($(OS_NAME),Darwin)
ifeq ($(HOMEBREW_PREFIX),)
HOMEBREW_PREFIX ?= $(shell brew --prefix)
endif
else
HOMEBREW_PREFIX := $prefix
endif

ifeq ($(wildcard Configuration.OperatingSystem.props),)
PREPARE_MSBUILD_FLAGS += "/p:HostHomebrewPrefix=$(HOMEBREW_PREFIX)"
endif

ifneq ($(PREPARE_SCENARIO),)
_PREPARE_ARGS += -s:"$(PREPARE_SCENARIO)"
endif

ifeq ($(XA_FORCE_COMPONENT_REFRESH),true)
_PREPARE_ARGS += -refresh
endif

_PREPARE_ARGS += $(PREPARE_ARGS)

include build-tools/scripts/msbuild.mk

ifeq ($(USE_MSBUILD),1)
_SLN_BUILD  = $(MSBUILD)
else    # $(MSBUILD) != 1
_SLN_BUILD  = MSBUILD="$(MSBUILD)" tools/scripts/xabuild
endif   # $(USE_MSBUILD) == 1

ifneq ($(API_LEVEL),)
MSBUILD_FLAGS += /p:AndroidApiLevel=$(API_LEVEL) /p:AndroidFrameworkVersion=$(word $(API_LEVEL), $(ALL_FRAMEWORKS)) /p:AndroidPlatformId=$(word $(a), $(ALL_PLATFORM_IDS))
endif

all-tests::
	MSBUILD="$(MSBUILD)" $(call MSBUILD_BINLOG,all-tests,tools/scripts/xabuild) $(MSBUILD_FLAGS) Xamarin.Android-Tests.sln

install::
	@if [ ! -d "bin/$(CONFIGURATION)" ]; then \
		echo "run 'make all' before you execute 'make install'!"; \
		exit 1; \
	fi
	-mkdir -p "$(prefix)/bin"
	-mkdir -p "$(prefix)/lib/mono/xbuild-frameworks"
	-mkdir -p "$(prefix)/lib/xamarin.android"
	-mkdir -p "$(prefix)/lib/mono/xbuild/Xamarin/"
	cp -a "bin/$(CONFIGURATION)/lib/xamarin.android/." "$(prefix)/lib/xamarin.android/"
	-rm -rf "$(prefix)/lib/mono/xbuild/Novell"
	-rm -rf "$(prefix)/lib/mono/xbuild/Xamarin/Xamarin.Android.Sdk.props"
	-rm -rf "$(prefix)/lib/mono/xbuild/Xamarin/Xamarin.Android.Sdk.targets"
	-rm -rf "$(prefix)/lib/mono/xbuild/Xamarin/Android"
	-rm -rf "$(prefix)/lib/mono/xbuild-frameworks/MonoAndroid"
	ln -s "$(prefix)/lib/xamarin.android/xbuild/Xamarin/Android/" "$(prefix)/lib/mono/xbuild/Xamarin/Android"
	ln -s "$(prefix)/lib/xamarin.android/xbuild/Novell/" "$(prefix)/lib/mono/xbuild/Novell"
	ln -s "$(prefix)/lib/xamarin.android/xbuild-frameworks/MonoAndroid/" "$(prefix)/lib/mono/xbuild-frameworks/MonoAndroid"
	if [ ! -e "$(prefix)/bin/mono" ]; then \
		cp tools/scripts/xabuild "$(prefix)/bin/xabuild"; \
	fi

uninstall::
	rm -rf "$(prefix)/lib/xamarin.android/" "$(prefix)/bin/xabuild"
	rm -rf "$(prefix)/lib/mono/xbuild/Xamarin/Android"
	rm -rf "$(prefix)/lib/mono/xbuild-frameworks/MonoAndroid"

include build-tools/scripts/BuildEverything.mk

# Must be after BuildEverything.mk - it uses variables defined there
include build-tools/scripts/Packaging.mk
include tests/api-compatibility/api-compatibility.mk

topdir  := $(shell pwd)

# Used by External XA Build
EXTERNAL_XA_PATH=$(topdir)
EXTERNAL_GIT_PATH=$(topdir)/external

-include $(EXTERNAL_GIT_PATH)/monodroid/xa-integration.mk

run-all-tests:
	@echo "PRINTING MONO VERSION"
	mono --version
	_r=0 ; \
	$(call MSBUILD_BINLOG,run-all-tests,,Test) $(TEST_TARGETS) /t:RunAllTests || _r=$$? ; \
	$(MAKE) run-api-compatibility-tests || _r=$$?; \
	exit $$_r

clean:
	$(call MSBUILD_BINLOG,clean) /t:Clean Xamarin.Android.sln
	tools/scripts/xabuild $(MSBUILD_FLAGS) /t:Clean Xamarin.Android-Tests.sln

distclean:
	# It may fail if we're cleaning a half-built tree, no harm done if we ignore it
	-$(MAKE) clean
	git clean -xdff
	git submodule foreach git clean -xdff

run-nunit-tests:
ifeq ($(SKIP_NUNIT_TESTS),)
	$(call MSBUILD_BINLOG,run-nunit-tests,,Test) $(TEST_TARGETS) /t:RunNUnitTests
endif # $(SKIP_NUNIT_TESTS) == ''

run-ji-tests:
	$(call MSBUILD_BINLOG,run-ji-tests,,Test) $(TEST_TARGETS) /t:RunJavaInteropTests

ifneq ($(PACKAGES),)
APK_TESTS_PROP = /p:ApkTests='"$(PACKAGES)"'
endif

run-apk-tests:
	_r=0 ; \
	$(call MSBUILD_BINLOG,run-apk-tests,,Test) $(TEST_TARGETS) /t:RunApkTests /p:RunApkTestsTarget=RunPerformanceApkTests $(APK_TESTS_PROP) || _r=$$? ; \
	$(call MSBUILD_BINLOG,run-apk-tests,,Test) $(TEST_TARGETS) /t:RunApkTests $(APK_TESTS_PROP) || _r = $$? ; \
	exit $$_r

run-performance-tests:
	$(call MSBUILD_BINLOG,run-performance-tests,,Test) $(TEST_TARGETS) /t:RunPerformanceTests

list-nunit-tests:
	$(MSBUILD) $(MSBUILD_FLAGS) $(TEST_TARGETS) /t:ListNUnitTests

include build-tools/scripts/runtime-helpers.mk

.PHONY: prepare-build-init
prepare-build-init:
	mkdir -p $(dir $(PREPARE_BUILD_LOG))
	msbuild $(PREPARE_RESTORE_FLAGS) $(PREPARE_SOLUTION) /t:Restore

.PHONY: prepare-build
prepare-build: prepare-build-init
	msbuild $(PREPARE_MSBUILD_FLAGS) $(PREPARE_SOLUTION)

.PHONY: prepare-build-ci
prepare-build-ci: prepare-build-init
	msbuild $(PREPARE_MSBUILD_FLAGS) $(PREPARE_SOLUTION) $(_MSBUILD_ARGS)

.PHONY: prepare
prepare:: prepare-build
	mono --debug $(PREPARE_EXE) $(_PREPARE_ARGS)

.PHONY: prepare-help
prepare-help: prepare-build
	mono --debug $(PREPARE_EXE) -h

# Hack: The current commercial pipeline doesn't pass all the required arguments when preparing the build, in particular it doesn't override the
# ABI targets to build and so the prepare step configures only for the default set (armeabi-v7a, arm64-v8a, x86, $HOST_OS) which is not enough.
# The `jenkins` rule in `BuildEverything.mk`, invoked by the commercial pipeline, now calls the rule below in which we rebuild the bootstrapper
# with all the required properties set to include all the ABIs - it should fix the build. After the PR is merged, the commercial pipeline should
# be modified to do the right thing instead.
#
# Commercial pipeline should also set PREPARE_CI=1 when calling targets. Since this is currently not done, we have to pass $(_PREPARE_CI_MODE_ARGS)
# directly below
#
.PHONY: prepare-jenkins
prepare-jenkins: prepare-build-ci prepare-commercial
	@echo preparing jenkins build
	mono --debug $(PREPARE_EXE) $(_PREPARE_ARGS) $(_PREPARE_CI_MODE_ARGS)

# This should go away once we can modify the commercial pipeline for the bootstrapper
.PHONY: prepare-commercial
ifeq ($(USE_COMMERCIAL_INSTALLER_NAME),true)
prepare-commercial:
	cd $(TOP) && ./configure --with-xamarin-android='$(XAMARIN_ANDROID_PATH)'
	mkdir -p $(XA_MSBUILD_DIR)

else
prepare-commercial:
endif

.PHONY: prepare-update-mono

prepare-update-mono: prepare-build-ci
	mono --debug $(PREPARE_EXE) $(_PREPARE_ARGS) $(_PREPARE_CI_MODE_ARGS) /s:UpdateMono

# These targets exist only temporarily to satisfy requirements of the commercial build (since we can't modify the pipeline script in this PR)
.PHONY: prepare-deps

# Commercial pipeline installs an older version of Mono and in effect we fail. `prepare-deps` is called after provisionator is ran and so we
# can, temporarily, re-update Mono here. After the PR is merged and commercial pipeline updated, this step should be removed.
prepare-deps: prepare-update-mono
	@echo prepare-deps is no-op, prepare-jenkins or prepare do the work instead

prepare-image-dependencies: prepare-build-ci
	mono --debug $(PREPARE_EXE) $(_PREPARE_ARGS) $(_PREPARE_CI_MODE_ARGS) -s:PrepareImageDependencies

prepare-external-git-dependencies: prepare-build-ci prepare-update-mono
	mono --debug $(PREPARE_EXE) $(_PREPARE_ARGS) $(_PREPARE_CI_MODE_ARGS) -s:PrepareExternalGitDependencies
