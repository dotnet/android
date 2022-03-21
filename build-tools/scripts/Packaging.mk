# If commercial components exist in output, set required variables
ifneq ("$(wildcard $(topdir)/bin/$(CONFIGURATION)/lib/xamarin.android/xbuild/Xamarin/Android/Xamarin.Android.Common.Debugging.targets)","")
PKG_LICENSE_EN=$(topdir)/external/monodroid/tools/scripts/License.txt
USE_COMMERCIAL_INSTALLER_NAME=true
EXPERIMENTAL=false
endif

create-installers: create-nupkgs create-pkg create-vsix

create-nupkgs:
	@echo Disk usage before create-nupkgs
	-df -h
	$(call DOTNET_BINLOG,create-all-packs) -t:CreateAllPacks $(topdir)/build-tools/create-packs/Microsoft.Android.Sdk.proj

create-pkg:
	$(call DOTNET_BINLOG,create-pkg) /t:CreatePkg \
		build-tools/create-pkg/create-pkg.csproj \
		$(if $(PACKAGE_VERSION),/p:ProductVersion="$(PACKAGE_VERSION)") \
		$(if $(PACKAGE_VERSION_REV),/p:XAVersionCommitCount="$(PACKAGE_VERSION_REV)") \
		$(if $(PKG_LICENSE_EN),/p:PkgLicenseSrcEn="$(PKG_LICENSE_EN)") \
		$(if $(PKG_OUTPUT_PATH),/p:PkgProductOutputPath="$(PKG_OUTPUT_PATH)") \
		$(if $(USE_COMMERCIAL_INSTALLER_NAME),/p:UseCommercialInstallerName="$(USE_COMMERCIAL_INSTALLER_NAME)") \
		$(if $(_MSBUILD_ARGS),"$(_MSBUILD_ARGS)")

create-workload-installers:
	$(call DOTNET_BINLOG,create-workload-installers) /t:CreateWorkloadInstallers \
		Xamarin.Android.sln \
		$(if $(_MSBUILD_ARGS),"$(_MSBUILD_ARGS)")

# create-vsix.csproj dependencies do not yet support `dotnet build`:
#    .nuget/packages/microsoft.vssdk.buildtools/17.0.4207-preview4/build/Microsoft.VSSDK.BuildTools.targets(16,5): error MSB4801: The task factory "CodeTaskFactory" is not supported on the .NET Core version of MSBuild.
create-vsix:
	MONO_IOMAP=all MONO_OPTIONS="$(MONO_OPTIONS)" $(call MSBUILD_BINLOG,create-vsix) /p:Configuration=$(CONFIGURATION) /p:CreateVsixContainer=True \
		build-tools/create-vsix/create-vsix.csproj \
		$(if $(VSIX),"/p:VsixPath=$(VSIX)") \
		$(if $(EXPERIMENTAL),/p:IsExperimental="$(EXPERIMENTAL)") \
		$(if $(PRODUCT_COMPONENT),/p:IsProductComponent="$(PRODUCT_COMPONENT)") \
		$(if $(PACKAGE_VERSION),/p:ProductVersion="$(PACKAGE_VERSION)") \
		$(if $(REPO_NAME),/p:XARepositoryName="$(REPO_NAME)") \
		$(if $(PACKAGE_HEAD_BRANCH),/p:XAVersionBranch="$(PACKAGE_HEAD_BRANCH)") \
		$(if $(PACKAGE_VERSION_REV),/p:XAVersionCommitCount="$(PACKAGE_VERSION_REV)") \
		$(if $(COMMIT),/p:XAVersionHash="$(COMMIT)") \
		$(if $(USE_COMMERCIAL_INSTALLER_NAME),/p:UseCommercialInstallerName="$(USE_COMMERCIAL_INSTALLER_NAME)") \
		$(if $(_MSBUILD_ARGS),"$(_MSBUILD_ARGS)")

package-oss-name:
	@echo ZIP_OUTPUT=$(ZIP_OUTPUT)

package-oss $(ZIP_OUTPUT):
	if [ -d bin/Debug/bin ]   ; then cp tools/scripts/xabuild bin/Debug/bin   ; fi
	if [ -d bin/Release/bin ] ; then cp tools/scripts/xabuild bin/Release/bin ; fi
	if [ ! -d $(ZIP_OUTPUT_BASENAME) ] ; then mkdir $(ZIP_OUTPUT_BASENAME) ; fi
	if [ ! -L $(ZIP_OUTPUT_BASENAME)/bin ] ; then ln -s ../bin $(ZIP_OUTPUT_BASENAME) ; fi
	if [ ! -f $(ZIP_OUTPUT_BASENAME)/ThirdPartyNotices.txt ] ; then \
		for f in bin/*/lib/xamarin.android/ThirdPartyNotices.txt ; do \
			cp -n "$$f" $(ZIP_OUTPUT_BASENAME) || true; \
		done; \
	fi
	_exclude_list=".__exclude_list.txt"; \
	ls -1d $(_BUNDLE_ZIPS_EXCLUDE) > "$$_exclude_list" 2>/dev/null ; \
	_sl="$(ZIP_OUTPUT_BASENAME)/bin/$(CONFIGURATION)/lib/xamarin.android/xbuild/.__sys_links.txt"; \
	if [ ! -f "$$_sl" ]; then continue; fi; \
	for f in `cat $$_sl` ; do \
		echo "$(ZIP_OUTPUT_BASENAME)/bin/$(CONFIGURATION)/lib/xamarin.android/xbuild/$$f" >> "$$_exclude_list"; \
	done
	echo "Exclude List:"
	cat ".__exclude_list.txt"
ifeq ($(ZIP_EXTENSION),zip)
	zip -r "$(ZIP_OUTPUT)" \
		`ls -1d $(_BUNDLE_ZIPS_INCLUDE) 2>/dev/null` \
		"-x@.__exclude_list.txt"
else ifeq ($(ZIP_EXTENSION),tar.bz2)
	tar --exclude-from=.__exclude_list.txt -cjhvf "$(ZIP_OUTPUT)" `ls -1d $(_BUNDLE_ZIPS_INCLUDE) 2>/dev/null`
endif
	-rm ".__exclude_list.txt"

package-deb: $(ZIP_OUTPUT)
	rm -fr $(ZIP_OUTPUT_BASENAME)
	tar xf $(ZIP_OUTPUT)
	cp -a build-tools/debian-metadata $(ZIP_OUTPUT_BASENAME)/debian
	sed "s/%CONFIG%/$(CONFIGURATION)/" $(ZIP_OUTPUT_BASENAME)/debian/xamarin.android-oss.install.in > $(ZIP_OUTPUT_BASENAME)/debian/xamarin.android-oss.install && rm -f $(ZIP_OUTPUT_BASENAME)/debian/xamarin.android-oss.install.in
	cp LICENSE $(ZIP_OUTPUT_BASENAME)/debian/copyright
	cd $(ZIP_OUTPUT_BASENAME) && DEBEMAIL="Xamarin Public Jenkins (auto-signing) <releng@xamarin.com>" dch --create -v $(PRODUCT_VERSION).$(-num-commits-since-version-change) --package xamarin.android-oss --force-distribution --distribution alpha "New release - please see git log for $(GIT_COMMIT)"
	cd $(ZIP_OUTPUT_BASENAME) && dpkg-buildpackage -us -uc -rfakeroot
