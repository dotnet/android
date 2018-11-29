PRODUCT_VERSION   = $(shell $(MSBUILD) $(MSBUILD_FLAGS) /p:DoNotLoadOSProperties=True /nologo /v:minimal /t:GetProductVersion build-tools/scripts/Info.targets | tr -d '[[:space:]]')

GIT_BRANCH        = $(shell LANG=C build-tools/scripts/get-git-branch.sh | tr -d '[[:space:]]' | tr -C a-zA-Z0-9- _)
GIT_COMMIT        = $(shell LANG=C git log --no-color --first-parent -n1 --pretty=format:%h)

# In which commit did $(PRODUCT_VERSION) change? 00000000 if uncommitted
-commit-of-last-version-change    = $(shell LANG=C git blame Configuration.props | grep '<ProductVersion>' | grep -v grep | sed 's/ .*//')

# How many commits have passed since $(-commit-of-last-version-change)?
# "0" when commit hash is invalid (e.g. 00000000)
-num-commits-since-version-change = $(shell LANG=C git log $(-commit-of-last-version-change)..HEAD --oneline 2>/dev/null | wc -l | sed 's/ //g')

ifeq ($(OS_NAME),Linux)
ZIP_EXTENSION         = tar.bz2
else
ZIP_EXTENSION         = zip
endif

ZIP_OUTPUT_BASENAME   = xamarin.android-oss_v$(PRODUCT_VERSION).$(-num-commits-since-version-change)_$(OS_NAME)-$(OS_ARCH)_$(GIT_BRANCH)_$(GIT_COMMIT)-$(CONFIGURATION)
ZIP_OUTPUT            = $(ZIP_OUTPUT_BASENAME).$(ZIP_EXTENSION)


## The following values *must* use SPACE, **not** TAB, to separate values.

# $(ALL_API_LEVELS) and $(ALL_FRAMEWORKS) must be kept in sync w/ each other
ALL_API_LEVELS    = 1 2 3 4 5 6 7 8 9 10  11  12  13  14  15  16  17  18  19    20        21    22    23    24    25    26    27    28
# this was different from ALL_API_LEVELS when API Level 26 was "O". Same could happen in the future.
ALL_PLATFORM_IDS  = 1 2 3 4 5 6 7 8 9 10  11  12  13  14  15  16  17  18  19    20        21    22    23    24    25    26    27    28
# supported api levels
ALL_FRAMEWORKS    = _ _ _ _ _ _ _ _ _ _   _   _   _   _   _   _   _   _   v4.4  v4.4.87   v5.0  v5.1  v6.0  v7.0  v7.1  v8.0  v8.1  v9.0
API_LEVELS        =                                                       19    20        21    22    23    24    25    26    27    28
STABLE_API_LEVELS =                                                       19    20        21    22    23    24    25    26    27    28

## The preceding values *must* use SPACE, **not** TAB, to separate values.


FRAMEWORKS        = $(foreach a, $(API_LEVELS), $(word $(a),$(ALL_FRAMEWORKS)))
STABLE_FRAMEWORKS = $(foreach a, $(STABLE_API_LEVELS), $(word $(a),$(ALL_FRAMEWORKS)))
PLATFORM_IDS      = $(foreach a, $(API_LEVELS), $(word $(a),$(ALL_PLATFORM_IDS)))

ALL_JIT_ABIS  = \
	armeabi-v7a \
	arm64-v8a \
	x86 \
	x86_64

ALL_HOST_ABIS = \
	$(shell uname)

ALL_AOT_ABIS = \
	armeabi-v7a \
	arm64 \
	x86 \
	x86_64 \
	win-armeabi-v7a \
	win-arm64 \
	win-x86 \
	win-x86_64

ifneq ($(OS_NAME),Linux)
ALL_HOST_ABIS += \
	mxe-Win32 \
	mxe-Win64
endif

ifneq ($(OS_NAME),Linux)
MONO_OPTIONS += --arch=64
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

.PHONY: leeroy jenkins leeroy-all opentk-jcw framework-assemblies
.PHONY: create-vsix

jenkins: prepare leeroy $(ZIP_OUTPUT)

leeroy: leeroy-all framework-assemblies opentk-jcw

leeroy-all:
	$(call MSBUILD_BINLOG,leeroy-all,$(_SLN_BUILD)) $(SOLUTION) /p:Configuration=$(CONFIGURATION) $(_MSBUILD_ARGS) && \
	$(call CREATE_THIRD_PARTY_NOTICES,bin/$(CONFIGURATION)/lib/xamarin.android/ThirdPartyNotices.txt,$(THIRD_PARTY_NOTICE_LICENSE_TYPE),True,False)

framework-assemblies:
	PREV_VERSION="v1.0"; \
	$(foreach a, $(API_LEVELS), \
		CUR_VERSION=`echo "$(ALL_FRAMEWORKS)"|tr -s " "|cut -d " " -s -f $(a)`; \
		REDIST_FILE=bin/$(CONFIGURATION)/lib/xamarin.android/xbuild-frameworks/MonoAndroid/$${CUR_VERSION}/RedistList/FrameworkList.xml; \
		grep -q $${PREV_VERSION} $${REDIST_FILE}; \
		if [ $$? -ne 0 ] ; then \
			rm -f bin/$(CONFIGURATION)/lib/xamarin.android/xbuild-frameworks/MonoAndroid/$${CUR_VERSION}/RedistList/FrameworkList.xml; \
		fi; \
		$(call MSBUILD_BINLOG,Mono.Android,$(_SLN_BUILD)) src/Mono.Android/Mono.Android.csproj \
			/p:Configuration=$(CONFIGURATION) $(_MSBUILD_ARGS) \
			/p:AndroidApiLevel=$(a) /p:AndroidPlatformId=$(word $(a), $(ALL_PLATFORM_IDS)) /p:AndroidFrameworkVersion=$${CUR_VERSION} \
			/p:AndroidPreviousFrameworkVersion=$${PREV_VERSION} || exit 1; \
		PREV_VERSION=$${CUR_VERSION}; )
	rm -f bin/$(CONFIGURATION)/lib/xamarin.android/xbuild-frameworks/MonoAndroid/v1.0/Xamarin.Android.NUnitLite.dll; \
	$(call MSBUILD_BINLOG,NUnitLite,$(_SLN_BUILD)) $(MSBUILD_FLAGS) src/Xamarin.Android.NUnitLite/Xamarin.Android.NUnitLite.csproj \
		/p:Configuration=$(CONFIGURATION) $(_MSBUILD_ARGS) \
		/p:AndroidApiLevel=$(firstword $(API_LEVELS)) /p:AndroidPlatformId=$(word $(firstword $(API_LEVELS)), $(ALL_PLATFORM_IDS)) \
		/p:AndroidFrameworkVersion=$(firstword $(FRAMEWORKS)) || exit 1;
	_latest_stable_framework=$$($(MSBUILD) /p:DoNotLoadOSProperties=True /nologo /v:minimal /t:GetAndroidLatestStableFrameworkVersion build-tools/scripts/Info.targets | tr -d '[[:space:]]') ; \
	rm -f "bin/$(CONFIGURATION)/lib/xamarin.android/xbuild-frameworks/MonoAndroid/$$_latest_stable_framework"/Mono.Android.Export.* ; \
	$(call MSBUILD_BINLOG,Mono.Android.Export,$(_SLN_BUILD)) $(MSBUILD_FLAGS) src/Mono.Android.Export/Mono.Android.Export.csproj \
		/p:Configuration=$(CONFIGURATION) $(_MSBUILD_ARGS) \
		/p:AndroidApiLevel=$(firstword $(API_LEVELS)) /p:AndroidPlatformId=$(word $(firstword $(API_LEVELS)), $(ALL_PLATFORM_IDS)) \
		/p:AndroidFrameworkVersion=$(firstword $(FRAMEWORKS)) || exit 1; \
	rm -f "bin/$(CONFIGURATION)/lib/xamarin.android/xbuild-frameworks/MonoAndroid/$$_latest_stable_framework"/OpenTK-1.0.* ; \
	$(call MSBUILD_BINLOG,OpenTK,$(_SLN_BUILD)) $(MSBUILD_FLAGS) src/OpenTK-1.0/OpenTK.csproj \
		/p:Configuration=$(CONFIGURATION) $(_MSBUILD_ARGS) \
		/p:AndroidApiLevel=$(firstword $(API_LEVELS)) /p:AndroidPlatformId=$(word $(firstword $(API_LEVELS)), $(ALL_PLATFORM_IDS)) \
		/p:AndroidFrameworkVersion=$(firstword $(FRAMEWORKS)) || exit 1;

opentk-jcw:
	$(foreach a, $(API_LEVELS), \
		touch bin/$(CONFIGURATION)/lib/xamarin.android/xbuild-frameworks/MonoAndroid/*/OpenTK-1.0.dll; \
		$(call MSBUILD_BINLOG,OpenTK-JCW,$(_SLN_BUILD)) $(MSBUILD_FLAGS) src/OpenTK-1.0/OpenTK.csproj \
			/t:GenerateJavaCallableWrappers /p:Configuration=$(CONFIGURATION) $(_MSBUILD_ARGS) \
			/p:AndroidApiLevel=$(a) /p:AndroidPlatformId=$(word $(a), $(ALL_PLATFORM_IDS)) /p:AndroidFrameworkVersion=$(word $(a), $(ALL_FRAMEWORKS)) || exit 1; )
