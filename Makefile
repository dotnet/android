export OS_NAME       := $(shell uname)
export OS_ARCH       := $(shell uname -m)
export NO_SUDO ?= false
V             ?= 0
prefix				= /usr/local
CONFIGURATION = Debug
RUNTIME       := $(shell if [ -f "`which mono64`" ] ; then echo mono64 ; else echo mono; fi) --debug=casts
SOLUTION      = Xamarin.Android.sln
TEST_TARGETS  = build-tools/scripts/RunTests.targets
API_LEVEL     ?=

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
	$(_SLN_BUILD) $(MSBUILD_FLAGS) $(SOLUTION)

all-tests::
	MSBUILD="$(MSBUILD)" tools/scripts/xabuild $(MSBUILD_FLAGS) Xamarin.Android-Tests.sln

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
	-rm -rf "$(prefix)/lib/mono/xbuild/Xamarin/Android"
	-rm -rf "$(prefix)/lib/mono/xbuild-frameworks/MonoAndroid"
	ln -s "$(prefix)/lib/xamarin.android/xbuild/Xamarin/Android/" "$(prefix)/lib/mono/xbuild/Xamarin/Android"
	ln -s "$(prefix)/lib/xamarin.android/xbuild-frameworks/MonoAndroid/" "$(prefix)/lib/mono/xbuild-frameworks/MonoAndroid"
	if [ ! -e "$(prefix)/bin/mono" ]; then \
		cp tools/scripts/xabuild "$(prefix)/bin/xabuild"; \
	fi

uninstall::
	rm -rf "$(prefix)/lib/xamarin.android/" "$(prefix)/bin/xabuild"
	rm "$(prefix)/lib/mono/xbuild/Xamarin/Android"
	rm "$(prefix)/lib/mono/xbuild-frameworks/MonoAndroid"

ifeq ($(OS_NAME),Linux)
export LINUX_DISTRO         := $(shell lsb_release -i -s || true)
export LINUX_DISTRO_RELEASE := $(shell lsb_release -r -s || true)
prepare:: linux-prepare
endif # $(OS_NAME)=Linux

prepare:: prepare-msbuild

linux-prepare::
	BINFMT_MISC_TROUBLE="cli win" \
	BINFMT_WARN=no ; \
	for m in $BINFMT_MISC_TROUBLE; do \
		if [ -f /proc/sys/fs/binfmt_misc/$$m ]; then \
			BINFMT_WARN=yes ; \
		fi ; \
	done ; \
	if [ "x$$BINFMT_WARN" = "xyes" ]; then \
		cat Documentation/binfmt_misc-warning-Linux.txt ; \
	fi; \
	if [ -f build-tools/scripts/dependencies/linux-prepare-$(LINUX_DISTRO)-$(LINUX_DISTRO_RELEASE).sh ]; then \
		sh build-tools/scripts/dependencies/linux-prepare-$(LINUX_DISTRO)-$(LINUX_DISTRO_RELEASE).sh; \
	elif [ -f build-tools/scripts/dependencies/linux-prepare-$(LINUX_DISTRO).sh ]; then \
		sh build-tools/scripts/dependencies/linux-prepare-$(LINUX_DISTRO).sh; \
	fi

# $(call GetPath,path)
GetPath   = $(shell $(MSBUILD) $(MSBUILD_FLAGS) /p:DoNotLoadOSProperties=True /nologo /v:minimal /t:Get$(1)FullPath build-tools/scripts/Paths.targets | tr -d '[[:space:]]' )

MSBUILD_PREPARE_PROJS = \
	build-tools/mono-runtimes/mono-runtimes.csproj \
	src/Xamarin.Android.Build.Tasks/Xamarin.Android.Build.Tasks.csproj

prepare-external:
	git submodule update --init --recursive
	nuget restore $(SOLUTION)
	nuget restore Xamarin.Android-Tests.sln
	$(foreach conf, $(CONFIGURATIONS), \
		(cd external/xamarin-android-tools && make prepare CONFIGURATION=$(conf)) && \
		(cd $(call GetPath,JavaInterop) && make prepare CONFIGURATION=$(conf) JI_MAX_JDK=8) && \
		(cd $(call GetPath,JavaInterop) && make bin/Build$(conf)/JdkInfo.props CONFIGURATION=$(conf) JI_MAX_JDK=8) && ) \
	true

prepare-deps: prepare-external
	./build-tools/scripts/generate-os-info Configuration.OperatingSystem.props
	$(MSBUILD) $(MSBUILD_FLAGS) build-tools/dependencies/dependencies.csproj

prepare-props: prepare-deps
	cp build-tools/scripts/Configuration.Java.Interop.Override.props external/Java.Interop/Configuration.Override.props
	cp $(call GetPath,MonoSource)/mcs/class/msfinal.pub .

prepare-msbuild: prepare-props
ifeq ($(USE_MSBUILD),1)
	for proj in $(MSBUILD_PREPARE_PROJS); do \
		$(MSBUILD) $(MSBUILD_FLAGS) "$$proj" || exit 1; \
	done
endif	# msbuild

prepare-image-dependencies:
	$(MSBUILD) $(MSBUILD_FLAGS) build-tools/scripts/PrepareImageDependencies.targets /t:PrepareImageDependencies \
		/p:AndroidSupportedHostJitAbis=mxe-Win32:mxe-Win64
	cat bin/Build$(CONFIGURATION)/prepare-image-dependencies.sh | tr -d '\r' > prepare-image-dependencies.sh

include build-tools/scripts/BuildEverything.mk
include tests/api-compatibility/api-compatibility.mk

run-all-tests:
	$(MSBUILD) $(MSBUILD_FLAGS) $(TEST_TARGETS) /t:RunAllTests
	$(MAKE) run-api-compatibility-tests

clean:
	$(MSBUILD) $(MSBUILD_FLAGS) /t:Clean Xamarin.Android.sln
	tools/scripts/xabuild $(MSBUILD_FLAGS) /t:Clean Xamarin.Android-Tests.sln

distclean:
	# It may fail if we're cleaning a half-built tree, no harm done if we ignore it
	-$(MAKE) clean
	git clean -xdff
	git submodule foreach git clean -xdff

run-nunit-tests:
ifeq ($(SKIP_NUNIT_TESTS),)
	$(MSBUILD) $(MSBUILD_FLAGS) $(TEST_TARGETS) /t:RunNUnitTests
endif # $(SKIP_NUNIT_TESTS) == ''

run-ji-tests:
	$(MSBUILD) $(MSBUILD_FLAGS) $(TEST_TARGETS) /t:RunJavaInteropTests

run-apk-tests:
	$(MSBUILD) $(MSBUILD_FLAGS) $(TEST_TARGETS) /t:RunApkTests

include build-tools/scripts/runtime-helpers.mk
