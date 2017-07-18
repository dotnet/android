export OS            := $(shell uname)
export OS_ARCH       := $(shell uname -m)
export NO_SUDO ?= false
V             ?= 0
prefix				= /usr/local
CONFIGURATION = Debug
RUNTIME       := $(shell if [ -f "`which mono64`" ] ; then echo mono64 ; else echo mono; fi) --debug=casts
SOLUTION      = Xamarin.Android.sln

NUNIT_TESTS = \
	bin/Test$(CONFIGURATION)/Xamarin.Android.Build.Tests.dll

NUNIT_CONSOLE = packages/NUnit.ConsoleRunner.3.2.1/tools/nunit3-console.exe

ifneq ($(V),0)
MONO_OPTIONS += --debug
endif

ifneq ($(MONO_OPTIONS),)
export MONO_OPTIONS
endif

include build-tools/scripts/msbuild.mk
all::
	$(MSBUILD) $(MSBUILD_FLAGS) $(SOLUTION)

all-tests::
	MSBUILD="$(MSBUILD)" tools/scripts/xabuild $(MSBUILD_FLAGS) Xamarin.Android-Tests.sln

install::
	@if [ ! -d "bin/$(CONFIGURATION)" ]; then \
		echo "run 'make all' before you execute 'make install'!"; \
		exit 1; \
	fi
	-mkdir -p "$(prefix)/lib/mono/xbuild-frameworks"
	-mkdir -p "$(prefix)/lib/xamarin.android"
	-mkdir -p "$(prefix)/lib/mono/xbuild/Xamarin/"
	cp -a "bin/$(CONFIGURATION)/." "$(prefix)/lib/xamarin.android/"
	cp tools/scripts/xabuild "$(prefix)/bin/xabuild"
	-rm -rf "$(prefix)/lib/mono/xbuild/Xamarin/Android"
	-rm -rf "$(prefix)/lib/mono/xbuild-frameworks/MonoAndroid"
	ln -s "$(prefix)/lib/xamarin.android/lib/xbuild/Xamarin/Android/" "$(prefix)/lib/mono/xbuild/Xamarin/Android"
	ln -s "$(prefix)/lib/xamarin.android/lib/xbuild-frameworks/MonoAndroid/" "$(prefix)/lib/mono/xbuild-frameworks/MonoAndroid"

uninstall::
	rm -rf "$(prefix)/lib/xamarin.android/" "$(prefix)/bin/xabuild"
	rm "$(prefix)/lib/mono/xbuild/Xamarin/Android"
	rm "$(prefix)/lib/mono/xbuild-frameworks/MonoAndroid"

ifeq ($(OS),Linux)
export LINUX_DISTRO         := $(shell lsb_release -i -s || true)
export LINUX_DISTRO_RELEASE := $(shell lsb_release -r -s || true)
prepare:: linux-prepare
endif # $(OS)=Linux

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
	if [ -f build-tools/scripts/dependencies/linux-prepare-$(LINUX_DISTRO).sh ]; then \
		sh build-tools/scripts/dependencies/linux-prepare-$(LINUX_DISTRO).sh; \
	elif [ -f build-tools/scripts/dependencies/linux-prepare-$(LINUX_DISTRO)-$(LINUX_DISTRO_RELEASE).sh ]; then \
		sh build-tools/scripts/dependencies/linux-prepare-$(LINUX_DISTRO)-$(LINUX_DISTRO_RELEASE).sh; \
	fi; \

# $(call GetPath,path)
GetPath   = $(shell $(MSBUILD) $(MSBUILD_FLAGS) /p:DoNotLoadOSProperties=True /nologo /v:minimal /t:Get$(1)FullPath build-tools/scripts/Paths.targets | tr -d '[[:space:]]' )

MSBUILD_PREPARE_PROJS = \
	src/Xamarin.Android.Build.Tasks/Xamarin.Android.Build.Tasks.csproj

prepare-deps:
	./build-tools/scripts/generate-os-info Configuration.OperatingSystem.props
	$(MSBUILD) $(MSBUILD_FLAGS) build-tools/dependencies/dependencies.mdproj

prepare-external: prepare-deps
	git submodule update --init --recursive
	nuget restore $(SOLUTION)
	nuget restore Xamarin.Android-Tests.sln
	(cd $(call GetPath,JavaInterop) && make prepare)
	(cd $(call GetPath,JavaInterop) && make bin/BuildDebug/JdkInfo.props)

prepare-props: prepare-external
	cp Configuration.Java.Interop.Override.props external/Java.Interop/Configuration.Override.props
	cp $(call GetPath,MonoSource)/mcs/class/msfinal.pub .

prepare-msbuild: prepare-props
ifeq ($(USE_MSBUILD),1)
	for proj in $(MSBUILD_PREPARE_PROJS); do \
		$(MSBUILD) $(MSBUILD_FLAGS) "$$proj"; \
	done
endif	# msbuild

include build-tools/scripts/BuildEverything.mk
include tests/api-compatibility/api-compatibility.mk

run-all-tests: run-nunit-tests run-ji-tests run-apk-tests run-api-compatibility-tests

clean:
	$(MSBUILD) $(MSBUILD_FLAGS) /t:Clean Xamarin.Android.sln
	tools/scripts/xabuild $(MSBUILD_FLAGS) /t:Clean Xamarin.Android-Tests.sln

distclean:
	# It may fail if we're cleaning a half-built tree, no harm done if we ignore it
	-$(MAKE) clean
	git clean -xdff
	git submodule foreach git clean -xdff

# $(call RUN_NUNIT_TEST,filename,log-lref?)
define RUN_NUNIT_TEST
	MONO_TRACE_LISTENER=Console.Out \
	$(RUNTIME) --runtime=v4.0.0 \
		$(NUNIT_CONSOLE) $(NUNIT_EXTRA) $(1) \
		$(if $(RUN),-run:$(RUN)) \
		--result="TestResult-$(basename $(notdir $(1))).xml;format=nunit2" \
		-output=bin/Test$(CONFIGURATION)/TestOutput-$(basename $(notdir $(1))).txt \
	|| true ; \
	if [ -f "bin/Test$(CONFIGURATION)/TestOutput-$(basename $(notdir $(1))).txt" ] ; then \
		cat bin/Test$(CONFIGURATION)/TestOutput-$(basename $(notdir $(1))).txt ; \
	fi
endef

run-nunit-tests: $(NUNIT_TESTS)
	$(foreach t,$(NUNIT_TESTS), $(call RUN_NUNIT_TEST,$(t),1))

run-ji-tests:
	$(MAKE) -C "$(call GetPath,JavaInterop)" CONFIGURATION=$(CONFIGURATION) all
	ANDROID_SDK_PATH="$(call GetPath,AndroidSdk)" $(MAKE) -C "$(call GetPath,JavaInterop)" CONFIGURATION=$(CONFIGURATION) run-all-tests || true
	cp "$(call GetPath,JavaInterop)"/TestResult-*.xml .

# .apk files to test on-device need to:
# (1) Have their .csproj files listed here
# (2) Add a `@(UnitTestApk)` entry to `tests/RunApkTests.targets`
TEST_APK_PROJECTS = \
	src/Mono.Android/Test/Mono.Android-Tests.csproj \
	tests/CodeGen-Binding/Xamarin.Android.JcwGen-Tests/Xamarin.Android.JcwGen-Tests.csproj \
	tests/locales/Xamarin.Android.Locale-Tests/Xamarin.Android.Locale-Tests.csproj

# Syntax: $(call BUILD_TEST_APK,path/to/project.csproj)
define BUILD_TEST_APK
	# Must use xabuild to ensure correct assemblies are resolved
	MSBUILD="$(MSBUILD)" tools/scripts/xabuild /t:SignAndroidPackage $(1)
endef	# BUILD_TEST_APK

run-apk-tests:
	$(foreach p, $(TEST_APK_PROJECTS), $(call BUILD_TEST_APK, $(p)))
	$(MSBUILD) $(MSBUILD_FLAGS) tests/RunApkTests.targets
