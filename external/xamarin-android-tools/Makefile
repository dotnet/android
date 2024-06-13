CONFIGURATION   := Debug
OS              := $(shell uname)
V               ?= 0

include build-tools/scripts/msbuild.mk

all:
	$(MSBUILD) $(MSBUILD_FLAGS) Xamarin.Android.Tools.sln

clean:
	-$(MSBUILD) $(MSBUILD_FLAGS) /t:Clean Xamarin.Android.Tools.sln

run-all-tests:
	dotnet test -l "console;verbosity=detailed" -l trx \
		tests/Xamarin.Android.Tools.AndroidSdk-Tests/Xamarin.Android.Tools.AndroidSdk-Tests.csproj
	dotnet test -l "console;verbosity=detailed" -l trx \
		tests/Microsoft.Android.Build.BaseTasks-Tests/Microsoft.Android.Build.BaseTasks-Tests.csproj
