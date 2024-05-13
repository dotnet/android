# How To Bump Mono

Android uses a binary artifcats package from mono that is tied to a specific version / commit. This makes it pretty trivial to bump mono.

The folloing checklist covers what you need to do.  Note, if you know the system mono version does not need changed, you can skip the first step.

  - [ ] [Update system mono used for the build](#update-system-mono).
  - [ ] [Update external mono commit in .external](#update-mono-external-commit).
  - [ ] [Ensure it *builds*](#build).
  - [ ] [Ensure unit tests *pass*](#unit-tests).
  - [ ] [Check for API Breakage](#api-validation).
  - [ ] [Create a Pull Request](#create-pr).
  - [ ] [Ask for QA Validation](#qa-validation) (***LAST***).

<a name="update-system-mono" />

### Update system mono

The `$(MonoDarwinPackageUrl)` property within [`Configuration.props`](../../Configuration.props)
should be updated to point to the absolute URL of the package.

The `$(MonoRequiredMinimumVersion)` and `$(MonoRequiredMaximumVersion)` properties
should be updated to correspond to the version number used in the mono submodule, and
the maximum version that should be used to build against.

These version numbers can be found in
[mono's `configure.ac`](https://github.com/mono/mono/blob/master/configure.ac)
in the `AC_INIT()` statement.

An example within Configuration.props:

`<MonoRequiredMinimumVersion Condition=" '$(MonoRequiredMinimumVersion)' == '' ">6.8.0</MonoRequiredMinimumVersion>`

`<MonoRequiredMaximumVersion Condition=" '$(MonoRequiredMaximumVersion)' == '' ">6.9.0</MonoRequiredMaximumVersion>`

<a name="update-mono-external-commit" />

### Update external mono commit in .external

Updating the commit in this file will pull in the version of mono you want to bump to. 

Within [`.external`](../../.external) indicate the mono branch and commit.  For example:

`mono/mono:2019-10@e9b5aec5ec7801df66117f2da730672ede15dcc6`

<a name="build" />

### Ensure it *builds*

`make all` only builds a subset of the full .NET for Android SDK: support
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

