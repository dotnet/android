.PHONY: leeroy jenkins leeroy-all

jenkins:
ifeq ($(PREPARE_CI_PR)$(PREPARE_CI),00)
	$(MAKE) PREPARE_ARGS=-a prepare
else
	$(MAKE) prepare
endif
ifneq ("$(wildcard $(topdir)/external/monodroid/Makefile)","")
	cd $(topdir)/external/monodroid && ./configure --with-xamarin-android='$(topdir)'
	$(call DOTNET_BINLOG,build-commercial) $(SOLUTION) -t:BuildExternal
endif
	$(MAKE) leeroy

leeroy:
	$(call DOTNET_BINLOG,leeroy) $(SOLUTION) $(_MSBUILD_ARGS)
	$(call DOTNET_BINLOG,setup-workload) -t:ConfigureLocalWorkload build-tools/create-packs/Microsoft.Android.Sdk.proj
