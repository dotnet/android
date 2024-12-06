.PHONY: leeroy jenkins leeroy-all

jenkins:
ifeq ($(PREPARE_CI_PR)$(PREPARE_CI),00)
	$(MAKE) PREPARE_ARGS=-a prepare
else
	$(MAKE) prepare
endif
ifneq ("$(wildcard $(topdir)/external/android-platform-support/src/Xamarin.Android.Build.Debugging.Tasks/Xamarin.Android.Build.Debugging.Tasks.csproj)","")
	$(call SYSTEM_DOTNET_BINLOG,build-commercial,msbuild) $(SOLUTION) -t:BuildExternal
endif
	$(MAKE) leeroy

leeroy:
	$(call DOTNET_BINLOG,leeroy) $(SOLUTION) $(_MSBUILD_ARGS)
	$(call DOTNET_BINLOG,preview-monoandroid) src/Mono.Android/Mono.Android.csproj -p:BuildLatestPreview=true
	$(call DOTNET_BINLOG,setup-workload) -t:ConfigureLocalWorkload build-tools/create-packs/Microsoft.Android.Sdk.proj
