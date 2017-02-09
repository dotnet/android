PRODUCT_VERSION   = $(shell $(MSBUILD) $(MSBUILD_FLAGS) /p:DoNotLoadOSProperties=True /nologo /v:minimal /t:GetProductVersion build-tools/scripts/Info.targets | tr -d '[[:space:]]')

GIT_BRANCH        = $(shell LANG=C git branch --contains HEAD | grep -E -v '\(.*detached.*\)' | sed 's/^. //' | head -1 | tr -d '[[:space:]]' | tr -C a-zA-Z0-9- _ )
GIT_COMMIT        = $(shell LANG=C git log --no-color --first-parent -n1 --pretty=format:%h)

# In which commit did $(PRODUCT_VERSION) change? 00000000 if uncommitted
-commit-of-last-version-change    = $(shell LANG=C git blame Configuration.props | grep '<ProductVersion>' | grep -v grep | sed 's/ .*//')

# How many commits have passed since $(-commit-of-last-version-change)?
# "0" when commit hash is invalid (e.g. 00000000)
-num-commits-since-version-change = $(shell LANG=C git log $(-commit-of-last-version-change)..HEAD --oneline 2>/dev/null | wc -l | sed 's/ //g')

ZIP_OUTPUT_BASENAME   = oss-xamarin.android_v$(PRODUCT_VERSION).$(-num-commits-since-version-change)_$(OS)-$(OS_ARCH)_$(GIT_BRANCH)_$(GIT_COMMIT)
ZIP_OUTPUT            = $(ZIP_OUTPUT_BASENAME).zip


# $(ALL_API_LEVELS) and $(ALL_FRAMEWORKS) must be kept in sync w/ each other
ALL_API_LEVELS    = 1 2 3 4 5 6 7 8 9 10    11  12  13  14  15      16    17    18    19    20        21    22    23    24    25
# this was different when API Level 21 was "L". Same could happen in the future.
ALL_PLATFORM_IDS  = 1 2 3 4 5 6 7 8 9 10    11  12  13  14  15      16    17    18    19    20        21    22    23    24    25
# supported api levels
ALL_FRAMEWORKS    = _ _ _ _ _ _ _ _ _ v2.3  _   _   _   _   v4.0.3  v4.1  v4.2  v4.3  v4.4  v4.4.87   v5.0  v5.1  v6.0  v7.0  v7.1
API_LEVELS        =                   10                    15      16    17    18    19    20        21    22    23    24    25
STABLE_API_LEVELS =                   10                    15      16    17    18    19    20        21    22    23    24

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

TASK_ASSEMBLIES = \
	bin/Debug/lib/xbuild/Xamarin/Android/Xamarin.Android.Build.Tasks.dll    \
	bin/Release/lib/xbuild/Xamarin/Android/Xamarin.Android.Build.Tasks.dll

RUNTIME_LIBRARIES = \
	$(ALL_JIT_ABIS:%=bin/Debug/lib/xbuild/Xamarin/Android/lib/%/libmonosgen-2.0.so) \
	$(ALL_JIT_ABIS:%=bin/Release/lib/xbuild/Xamarin/Android/lib/%/libmonosgen-2.0.so)

FRAMEWORK_ASSEMBLIES = \
	$(FRAMEWORKS:%=bin/Debug/lib/xbuild-frameworks/MonoAndroid/%/Mono.Android.dll)    \
	$(FRAMEWORKS:%=bin/Release/lib/xbuild-frameworks/MonoAndroid/%/Mono.Android.dll)

leeroy jenkins: prepare $(RUNTIME_LIBRARIES) $(TASK_ASSEMBLIES) $(FRAMEWORK_ASSEMBLIES) $(ZIP_OUTPUT)

$(TASK_ASSEMBLIES): bin/%/lib/xbuild/Xamarin/Android/Xamarin.Android.Build.Tasks.dll:
	$(MSBUILD) $(MSBUILD_FLAGS) /p:Configuration=$* $(_MSBUILD_ARGS) $(SOLUTION)

$(FRAMEWORK_ASSEMBLIES):
	$(foreach a, $(API_LEVELS), \
		$(MSBUILD) $(MSBUILD_FLAGS) src/Mono.Android/Mono.Android.csproj /p:Configuration=Debug   $(_MSBUILD_ARGS) /p:AndroidApiLevel=$(a) /p:AndroidFrameworkVersion=$(word $(a), $(ALL_FRAMEWORKS));  \
		$(MSBUILD) $(MSBUILD_FLAGS) src/Mono.Android/Mono.Android.csproj /p:Configuration=Release $(_MSBUILD_ARGS) /p:AndroidApiLevel=$(a) /p:AndroidFrameworkVersion=$(word $(a), $(ALL_FRAMEWORKS)); )

$(RUNTIME_LIBRARIES):
	$(MSBUILD) $(MSBUILD_FLAGS) /p:Configuration=Debug   $(_MSBUILD_ARGS) $(SOLUTION)
	$(MSBUILD) $(MSBUILD_FLAGS) /p:Configuration=Release $(_MSBUILD_ARGS) $(SOLUTION)

_BUNDLE_ZIPS_INCLUDE  = \
	$(ZIP_OUTPUT_BASENAME)/bin/Debug \
	$(ZIP_OUTPUT_BASENAME)/bin/Release

_BUNDLE_ZIPS_EXCLUDE  = \
	$(ZIP_OUTPUT_BASENAME)/bin/*/bundle-*.zip

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
