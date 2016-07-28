# $(ALL_API_LEVELS) and $(ALL_FRAMEWORKS) must be kept in sync w/ each other
ALL_API_LEVELS    = 1 2 3 4 5 6 7 8 9 10    11  12  13  14  15      16    17    18    19    20        21    22    23    24
# this was different when API Level 21 was "L". Same could happen in the future.
ALL_PLATFORM_IDS  = 1 2 3 4 5 6 7 8 9 10    11  12  13  14  15      16    17    18    19    20        21    22    23    24
# supported api levels
ALL_FRAMEWORKS    = _ _ _ _ _ _ _ _ _ v2.3  _   _   _   _   v4.0.3  v4.1  v4.2  v4.3  v4.4  v4.4.87   v5.0  v5.1  v6.0  v7.0
API_LEVELS        =                   10                    15      16    17    18    19    20        21    22    23    24
STABLE_API_LEVELS =                   10                    15      16    17    18    19    20        21    22    23

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
	$(shell uname) \
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

leeroy jenkins: prepare $(RUNTIME_LIBRARIES) $(TASK_ASSEMBLIES) $(FRAMEWORK_ASSEMBLIES)

$(TASK_ASSEMBLIES): bin/%/lib/xbuild/Xamarin/Android/Xamarin.Android.Build.Tasks.dll:
	$(MSBUILD) /p:Configuration=$* $(_MSBUILD_ARGS)

$(FRAMEWORK_ASSEMBLIES):
	$(foreach a, $(API_LEVELS), \
		$(MSBUILD) src/Mono.Android/Mono.Android.csproj /p:Configuration=Debug   $(_MSBUILD_ARGS) /p:AndroidApiLevel=$(a) /p:AndroidFrameworkVersion=$(word $(a), $(ALL_FRAMEWORKS));  \
		$(MSBUILD) src/Mono.Android/Mono.Android.csproj /p:Configuration=Release $(_MSBUILD_ARGS) /p:AndroidApiLevel=$(a) /p:AndroidFrameworkVersion=$(word $(a), $(ALL_FRAMEWORKS)); )

$(RUNTIME_LIBRARIES):
	$(MSBUILD) /p:Configuration=Debug $(_MSBUILD_ARGS)
	$(MSBUILD) /p:Configuration=Release $(_MSBUILD_ARGS)
