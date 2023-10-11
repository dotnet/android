create-installers: create-nupkgs

create-nupkgs:
	@echo Disk usage before create-nupkgs
	-df -h
	$(call DOTNET_BINLOG,create-all-packs) -t:CreateAllPacks $(topdir)/build-tools/create-packs/Microsoft.Android.Sdk.proj
