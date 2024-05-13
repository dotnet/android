<!--toc:start-->
- [Build Configuration](#build-configuration)
  - [Options suitable to use in builds for public use](#options-suitable-to-use-in-builds-for-public-use)
  - [Options suitable for local development](#options-suitable-for-local-development)
    - [Native runtime (`src/monodroid`)](#native-runtime-srcmonodroid)
      - [Disable function inlining](#disable-function-inlining)
      - [Don't strip the runtime shared libraries](#dont-strip-the-runtime-shared-libraries)
<!--toc:end-->

# Build Configuration

The .NET for Android build is heavily dependent on MSBuild, with the *intention*
that it should (eventually?) be possible to build the project simply by
checking out the repo, loading `Xamarin.Android.sln` into an IDE, and Building
the solution. (This isn't currently possible, and may never be, but it's
the *vision*.)

However, some properties may need to be altered in order to suit your
requirements, such as the location of a cache directory to store
the Android SDK and NDK.

## Options suitable to use in builds for public use

To modify the build process, copy
[`Configuration.Override.props.in`](../../Configuration.Override.props.in)
to `Configuration.Override.props`, and edit the file as appropriate.
`Configuration.Override.props` is `<Import/>`ed by `Configuration.props`
and will override any default values specified in `Configuration.props`.

Overridable MSBuild properties include:

  * `$(AutoProvision)`: Automatically install required dependencies, if possible.
    Only supported on macOS and certain Linux distros.

  * `$(AutoProvisionUsesSudo)`: Use `sudo` when installing dependencies.

  * `$(AndroidApiLevel)`: The Android API level to bind in `src/Mono.Android`.
    This is an integer value, e.g. `15` for
    [API-15 (Android 4.0.3)](http://developer.android.com/about/versions/android-4.0.3.html).

  * `$(AndroidFrameworkVersion)`: The Xamarin.Android `$(TargetFrameworkVersion)`
    version which corresponds to `$(AndroidApiLevel)`. This is *usually* the
    Android version number with a leading `v`, e.g. `v4.0.3` for API-15.

  * `$(AndroidLatestStableApiLevel)`: The highest/latest Android API level that
    has a stable API. The `src/Xamarin.Android.Build.Tasks` build uses this
    value to reference files built within `src/Mono.Android`.

    This should be consistent with `$(AndroidLatestStableFrameworkVersion)` and
    `$(AndroidLatestStablePlatformId)`.

    This should only be updated when a new API level is declared stable.

  * `$(AndroidLatestStableFrameworkVersion)`: The highest/latest Xamarin.Android
    `$(TargetFrameworkVersion)` value which has a stable API.
    The `src/Xamarin.Android.Build.Tasks` build uses this value to reference
    files built within `src/Mono.Android`.

    This should be consistent with `$(AndroidLatestStableApiLevel)` and
    `$(AndroidLatestStablePlatformId)`.

    This should only be updated when a new API level is declared stable.

  * `$(AndroidLatestStablePlatformId)`: The highest/latest Android platform ID
    which has a stable API.
    The `src/Xamarin.Android.Build.Tasks` build uses this value to reference
    files built within `src/Mono.Android`.

    This should be consistent with `$(AndroidLatestStableApiLevel)` and
    `$(AndroidLatestStableFrameworkVersion)`.

    This should only be updated when a new API level is declared stable.

  * `$(AndroidPlatformId)`: The "Platform ID" for the `android.jar` to use when
    building `src/Mono.Android`. This is usually the same value as
    `$(AndroidApiLevel)`, but may differ with Android Preview releases.

  * `$(AndroidSupportedTargetAotAbis)`: The Android ABIs for which to build the
    Mono AOT compilers. The AOT compilers are required in order to set the
    [`$(RunAOTCompilation)`][runaotcompilation] app configuration property to True.

    [runaotcompilation]: https://developer.xamarin.com/guides/android/under_the_hood/build_process/#RunAOTCompilation

  * `$(AndroidSupportedTargetJitAbis)`: The Android ABIs for which to build the
    the Mono JIT for inclusion within apps. This is a `:`-separated list of
    ABIs to build. Supported values are:

      * `armeabi-v7a`
      * `arm64-v8a`
      * `x86`
      * `x86_64`

  * `$(AndroidToolchainCacheDirectory)`: The directory to cache the downloaded
    Android NDK and SDK files. This value defaults to
    `$(HOME)\android-archives`.

  * `$(AndroidToolchainDirectory)`: The directory to install the downloaded
    Android NDK and SDK files. This value defaults to
    `$(HOME)\android-toolchain`.

  * `$(DisableApiCompatibilityCheck)`: disable running the API compatibility
    check when building Mono.Android if set to `True`. The check is performed
    by default.

  * `$(IgnoreMaxMonoVersion)`: Skip the enforcement of the `$(MonoRequiredMaximumVersion)`
    property. This is so that developers can run against the latest
    and greatest. But the build system can enforce the min and max
    versions. The default is `true`, however on CI we use:

         /p:IgnoreMaxMonoVersion=False

  * `$(JavaInteropSourceDirectory)`: The Java.Interop source directory to
    build and reference projects from. By default, this is
    `external/Java.Interop` directory, maintained by `git submodule update`.

  * `$(JavaSdkDirectory)`: The JDK directory.  `$(JavaSdkDirectory)\bin\java`,
    `$(JavaSdkDirectory)\bin\javac`, and `$(JavaSdkDirectory)\bin\jar` must
    exist.

    If not specified, we'll attempt to use a default based on e.g. the
    `JAVA_HOME` environment variable and other "known" directories.

  * `$(MakeConcurrency)`: **make**(1) parameters to use intended to influence
    the number of CPU cores used when **make**(1) executes. By default this uses
    `-jCOUNT`, where `COUNT` is obtained from `sysctl hw.ncpu`.

  * `$(MonoRequiredMinimumVersion)`: The minimum *system* mono version that is
    supported in order to allow a build to continue. Policy is to require a
    system mono which corresponds vaguely to the [`external/mono`](external)
    version. This is not strictly required; older mono versions *may* work, they
    just are not tested, and thus not guaranteed or supported.

  * `$(MonoRequiredMaximumVersion)`: The max *system* mono version that is
    required. This is so that we can ensure a stable build environment by
    making sure we dont install unstable versions.

  * `$(MonoSgenBridgeVersion)`: The Mono SGEN Bridge version to support.
    Valid values include:

      * `4`: Mono 4.6 support.
      * `5`: Mono 4.8 and above support. This is the default.

  * `$(AndroidEnableAssemblyCompression)`: Defaults to `True`. When enabled, all the
     assemblies placed in the APK will be compressed in `Release` builds. `Debug`
     builds are not affected.

## Options suitable for local development

### Native runtime (`src/monodroid`)

Note that in order for the native build settings to have full effect, one needs to make sure that
the entire native runtime is rebuilt **and** that all `cmake` files are regenerated.  This is true
on the very first build, but rebuilds may require forcing the entire runtime to be rebuilt.

The simplest way to do it is to remove `src/monodroid/obj` and run the usual build from the
repository's root directory.

#### Disable function inlining

The native runtime by default builds with function inlining enabled, making heavy use of
the feature in order to generate faster code.  However, when debugging a native crash inlining
causes stack traces to point to unlikely locations, reporting the outer function as the crash
location instead of the inlined function where crash actually happened.  There are two ways to
enable this mode of operation:

  1. Export the `XA_NO_INLINE` environment variable before building either the entire repository
     or just `src/monodroid/`
  2. Set the MSBuild property `DoNotInlineMonodroid` to `true`, when building `src/monodroid/monodroid.csproj`

Doing either will force all normally inlined functions to be strictly preserved and kept
separate.  The generated code will be slower, but crash stack traces should be much more precise.

#### Don't strip the runtime shared libraries

Similar to the previous section, this option makes crash stack traces more informative.  In normal
builds, all the debugging information is stripped from the runtime shared libraries, thus making
stack traces rarely point to anything more than the surrounding function name (which may sometimes
be misleading, too).  Just as for inlining, the no-strip mode can be enabled with one of two ways:

  1. Export the `XA_NO_STRIP` environment variable before building either the entire repository
     or just `src/monodroid/`
  2. Set the MSBuild property `DoNotStripMonodroid` to `true`, when building `src/monodroid/monodroid.csproj`
