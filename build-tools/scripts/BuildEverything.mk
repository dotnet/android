PRODUCT_VERSION   = $(shell $(MSBUILD) $(MSBUILD_FLAGS) /p:DoNotLoadOSProperties=True /nologo /v:minimal /t:GetProductVersion build-tools/scripts/Info.targets | tr -d '[[:space:]]')

GIT_BRANCH        = $(shell LANG=C git rev-parse --abbrev-ref HEAD | tr -d '[[:space:]]' | tr -C a-zA-Z0-9- _)
GIT_COMMIT        = $(shell LANG=C git log --no-color --first-parent -n1 --pretty=format:%h)

# In which commit did $(PRODUCT_VERSION) change? 00000000 if uncommitted
-commit-of-last-version-change    = $(shell LANG=C git blame Configuration.props | grep '<ProductVersion>' | grep -v grep | sed 's/ .*//')

# How many commits have passed since $(-commit-of-last-version-change)?
# "0" when commit hash is invalid (e.g. 00000000)
-num-commits-since-version-change = $(shell LANG=C git log $(-commit-of-last-version-change)..HEAD --oneline 2>/dev/null | wc -l | sed 's/ //g')

ZIP_OUTPUT_BASENAME   = oss-xamarin.android_v$(PRODUCT_VERSION).$(-num-commits-since-version-change)_$(OS)-$(OS_ARCH)_$(GIT_BRANCH)_$(GIT_COMMIT)
ZIP_OUTPUT            = $(ZIP_OUTPUT_BASENAME).zip


## The following values *must* use SPACE, **not** TAB, to separate values.

# $(ALL_API_LEVELS) and $(ALL_FRAMEWORKS) must be kept in sync w/ each other
ALL_API_LEVELS    = 1 2 3 4 5 6 7 8 9 10    11  12  13  14  15      16    17    18    19    20        21    22    23    24    25    26
# this was different from ALL_API_LEVELS when API Level 26 was "O". Same could happen in the future.
ALL_PLATFORM_IDS  = 1 2 3 4 5 6 7 8 9 10    11  12  13  14  15      16    17    18    19    20        21    22    23    24    25    O
# supported api levels
ALL_FRAMEWORKS    = _ _ _ _ _ _ _ _ _ v2.3  _   _   _   _   v4.0.3  v4.1  v4.2  v4.3  v4.4  v4.4.87   v5.0  v5.1  v6.0  v7.0  v7.1  v7.99.0
API_LEVELS        =                   10                    15      16    17    18    19    20        21    22    23    24    25    26
STABLE_API_LEVELS =                   10                    15      16    17    18    19    20        21    22    23    24

## The preceding values *must* use SPACE, **not** TAB, to separate values.


FRAMEWORKS        = $(foreach a, $(API_LEVELS), $(word $(a),$(ALL_FRAMEWORKS)))
STABLE_FRAMEWORKS = $(foreach a, $(STABLE_API_LEVELS), $(word $(a),$(ALL_FRAMEWORKS)))
PLATFORM_IDS      = $(foreach a, $(API_LEVELS), $(word $(a),$(ALL_PLATFORM_IDS)))

ALL_JIT_ABIS  = \
	armeabi \
	armeabi-v7a \
	arm64-v8a \
	x86 \
	x86_64

ALL_HOST_ABIS = \
	$(shell uname)

#
# On Linux we no disable building of all the cross-compiler/AOT environments.
# This is because CppSharp as used in Mono to generate C headers with
# runtime struct offsets doesn't work on Linux in the version used by Mono
#
# When/if CppSharp is fixed to work on Linux we can re-enable the code below
#
ifneq ($(OS),Linux)
ALL_HOST_ABIS += \
	mxe-Win32 \
	mxe-Win64

ALL_AOT_ABIS = \
	armeabi \
	win-armeabi \
	arm64 \
	win-arm64 \
	x86 \
	win-x86 \
	x86_64 \
	win-x86_64
endif

_space :=
_space +=

# usage: $(call join-with,SEPARATOR,LIST)
# Joins elements of LISt with SEPARATOR.
join-with = $(subst $(_space),$(1),$(strip $(2)))


_MSBUILD_ARGS	= \
	/p:AndroidSupportedTargetJitAbis=$(call join-with,:,$(ALL_JIT_ABIS)) \
	/p:AndroidSupportedHostJitAbis=$(call join-with,:,$(ALL_HOST_ABIS)) \
	/p:AndroidSupportedTargetAotAbis=$(call join-with,:,$(ALL_AOT_ABIS))

CONFIGURATIONS ?= Debug Release

.PHONY: leeroy jenkins leeroy-all opentk-jcw framework-assemblies runtime-libraries task-assemblies
.PHONY: create-vsix

jenkins: prepare leeroy $(ZIP_OUTPUT)

leeroy: leeroy-all runtime-libraries task-assemblies framework-assemblies opentk-jcw

leeroy-all:
	$(foreach conf, $(CONFIGURATIONS), \
		$(MSBUILD) $(MSBUILD_FLAGS) Xamarin.Android.sln /p:Configuration=$(conf) $(_MSBUILD_ARGS) ; )

task-assemblies:
	$(foreach conf, $(CONFIGURATIONS), \
		$(MSBUILD) $(MSBUILD_FLAGS) /p:Configuration=$(conf) $(_MSBUILD_ARGS) $(SOLUTION); )

framework-assemblies:
	PREV_VERSION="v1.0"; \
	$(foreach a, $(API_LEVELS), \
		CUR_VERSION=`echo "$(ALL_FRAMEWORKS)"|tr -s " "|cut -d " " -s -f $(a)`; \
		$(foreach conf, $(CONFIGURATIONS), \
			REDIST_FILE=bin/$(conf)/lib/xbuild-frameworks/MonoAndroid/$${CUR_VERSION}/RedistList/FrameworkList.xml; \
			grep -q $${PREV_VERSION} $${REDIST_FILE}; \
			if [ $$? -ne 0 ] ; then \
				rm -f bin/$(conf)/lib/xbuild-frameworks/MonoAndroid/$${CUR_VERSION}/RedistList/FrameworkList.xml; \
			fi; \
			$(MSBUILD) $(MSBUILD_FLAGS) src/Mono.Android/Mono.Android.csproj /p:Configuration=$(conf)   $(_MSBUILD_ARGS) /p:AndroidApiLevel=$(a)  /p:AndroidPlatformId=$(word $(a), $(ALL_PLATFORM_IDS)) /p:AndroidFrameworkVersion=$${CUR_VERSION} /p:AndroidPreviousFrameworkVersion=$${PREV_VERSION}; ) \
		PREV_VERSION=$${CUR_VERSION}; ) \
	$(foreach conf, $(CONFIGURATIONS), \
		rm -f bin/$(conf)/lib/xbuild-frameworks/MonoAndroid/v1.0/Xamarin.Android.NUnitLite.dll; \
		$(MSBUILD) $(MSBUILD_FLAGS) src/Xamarin.Android.NUnitLite/Xamarin.Android.NUnitLite.csproj /p:Configuration=$(conf) $(_MSBUILD_ARGS) /p:AndroidApiLevel=$(firstword $(API_LEVELS)) /p:AndroidFrameworkVersion=$(firstword $(FRAMEWORKS)); )

runtime-libraries:
	$(foreach conf, $(CONFIGURATIONS), \
		$(MSBUILD) $(MSBUILD_FLAGS) /p:Configuration=$(conf)   $(_MSBUILD_ARGS) $(SOLUTION); )

opentk-jcw:
	$(foreach a, $(API_LEVELS), \
		$(foreach conf, $(CONFIGURATIONS), \
			touch bin/$(conf)/lib/xbuild-frameworks/MonoAndroid/*/OpenTK-1.0.dll; \
			$(MSBUILD) $(MSBUILD_FLAGS) src/OpenTK-1.0/OpenTK.csproj /t:GenerateJavaCallableWrappers /p:Configuration=$(conf) $(_MSBUILD_ARGS) /p:AndroidApiLevel=$(a) /p:AndroidPlatformId=$(word $(a), $(ALL_PLATFORM_IDS)) /p:AndroidFrameworkVersion=$(word $(a), $(ALL_FRAMEWORKS)); ))

_BUNDLE_ZIPS_INCLUDE  = \
	$(ZIP_OUTPUT_BASENAME)/bin/Debug \
	$(ZIP_OUTPUT_BASENAME)/bin/Release

_BUNDLE_ZIPS_EXCLUDE  = \
	$(ZIP_OUTPUT_BASENAME)/bin/*/bundle-*.zip

create-vsix:
	$(foreach conf, $(CONFIGURATIONS), \
		MONO_IOMAP=all MONO_OPTIONS=--arch=64 msbuild $(MSBUILD_FLAGS) build-tools/create-vsix/create-vsix.csproj /p:Configuration=$(conf) /p:CreateVsixContainer=True ; )

package-oss-name:
	@echo ZIP_OUTPUT=$(ZIP_OUTPUT)

package-oss $(ZIP_OUTPUT):
	if [ -d bin/Debug/bin ]   ; then cp tools/scripts/xabuild bin/Debug/bin   ; fi
	if [ -d bin/Release/bin ] ; then cp tools/scripts/xabuild bin/Release/bin ; fi
	if [ ! -d $(ZIP_OUTPUT_BASENAME) ] ; then mkdir $(ZIP_OUTPUT_BASENAME) ; fi
	if [ ! -L $(ZIP_OUTPUT_BASENAME)/bin ] ; then ln -s ../bin $(ZIP_OUTPUT_BASENAME) ; fi
	zip -r "$(ZIP_OUTPUT)" \
		`ls -1d $(_BUNDLE_ZIPS_INCLUDE) 2>/dev/null` \
		--exclude `ls -1d $(_BUNDLE_ZIPS_EXCLUDE) 2>/dev/null`
