OS            := $(shell uname)
OS_ARCH       := $(shell uname -m)
V             ?= 0
CONFIGURATION = Debug
MSBUILD       = xbuild /p:Configuration=$(CONFIGURATION) $(MSBUILD_ARGS)
RUNTIME       := $(shell if [ -f `which mono64` ] ; then echo mono64 ; else echo mono; fi) --debug=casts

NUNIT_TESTS = \
	bin/Test$(CONFIGURATION)/Xamarin.Android.Build.Tests.dll

NUNIT_CONSOLE = packages/NUnit.ConsoleRunner.3.2.1/tools/nunit3-console.exe

ifneq ($(V),0)
MONO_OPTIONS += --debug
MSBUILD      += /v:d
endif

ifneq ($(MONO_OPTIONS),)
export MONO_OPTIONS
endif

all:
	$(MSBUILD)

prepare:: prepare-external prepare-props

prepare-external:
	git submodule update --init --recursive
	nuget restore
	(cd `$(MSBUILD) /nologo /v:minimal /t:GetJavaInteropFullPath build-tools/scripts/Paths.targets` && nuget restore)

prepare-props:
	cp Configuration.Java.Interop.Override.props external/Java.Interop/Configuration.Override.props
	./build-tools/scripts/generate-os-info Configuration.OperatingSystem.props
	cp `$(MSBUILD) /nologo /v:minimal /t:GetMonoSourceFullPath build-tools/scripts/Paths.targets`/mcs/class/msfinal.pub .

include build-tools/scripts/BuildEverything.mk

# Please keep the package names sorted
ifeq ($(OS),Linux)
UBUNTU_DEPS          = \
	autoconf \
	autotools-dev \
	automake \
	clang \
	curl \
	g++-mingw-w64 \
	gcc-mingw-w64 \
	git \
	libtool \
	libzip4 \
	linux-libc-dev \
	make \
	openjdk-8-jdk \
	unzip \
	vim-common

ifeq ($(OS_ARCH),x86_64)
UBUNTU_DEPS          += \
	lib32stdc++6 \
	lib32z1 \
	libx32tinfo-dev \
	linux-libc-dev:i386 \
	zlib1g-dev:i386
endif
LINUX_DISTRO         := $(shell lsb_release -i -s || true)
LINUX_DISTRO_RELEASE := $(shell lsb_release -r -s || true)
BINFMT_MISC_TROUBLE  := cli win

prepare:: linux-prepare-$(LINUX_DISTRO) linux-prepare-$(LINUX_DISTRO)-$(LINUX_DISTRO_RELEASE)
	@BINFMT_WARN=no ; \
	for m in $(BINFMT_MISC_TROUBLE); do \
		if [ -f /proc/sys/fs/binfmt_misc/$$m ]; then \
			BINFMT_WARN=yes ; \
		fi ; \
	done ; \
	if [ "x$$BINFMT_WARN" = "xyes" ]; then \
		cat Documentation/binfmt_misc-warning-Linux.txt ; \
	fi

linux-prepare-Ubuntu::
	@echo Installing build depedencies for $(LINUX_DISTRO)
	@echo Will use sudo, please provide your password as needed
	sudo apt-get -f -u install $(UBUNTU_DEPS)

linux-prepare-$(LINUX_DISTRO)::

linux-prepare-$(LINUX_DISTRO)-$(LINUX_DISTRO_RELEASE)::
endif

run-all-tests: run-nunit-tests run-apk-tests

clean:
	$(MSBUILD) /t:Clean

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
		-output=bin/Test$(CONFIGURATION)/TestOutput-$(basename $(notdir $(1))).txt ;
endef

run-nunit-tests: $(NUNIT_TESTS)
	$(foreach t,$(NUNIT_TESTS), $(call RUN_NUNIT_TEST,$(t),1))

# Test .apk projects must satisfy the following requirements:
# 1. They must have a UnDeploy target
# 2. They must have a Deploy target
# 3. They must have a RunTests target
TEST_APK_PROJECTS = \
	src/Mono.Android/Test/Mono.Android-Tests.csproj

# Syntax: $(call RUN_TEST_APK,path/to/project.csproj)
define RUN_TEST_APK
	# Must use xabuild to ensure correct assemblies are resolved
	tools/scripts/xabuild /t:SignAndroidPackage $(1) && \
	$(MSBUILD) /t:UnDeploy $(1) && \
	$(MSBUILD) /t:Deploy $(1) && \
	$(MSBUILD) /t:RunTests $(1) $(if $(ADB_TARGET),"/p:AdbTarget=$(ADB_TARGET)",)
endef

run-apk-tests:
	$(foreach p, $(TEST_APK_PROJECTS), $(call RUN_TEST_APK, $(p)))
