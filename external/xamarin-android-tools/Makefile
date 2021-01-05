CONFIGURATION   := Debug
OS              := $(shell uname)
V               ?= 0

include build-tools/scripts/msbuild.mk

all:
	$(MSBUILD) /restore $(MSBUILD_FLAGS) Xamarin.Android.Tools.sln

clean:
	-$(MSBUILD) $(MSBUILD_FLAGS) /t:Clean Xamarin.Android.Tools.sln

prepare:
	nuget restore Xamarin.Android.Tools.sln

run-all-tests:
	dotnet test -l "console;verbosity=detailed" -l trx \
		tests/Xamarin.Android.Tools.AndroidSdk-Tests/Xamarin.Android.Tools.AndroidSdk-Tests.csproj
