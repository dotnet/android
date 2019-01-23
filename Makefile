export OS_NAME       := $(shell uname)
export OS_ARCH       := $(shell uname -m)
export NO_SUDO       ?= false
V             ?= 0
prefix         = /usr/local
CONFIGURATION  = Debug
RUNTIME       := $(shell which mono64 2> /dev/null && echo mono64 || echo mono) --debug=casts
SOLUTION       = Xamarin.Android.sln
TEST_TARGETS   = build-tools/scripts/RunTests.targets
API_LEVEL     ?=

ifeq ($(OS_NAME),Darwin)
export MACOSX_DEPLOYMENT_TARGET := 10.11
endif

ifneq ($(V),0)
MONO_OPTIONS += --debug
endif

ifneq ($(MONO_OPTIONS),)
export MONO_OPTIONS
endif

include build-tools/scripts/msbuild.mk

ifeq ($(USE_MSBUILD),1)
_SLN_BUILD  = $(MSBUILD)
else    # $(MSBUILD) != 1
_SLN_BUILD  = MSBUILD="$(MSBUILD)" tools/scripts/xabuild
endif   # $(USE_MSBUILD) == 1

ifneq ($(API_LEVEL),)
MSBUILD_FLAGS += /p:AndroidApiLevel=$(API_LEVEL) /p:AndroidFrameworkVersion=$(word $(API_LEVEL), $(ALL_FRAMEWORKS))
endif

all::
	$(call MSBUILD_BINLOG,all,$(_SLN_BUILD)) $(MSBUILD_FLAGS) $(SOLUTION)

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

ifeq ($(OS_NAME),Linux)
export LINUX_DISTRO         := $(shell lsb_release -i -s || true)
export LINUX_DISTRO_RELEASE := $(shell lsb_release -r -s || true)
prepare:: linux-prepare
endif # $(OS_NAME)=Linux

prepare:: prepare-paths prepare-msbuild

linux-prepare::
	BINFMT_MISC_TROUBLE="cli win" \
	BINFMT_WARN=no ; \
	for m in ${BINFMT_MISC_TROUBLE}; do \
		if [ -f "/proc/sys/fs/binfmt_misc/$$m" ]; then \
			BINFMT_WARN=yes ; \
		fi ; \
	done ; \
	if [ "x${BINFMT_WARN}" = "xyes" ]; then \
		cat Documentation/binfmt_misc-warning-Linux.txt ; \
	fi; \
	if [ -f build-tools/scripts/dependencies/linux-prepare-$(LINUX_DISTRO)-$(LINUX_DISTRO_RELEASE).sh ]; then \
		sh build-tools/scripts/dependencies/linux-prepare-$(LINUX_DISTRO)-$(LINUX_DISTRO_RELEASE).sh $(LINUX_DISTRO_RELEASE); \
	elif [ -f build-tools/scripts/dependencies/linux-prepare-$(LINUX_DISTRO).sh ]; then \
		sh build-tools/scripts/dependencies/linux-prepare-$(LINUX_DISTRO).sh $(LINUX_DISTRO_RELEASE); \
	fi

# $(call GetPath,path)
GetPath   = $(shell $(MSBUILD) $(MSBUILD_FLAGS) /p:DoNotLoadOSProperties=True /nologo /v:minimal /t:Get$(1)FullPath build-tools/scripts/Paths.targets | tr -d '[[:space:]]' )

MSBUILD_PREPARE_PROJS = \
	src/mono-runtimes/mono-runtimes.csproj \
	src/Xamarin.Android.Build.Tasks/Xamarin.Android.Build.Tasks.csproj

prepare-deps:
	git submodule update --init --recursive
	./build-tools/scripts/generate-os-info Configuration.OperatingSystem.props
	mkdir -p bin/Build$(CONFIGURATION)
	$(call MSBUILD_BINLOG,prepare-deps) build-tools/dependencies/dependencies.csproj

prepare-external: prepare-deps
	nuget restore $(SOLUTION)
	nuget restore Xamarin.Android-Tests.sln
	(cd external/xamarin-android-tools && make prepare CONFIGURATION=$(CONFIGURATION))
	(cd $(call GetPath,JavaInterop) && make prepare CONFIGURATION=$(CONFIGURATION) JI_MAX_JDK=8)
	(cd $(call GetPath,JavaInterop) && make bin/Build$(CONFIGURATION)/JdkInfo.props CONFIGURATION=$(CONFIGURATION) JI_MAX_JDK=8)

prepare-bundle: prepare-external
	$(call MSBUILD_BINLOG,prepare-bundle) build-tools/download-bundle/download-bundle.csproj
	$(call MSBUILD_BINLOG,prepare-restore) $(MSBUILD_FLAGS) tests/Xamarin.Forms-Performance-Integration/Xamarin.Forms.Performance.Integration.csproj /t:Restore

prepare-props: prepare-bundle
	cp $(call GetPath,JavaInterop)/external/Mono.Cecil* "$(call GetPath,MonoSource)/external"
	cp "$(call GetPath,JavaInterop)/product.snk" "$(call GetPath,MonoSource)"
	cp build-tools/scripts/Configuration.Java.Interop.Override.props external/Java.Interop/Configuration.Override.props
	cp $(call GetPath,MonoSource)/mcs/class/msfinal.pub .

prepare-msbuild: prepare-props
ifeq ($(USE_MSBUILD),1)
	for proj in $(MSBUILD_PREPARE_PROJS); do \
		$(call MSBUILD_BINLOG,prepare-msbuild) "$$proj" || exit 1; \
	done
endif	# msbuild

prepare-image-dependencies:
	$(call MSBUILD_BINLOG,prepare-image-deps) build-tools/scripts/PrepareImageDependencies.targets /t:PrepareImageDependencies \
		/p:AndroidSupportedHostJitAbis=mxe-Win32:mxe-Win64
	cat bin/Build$(CONFIGURATION)/prepare-image-dependencies.sh | tr -d '\r' > prepare-image-dependencies.sh

include build-tools/scripts/BuildEverything.mk

# Must be after BuildEverything.mk - it uses variables defined there
include build-tools/scripts/Packaging.mk
include tests/api-compatibility/api-compatibility.mk

topdir  := $(shell pwd)


XA_BUILD_PATHS_OUT = bin/Test$(CONFIGURATION)/XABuildPaths.cs

prepare-paths: $(XA_BUILD_PATHS_OUT)

$(XA_BUILD_PATHS_OUT): bin/Test%/XABuildPaths.cs: build-tools/scripts/XABuildPaths.cs.in
	mkdir -p $(shell dirname $@)
	sed -e 's;@CONFIGURATION@;$*;g' \
	    -e 's;@TOP_DIRECTORY@;$(topdir);g' < $< > $@
	cat $@


# Usage: $(call CALL_CREATE_THIRD_PARTY_NOTICES,path,licenseType,includeExternalDeps,includeBuildDeps)
define CREATE_THIRD_PARTY_NOTICES
	$(call MSBUILD_BINLOG,create-tpn,$(MSBUILD)) $(_MSBUILD_ARGS) \
		$(topdir)/build-tools/ThirdPartyNotices/ThirdPartyNotices.csproj \
		/p:Configuration=$(CONFIGURATION) \
		/p:ThirdPartyNoticeFile=$(topdir)/$(1) \
		/p:ThirdPartyNoticeLicenseType=$(2) \
		/p:TpnIncludeExternalDependencies=$(3) \
		/p:TpnIncludeBuildDependencies=$(4)
endef # CREATE_THIRD_PARTY_NOTICES

prepare:: prepare-tpn

TPN_LICENSE_FILES = $(shell grep -h '<LicenseFile>' external/*.tpnitems src/*.tpnitems \
	| sed -E 's,<LicenseFile>(.*)</LicenseFile>,\1,g;s,.\(MSBuildThisFileDirectory\),$(topdir)/external/,g' \
	| tr \\ / )

# Usage: $(call CREATE_THIRD_PARTY_NOTICES_RULE,path,licenseType,includeExternalDeps,includeBuildDeps)
define CREATE_THIRD_PARTY_NOTICES_RULE
prepare-tpn:: $(1)

$(1) $(topdir)/$(1): build-tools/ThirdPartyNotices/ThirdPartyNotices.csproj \
		$(wildcard external/*.tpnitems src/*.tpnitems) \
		$(TPN_LICENSE_FILES)
	$(call CREATE_THIRD_PARTY_NOTICES,$(1),$(2),$(3),$(4))
endef # CREATE_THIRD_PARTY_NOTICES_RULE

THIRD_PARTY_NOTICE_LICENSE_TYPE = microsoft-oss

$(eval $(call CREATE_THIRD_PARTY_NOTICES_RULE,ThirdPartyNotices.txt,foundation,False,False))
$(eval $(call CREATE_THIRD_PARTY_NOTICES_RULE,bin/$(CONFIGURATION)/lib/xamarin.android/ThirdPartyNotices.txt,$(THIRD_PARTY_NOTICE_LICENSE_TYPE),True,False))

run-all-tests:
	@echo "PRINTING MONO VERSION"
	mono --version
	$(call MSBUILD_BINLOG,run-all-tests) $(TEST_TARGETS) /t:RunAllTests
	$(MAKE) run-api-compatibility-tests

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
	$(call MSBUILD_BINLOG,run-nunit-tests) $(TEST_TARGETS) /t:RunNUnitTests
endif # $(SKIP_NUNIT_TESTS) == ''

run-ji-tests:
	$(call MSBUILD_BINLOG,run-ji-tests) $(TEST_TARGETS) /t:RunJavaInteropTests

ifneq ($(PACKAGES),)
APK_TESTS_PROP = /p:ApkTests='"$(PACKAGES)"'
endif

run-apk-tests:
	$(call MSBUILD_BINLOG,run-apk-tests) $(TEST_TARGETS) /t:RunApkTests $(APK_TESTS_PROP)

run-performance-tests:
	$(call MSBUILD_BINLOG,run-performance-tests) $(TEST_TARGETS) /t:RunPerformanceTests

list-nunit-tests:
	$(MSBUILD) $(MSBUILD_FLAGS) $(TEST_TARGETS) /t:ListNUnitTests

include build-tools/scripts/runtime-helpers.mk
