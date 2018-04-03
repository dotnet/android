#
# Various helper/shortcut targets
#
MONO_RUNTIMES_DIR = src/mono-runtimes
MONO_RUNTIMES_PROJECT = $(MONO_RUNTIMES_DIR)/mono-runtimes.csproj
MONO_RUNTIMES_BUILD_DIR = $(MONO_RUNTIMES_DIR)/obj/$(CONFIGURATION)

# $(1) - architecture name
define CreateMonoRebuildRuleForArch
rebuild-$(1)-mono: touch-mono-head
	@if ! test -d $$(MONO_RUNTIMES_BUILD_DIR)/$(1); then \
		echo $$(MONO_RUNTIMES_BUILD_DIR)/$(1) ; \
		echo Runtime for architecture $(1) and configuration $$(CONFIGURATION) was not built yet or is not enabled in configuration or cached Mono runtime was installed ; \
		echo In order to build Mono for $(1) please enable it in Configuration.Override.props file and ; \
		echo Run the following command: make rebuild-mono ; \
		exit 1 ; \
	fi
	$$(MAKE) -C $$(MONO_RUNTIMES_BUILD_DIR)/$(1)
	$$(MSBUILD) /t:_InstallRuntimes $$(MONO_RUNTIMES_PROJECT)

.PHONY: rebuild-$(1)-mono
endef

#
# This is less than ideal, but that's how things are right now.
# Do NOT push the change to Mono!
#
touch-mono-head:
	@(cd external/mono; git commit --amend -C HEAD --date="`date`")

rebuild-mono: rebuild-all-mono
rebuild-mono-runtime: rebuild-all-mono
rebuild-all-mono: touch-mono-head
	$(MSBUILD) /t:ForceBuild $(MONO_RUNTIMES_PROJECT)

$(foreach arch,$(ALL_JIT_ABIS),$(eval $(call CreateMonoRebuildRuleForArch,$(arch))))

.PHONY: rebuild-mono rebuild-all-mono touch-mono-head

rebuild-mono-bcl-assembly: rebuild-bcl-assembly
rebuild-bcl-assembly:
	@if [ -z "$(ASSEMBLY)" ]; then \
		echo Assembly name required. Run make as follows: ; \
		echo $(MAKE) ASSEMBLY=bcl_assembly_name ; \
		exit 1 ; \
	fi
	$(MAKE) -C external/mono/mcs/class/$(ASSEMBLY)/ PROFILE=monodroid
	$(MSBUILD) /t:_InstallBcl $(MONO_RUNTIMES_PROJECT)

rebuild-all-bcl:
	@if [ ! -d $(MONO_RUNTIMES_BUILD_DIR)/host-$(OS_NAME) ]; then \
		echo Host Mono runtime for $(OS_NAME) has not been built ; \
		echo or cached Mono runtime was installed ; \
		echo In order to build host Mono for $(OS_NAME) please enable it in Configuration.Override.props file and ; \
		echo Run the following command: make rebuild-mono ; \
		echo Unable to rebuild all BCL assemblies ; \
		exit 1 ; \
	fi
	make -C $(MONO_RUNTIMES_BUILD_DIR)/host-$(OS_NAME)

.PHONY: rebuild-bcl-assembly rebuild-all-bcl

#
# $(1) - arch name
#
# NOTE: the first empty line must stay since Make displays it verbatim...
#
define MonoArchRebuildHelp

	@echo "  rebuild-$(1)-mono"
	@echo "     Rebuild and install Mono runtime for the $(1) architecture only regardless"
	@echo "     of whether a cached copy was used."
	@echo
endef

rebuild-help:
	@echo Helper targets to rebuild and install the Mono runtime and BCL assemblies
	@echo
	@echo "  rebuild-mono-runtime"
	@echo "     Rebuild and install Mono runtime for all configured architectures regardless"
	@echo "     of whether a cached copy was used."
	@echo
	$(foreach arch,$(ALL_JIT_ABIS),$(call MonoArchRebuildHelp,$(arch)))
	@echo "  rebuild-mono-bcl-assembly ASSEMBLY=bcl_assembly_name"
	@echo "     Where 'bcl_assembly_name' is base name of the assembly to rebuild, e.g. 'System' or 'System.Data'"
	@echo "     Rebuild and install a specific BCL assembly. Assembly name must be passed in the ASSEMBLY Make variable"
	@echo
	@echo "  rebuild-all-bcl"
	@echo "     Rebuild and install all the BCL assemblies"
	@echo
	@echo "ALL the rebuild-*-mono targets (including rebuild-mono) will modify the HEAD commit of Mono in external/mono"
	@echo "DO NOT UNDER ANY CIRCUMSTANCES PUSH THAT CHANGE TO MONO UPSTREAM"
	@echo

.PHONY: rebuild-help
