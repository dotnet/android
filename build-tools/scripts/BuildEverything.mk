.PHONY: leeroy jenkins leeroy-all

jenkins:
	$(MAKE) prepare
	$(MAKE) leeroy

leeroy:
	$(call DOTNET_BINLOG,leeroy) $(SOLUTION) $(_MSBUILD_ARGS)
	$(call DOTNET_BINLOG,monoandroid-preview) $(SOLUTION) -t:BuildExtraApiLevels
	$(call DOTNET_BINLOG,setup-workload) -t:ConfigureLocalWorkload build-tools/create-packs/Microsoft.Android.Sdk.proj
