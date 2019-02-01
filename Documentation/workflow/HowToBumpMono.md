# How To Bump Mono

There will eventually be two ways that xamarin-android depends on mono:

 1. Via [source code](#mono-source), or
 2. Via [mono archive](#mono-archive)


<a name="mono-source" />

## Mono Source

When using mono from source -- which is required for all versions of mono
before the `mono/2018-10` release branch -- the
[`external/mono`](../../external) git submodule reference is used.

Currently, many parts of the xamarin-android build system *require* the
presence and use of mono source code, including:

  * The [`src/Mono.Posix`](../../src/Mono.Posix) and
    [`src/Mono.Data.Sqlite`](../../src/Mono.Data.Sqlite) builds.
  * Inclusion of PDB2MDB source code within
    [`src/Xamarin.Android.Build.Tasks`](../../src/Xamarin.Android.Build.Tasks).
  * Inclusion of networking and related code into
    [`src/monodroid`](../../src/monodroid).

We are working to remove these source dependencies so that we can support using
a [Mono Archive](#mono-archive) in the future.

Until a Mono Archive *exists* and can be *used*, source code integration must
fulfill the following checklist:

  - [ ] [Update `.gitmodules`](#update-gitmodules).
  - [ ] [Update `external/mono` submodule reference](#update-mono-submodule).
  - [ ] [Update system mono used for the build](#update-system-mono).
  - [ ] [Update `MonoAndroid` Profile Assemblies](#update-profile)
  - [ ] [Ensure it *builds*](#build).
  - [ ] [Ensure unit tests *pass*](#unit-tests).
  - [ ] [Check for API Breakage](#api-validation).
  - [ ] [Create a Pull Request](#create-pr).
  - [ ] [Ask for QA Validation](#qa-validation) (***LAST***).


<a name="update-gitmodules" />

### Update `.gitmodules`

Update [`.gitmodules`](../../.gitmodules) to refer to the correct mono branch.


<a name="update-mono-submodule" />

### Update `external/mono`

Update the git submodule reference that [`external/mono`](../../external) refers to:

	cd external/mono
	git checkout BRANCH-NAME
	git pull --rebase


<a name="update-system-mono" />

### Update system mono

The `$(MonoRequiredMinimumVersion)` and `$(MonoRequiredMaximumVersion)` values
within [`Configuration.props`](../../Configuration.props)
should be updated to correspond to the version number used in the mono submodule.

These version numbers can be found in
[mono's `configure.ac`](https://github.com/mono/mono/blob/master/configure.ac)
in the `AC_INIT()` statement.

The `$(_DarwinMonoFramework)` and `%(RequiredProgram.DarwinMinimumUrl)` values
within [`build-tools/dependencies/dependencies.projitems`](../../build-tools/dependencies/dependencies.projitems)
should be updated to corresponds to the version number used in the mono submodule.

`%(DarwinMinimumUrl)` must be a macOS `.pkg` file and must exist.

For example, see commit
[606675b5](https://github.com/xamarin/xamarin-android/commit/606675b59f52595e3030c529de4c856fb347edd8):

```diff
diff --git a/Configuration.props b/Configuration.props
index a2a9c1d1..ec78ddb4 100644
--- a/Configuration.props
+++ b/Configuration.props
@@ -70,8 +70,8 @@
     <JavaInteropSourceDirectory Condition=" '$(JavaInteropSourceDirectory)' == '' ">$(MSBuildThisFileDirectory)external\Java.Interop</JavaInteropSourceDirectory>
     <LlvmSourceDirectory Condition=" '$(LlvmSourceDirectory)' == '' ">$(MSBuildThisFileDirectory)external\llvm</LlvmSourceDirectory>
     <MonoSourceDirectory>$(MSBuildThisFileDirectory)external\mono</MonoSourceDirectory>
-    <MonoRequiredMinimumVersion Condition=" '$(MonoRequiredMinimumVersion)' == '' ">5.14.0</MonoRequiredMinimumVersion>
-    <MonoRequiredMaximumVersion Condition=" '$(MonoRequiredMaximumVersion)' == '' ">5.15.0</MonoRequiredMaximumVersion>
+    <MonoRequiredMinimumVersion Condition=" '$(MonoRequiredMinimumVersion)' == '' ">5.16.0</MonoRequiredMinimumVersion>
+    <MonoRequiredMaximumVersion Condition=" '$(MonoRequiredMaximumVersion)' == '' ">5.17.0</MonoRequiredMaximumVersion>
     <IgnoreMaxMonoVersion Condition=" '$(IgnoreMaxMonoVersion)' == '' ">True</IgnoreMaxMonoVersion>
     <MonoRequiredDarwinMinimumVersion>$(MonoRequiredMinimumVersion).0</MonoRequiredDarwinMinimumVersion>
     <LinkerSourceDirectory>$(MSBuildThisFileDirectory)external\mono\external\linker</LinkerSourceDirectory>
diff --git a/build-tools/dependencies/dependencies.projitems b/build-tools/dependencies/dependencies.projitems
index f4a2f60e..1bd5d8c2 100644
--- a/build-tools/dependencies/dependencies.projitems
+++ b/build-tools/dependencies/dependencies.projitems
@@ -1,7 +1,7 @@
 <?xml version="1.0" encoding="utf-8"?>
 <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
   <PropertyGroup>
-    <_DarwinMonoFramework>MonoFramework-MDK-5.14.0.141.macos10.xamarin.universal.pkg</_DarwinMonoFramework>
+    <_DarwinMonoFramework>MonoFramework-MDK-5.16.0.106.macos10.xamarin.universal.pkg</_DarwinMonoFramework>
     <_AptGetInstall>apt-get -f -u install</_AptGetInstall>
   </PropertyGroup>
   <ItemGroup>
@@ -59,7 +59,7 @@
       <MaximumVersion Condition=" '$(IgnoreMaxMonoVersion)' == '' Or '$(IgnoreMaxMonoVersion)' == 'False' " >$(MonoRequiredMaximumVersion)</MaximumVersion>
       <DarwinMinimumVersion>$(MonoRequiredDarwinMinimumVersion)</DarwinMinimumVersion>
       <CurrentVersionCommand>$(MSBuildThisFileDirectory)..\scripts\mono-version</CurrentVersionCommand>
-      <DarwinMinimumUrl>https://xamjenkinsartifact.azureedge.net/build-package-osx-mono/2018-04/116/8ae8c52383b43892fb7a35dbf0992738bd52fa90/$(_DarwinMonoFramework)</DarwinMinimumUrl>
+      <DarwinMinimumUrl>https://xamjenkinsartifact.azureedge.net/build-package-osx-mono/2018-06/78/341142d7656f43239a041b2c44f00acfb8fa7c59/$(_DarwinMonoFramework)</DarwinMinimumUrl>
       <DarwinInstall>installer -pkg "$(AndroidToolchainCacheDirectory)\$(_DarwinMonoFramework)" -target /</DarwinInstall>
     </RequiredProgram>
   </ItemGroup>
```


<a name="update-profile" />

### Update `MonoAndroid` Profile Assemblies

[`src/mono-runtimes/ProfileAssemblies.projitems`](../../src/mono-runtimes/ProfileAssemblies.projitems)
has three item groups that may need to be updated:

  * `@(MonoFacadeAssembly)`
  * `@(MonoProfileAssembly)`
  * `@(MonoTestAssembly)`

There must be a `@(MonoFacadeAssembly)` entry for every Facade assembly that
must be shipped in the SDK.  Facade assemblies are installed into the
`bin/$(Configuration)/lib/xamarin.android/xbuild-frameworks/MonoAndroid/v1.0/Facades`
directory.

The `@(MonoFacadeAssembly)` list can be updated with this shell code on macOS:

	$ cd external/mono/mcs/class/Facades
	$ for d in `find . -depth 1 -type d | grep -v 'netstandard\|System.Drawing.Primitives\|System.Net.Http.Rtc' | sort -f` ; do
	  n=`basename "$d"`;
	  echo "    <MonoFacadeAssembly Include=\"$n.dll\" />";
	done | pbcopy

The `@(MonoProfileAssembly)` item group is for non-Facade assemblies, which are
installed into the
`bin/$(Configuration)/lib/xamarin.android/xbuild-frameworks/MonoAndroid/v1.0`
directory.  This item group must be updated whenever a new BCL assembly is added.

The `@(MonoTestAssembly)` item group contains unit test assemblies, executed by
[`tests/BCL-Tests`](../../tests/BCL-Tests).
The `%(MonoTestAssembly.SourcePath)` item metadata is the directory name within
`external/mono/mcs/class` which contains files needed for execution by the unit
tests.
The `%(MonoTestAssembly.TestType)` item metadata is the *type* of unit test
assembly; valid values are `xunit` (for xUnit unit test assemblies),
`reference` (for ???), and the empty string/not set (for NUnit assemblies).


<a name="build" />

### Ensure it *builds*

`make all` only builds a subset of the full Xamarin.Android SDK: support
for only select ABIs (host OS, x86, armeabi-v7a), only one `Mono.Android.dll`
version, and *no* builds for Windows support.

Ensure that `make all` builds *first*.  Once that builds, move on to using
`make jenkins`, which adds support for *all* ABIs, *plus* AOT and LLVM
compilers, plus Windows binaries.

See [`Documentation/building/unix/instructions.md`](../building/unix/instructions.md).


<a name="unit-tests" />

### Ensure Unit Tests *Pass*

Run the unit tests by using `make all-tests run-all-tests`.

All unit tests should pass.

See [`Documentation/building/unix/instructions.md`](../building/unix/instructions.md).


<a name="create-pr" />

### Create a Pull Request

Create a Pull Request (PR) on the https://github.com/xamarin/xamarin-android repo.

Add the **full-mono-integration-build** label to the PR.  This ensures that
the PR build is the full `make jenkins` build.

The resulting PR *should* be green before merging.


<a name="api-validation" />

### Check for API Breakage

The `make run-api-compatibility-tests` target will check the built assemblies
for API breakage.

PR builds may report API breakage in the left-hand pane, in an
**API Compatibility Checks** link.  If the API Compatibility Checks link is
not present, no API breakage was detected.

For example, this build:

<https://jenkins.mono-project.com/job/xamarin-android-pr-builder/4577/>

links to this set of reported API breakage:

<https://jenkins.mono-project.com/job/xamarin-android-pr-builder/4577/API_20Compatibility_20Checks/>

**To fix reported API breakage**, the mono sources may need to be updated, *or*
the [`xamarin/xamarin-android-api-compatibility`](https://github.com/xamarin/xamarin-android-api-compatibility/)
repo will need to be updated to "accept" the reported breakage, by updating
the [`external/xamarin-android-api-compatibility`](../../external) submodule
reference.

See the xamarin-android-api-compatibility repo for details.


<a name="qa-validation" />

### Ask for QA Validation

Asking QA for validation should be done ***last***.

Once QA approves, the mono bump PR can be merged.


<a name="mono-archive" />

## Mono Archives

A "Mono Archive" is a binary package (`.zip` file) which contains *binary*
mono artifacts, *not* source code.

See also:

  * The [Mono SDKs Integration project](https://github.com/xamarin/xamarin-android/projects/10)
  * Commit [f970cd50](https://github.com/xamarin/xamarin-android/commit/f970cd50d2c19dcb4b62cc1dd1198c31cc10a2df)

TODO. :-)
